using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Storage;
using XW.Approve.Domain.Constants;
using XW.Approve.Domain.Entities;
using XW.Approve.Domain.Models;
using XW.Approve.Domain.Repositories;
using XW.Approve.Infrastructure.Persistence;

namespace XW.Approve.Infrastructure.Repositories;

internal sealed class ApproveProcessDesignRepository(ApproveDbContext db) : IApproveProcessDesignRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ApproveProcessDef>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await db.Set<ApproveProcessDef>()
            .AsNoTracking()
            .OrderByDescending(x => x.ModifyTime)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApproveProcessDef?> FindDefAsync(long id, CancellationToken cancellationToken = default) =>
        db.Set<ApproveProcessDef>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<bool> KeyExistsAsync(string processKey, CancellationToken cancellationToken = default) =>
        db.Set<ApproveProcessDef>().AsNoTracking().AnyAsync(x => x.ProcessKey == processKey, cancellationToken);

    /// <inheritdoc />
    public async Task<int> NextVersionAsync(string processKey, CancellationToken cancellationToken = default)
    {
        var max = await db.Set<ApproveProcessDef>()
            .AsNoTracking()
            .Where(x => x.ProcessKey == processKey)
            .Select(x => (int?)x.Version)
            .MaxAsync(cancellationToken);
        return (max ?? 0) + 1;
    }

    /// <inheritdoc />
    public async Task<ApproveProcessDesignRecord?> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        var def = await db.Set<ApproveProcessDef>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (def is null)
        {
            return null;
        }

        var nodes = await db.Set<ApproveProcessNode>()
            .AsNoTracking()
            .Where(x => x.ProcessDefId == id)
            .OrderBy(x => x.Ordinal)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var transitions = await db.Set<ApproveProcessTransition>()
            .AsNoTracking()
            .Where(x => x.ProcessDefId == id)
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var nodeIds = nodes.Select(x => x.Id).ToList();
        var resolvers = nodeIds.Count == 0
            ? []
            : await db.Set<ApproveResolver>()
                .AsNoTracking()
                .Where(x => nodeIds.Contains(x.ProcessNodeId))
                .OrderBy(x => x.Ordinal)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);

        var transitionIds = transitions.Select(x => x.Id).ToList();
        var groups = transitionIds.Count == 0
            ? []
            : await db.Set<ApproveConditionGroup>()
                .AsNoTracking()
                .Where(x => x.OwnerType == ApproveConditionOwner.Transition && transitionIds.Contains(x.OwnerId))
                .OrderBy(x => x.Ordinal)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);

        var groupIds = groups.Select(x => x.Id).ToList();
        var items = groupIds.Count == 0
            ? []
            : await db.Set<ApproveConditionItem>()
                .AsNoTracking()
                .Where(x => groupIds.Contains(x.GroupId))
                .OrderBy(x => x.Ordinal)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);

        return new ApproveProcessDesignRecord
        {
            Definition = def,
            Nodes = nodes,
            Transitions = transitions,
            Groups = groups,
            Items = items,
            Resolvers = resolvers,
            LayoutJson = await TryReadLayoutAsync(id, cancellationToken),
        };
    }

    /// <inheritdoc />
    public async Task InsertAsync(ApproveProcessDesignRecord record, CancellationToken cancellationToken = default)
    {
        db.Set<ApproveProcessDef>().Add(record.Definition);
        AddChildren(record);
        await db.SaveChangesAsync(cancellationToken);
        await TryWriteLayoutAsync(record.Definition.Id, record.LayoutJson, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ReplaceDraftAsync(ApproveProcessDesignRecord record, CancellationToken cancellationToken = default)
    {
        var id = record.Definition.Id;
        var def = await db.Set<ApproveProcessDef>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"流程 {id} 不存在");

        def.ProcessName = record.Definition.ProcessName;
        def.BusinessType = record.Definition.BusinessType;

        await DeleteChildrenAsync(id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        AddChildren(record);
        await db.SaveChangesAsync(cancellationToken);
        await TryWriteLayoutAsync(id, record.LayoutJson, cancellationToken);
    }

    /// <inheritdoc />
    public async Task PublishAsync(long id, CancellationToken cancellationToken = default)
    {
        var def = await db.Set<ApproveProcessDef>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"流程 {id} 不存在");

        var others = await db.Set<ApproveProcessDef>()
            .Where(x => x.ProcessKey == def.ProcessKey
                        && x.Id != id
                        && x.Status == ApproveProcessStatus.Active)
            .ToListAsync(cancellationToken);
        foreach (var other in others)
        {
            other.Status = ApproveProcessStatus.Disabled;
        }

        def.Status = ApproveProcessStatus.Active;

        var defaults = await db.Set<ApproveProcessBinding>()
            .Where(x => x.BusinessType == def.BusinessType
                        && x.BusinessSubType == string.Empty
                        && x.IsDefault)
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (defaults.Count == 0)
        {
            db.Set<ApproveProcessBinding>().Add(new ApproveProcessBinding
            {
                Id = IdGenerater.GetNextId(),
                BusinessType = def.BusinessType,
                BusinessSubType = string.Empty,
                ProcessDefId = def.Id,
                Priority = 0,
                IsDefault = true,
                Enabled = true,
            });
        }
        else
        {
            defaults[0].ProcessDefId = def.Id;
            defaults[0].Enabled = true;
            defaults[0].Priority = 0;
            for (var i = 1; i < defaults.Count; i++)
            {
                defaults[i].Enabled = false;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteDraftAsync(long id, CancellationToken cancellationToken = default)
    {
        await DeleteChildrenAsync(id, cancellationToken);

        var bindings = await db.Set<ApproveProcessBinding>()
            .Where(x => x.ProcessDefId == id)
            .ToListAsync(cancellationToken);
        if (bindings.Count > 0)
        {
            db.Set<ApproveProcessBinding>().RemoveRange(bindings);
        }

        var def = await db.Set<ApproveProcessDef>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (def is not null)
        {
            db.Set<ApproveProcessDef>().Remove(def);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApproveBusinessField>> ListFieldsAsync(
        string businessType,
        CancellationToken cancellationToken = default)
    {
        return await db.Set<ApproveBusinessField>()
            .AsNoTracking()
            .Where(x => x.BusinessType == businessType)
            .OrderBy(x => x.Ordinal)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task ReplaceFieldsAsync(
        string businessType,
        IReadOnlyList<ApproveBusinessField> fields,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.Set<ApproveBusinessField>()
            .Where(x => x.BusinessType == businessType)
            .ToListAsync(cancellationToken);
        if (existing.Count > 0)
        {
            db.Set<ApproveBusinessField>().RemoveRange(existing);
            await db.SaveChangesAsync(cancellationToken);
        }

        if (fields.Count > 0)
        {
            db.Set<ApproveBusinessField>().AddRange(fields);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<string?> TryReadLayoutAsync(long id, CancellationToken cancellationToken)
    {
        try
        {
            return await WithConnectionAsync(async command =>
            {
                command.CommandText = "SELECT `layoutjson` FROM `appr_process_def` WHERE `id` = @id";
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@id";
                parameter.Value = id;
                command.Parameters.Add(parameter);
                var value = await command.ExecuteScalarAsync(cancellationToken);
                return value is string text ? text : null;
            }, cancellationToken);
        }
        catch (DbException)
        {
            return null;
        }
    }

    private async Task TryWriteLayoutAsync(long id, string? layoutJson, CancellationToken cancellationToken)
    {
        try
        {
            await WithConnectionAsync(async command =>
            {
                command.CommandText = "UPDATE `appr_process_def` SET `layoutjson` = @layout WHERE `id` = @id";
                var layout = command.CreateParameter();
                layout.ParameterName = "@layout";
                layout.Value = (object?)layoutJson ?? DBNull.Value;
                command.Parameters.Add(layout);
                var key = command.CreateParameter();
                key.ParameterName = "@id";
                key.Value = id;
                command.Parameters.Add(key);
                await command.ExecuteNonQueryAsync(cancellationToken);
                return 0;
            }, cancellationToken);
        }
        catch (DbException)
        {
            // 旧库还没有 layoutjson 时，流程照常保存，重新打开后自动排列。
        }
    }

    private async Task<T> WithConnectionAsync<T>(Func<DbCommand, Task<T>> action, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            var transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            if (transaction is not null)
            {
                command.Transaction = transaction;
            }

            return await action(command);
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private void AddChildren(ApproveProcessDesignRecord record)
    {
        if (record.Nodes.Count > 0)
        {
            db.Set<ApproveProcessNode>().AddRange(record.Nodes);
        }

        if (record.Transitions.Count > 0)
        {
            db.Set<ApproveProcessTransition>().AddRange(record.Transitions);
        }

        if (record.Groups.Count > 0)
        {
            db.Set<ApproveConditionGroup>().AddRange(record.Groups);
        }

        if (record.Items.Count > 0)
        {
            db.Set<ApproveConditionItem>().AddRange(record.Items);
        }

        if (record.Resolvers.Count > 0)
        {
            db.Set<ApproveResolver>().AddRange(record.Resolvers);
        }
    }

    private async Task DeleteChildrenAsync(long processDefId, CancellationToken cancellationToken)
    {
        var transitionIds = await db.Set<ApproveProcessTransition>()
            .Where(x => x.ProcessDefId == processDefId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (transitionIds.Count > 0)
        {
            var groupIds = await db.Set<ApproveConditionGroup>()
                .Where(x => x.OwnerType == ApproveConditionOwner.Transition && transitionIds.Contains(x.OwnerId))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            if (groupIds.Count > 0)
            {
                var items = await db.Set<ApproveConditionItem>()
                    .Where(x => groupIds.Contains(x.GroupId))
                    .ToListAsync(cancellationToken);
                db.Set<ApproveConditionItem>().RemoveRange(items);

                var groups = await db.Set<ApproveConditionGroup>()
                    .Where(x => groupIds.Contains(x.Id))
                    .ToListAsync(cancellationToken);
                db.Set<ApproveConditionGroup>().RemoveRange(groups);
            }

            var transitions = await db.Set<ApproveProcessTransition>()
                .Where(x => x.ProcessDefId == processDefId)
                .ToListAsync(cancellationToken);
            db.Set<ApproveProcessTransition>().RemoveRange(transitions);
        }

        var nodeIds = await db.Set<ApproveProcessNode>()
            .Where(x => x.ProcessDefId == processDefId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        if (nodeIds.Count > 0)
        {
            var resolvers = await db.Set<ApproveResolver>()
                .Where(x => nodeIds.Contains(x.ProcessNodeId))
                .ToListAsync(cancellationToken);
            db.Set<ApproveResolver>().RemoveRange(resolvers);

            var nodes = await db.Set<ApproveProcessNode>()
                .Where(x => x.ProcessDefId == processDefId)
                .ToListAsync(cancellationToken);
            db.Set<ApproveProcessNode>().RemoveRange(nodes);
        }
    }
}
