using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using XW.Approve.Application.Contracts.Dtos;
using XW.Approve.Application.Contracts.Interfaces;
using XW.Approve.Domain.Constants;
using XW.Approve.Domain.Entities;
using XW.Approve.Domain.Exceptions;
using XW.Approve.Domain.Models;
using XW.Approve.Domain.Repositories;
using XW.Approve.Domain.Rules;

namespace XW.Approve.Application.Services;

/// <summary>把设计器提交的整张图写成配置表。流转规则仍在 Domain，这里只做校验和落库。</summary>
public sealed class ApproveProcessDesignService(IApproveProcessDesignRepository repository) : AbstractAppService, IApproveProcessDesignService
{
    private static readonly Regex KeyPattern = new("^[A-Za-z][A-Za-z0-9_]{0,63}$", RegexOptions.CultureInvariant);

    private static readonly HashSet<string> Modes = new(StringComparer.OrdinalIgnoreCase)
    {
        ApproveMode.Single, ApproveMode.OrSign, ApproveMode.AndSign, ApproveMode.Vote,
    };

    private static readonly HashSet<string> ResolverTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ApproveResolverType.PositionFromDocDept,
        ApproveResolverType.PositionInFixedDept,
        ApproveResolverType.User,
        ApproveResolverType.Submitter,
    };

    private static readonly HashSet<string> Operators = new(StringComparer.OrdinalIgnoreCase)
    {
        ApproveOperator.Equal, ApproveOperator.NotEqual, ApproveOperator.GreaterThan, ApproveOperator.GreaterThanOrEqual,
        ApproveOperator.LessThan, ApproveOperator.LessThanOrEqual, ApproveOperator.In, ApproveOperator.NotIn, ApproveOperator.Contains,
    };

    private static readonly HashSet<string> ValueTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ApproveValueType.Const, ApproveValueType.Field, ApproveValueType.Initiator, ApproveValueType.InitiatorDept,
    };

    private static readonly HashSet<string> FieldTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ApproveFieldType.Number, ApproveFieldType.String, ApproveFieldType.Enum, ApproveFieldType.User, ApproveFieldType.Department,
    };

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApproveProcessListItemDto>> ListAsync()
    {
        var list = await repository.ListAsync();
        return list.Select(x => new ApproveProcessListItemDto
        {
            Id = x.Id,
            ProcessKey = x.ProcessKey,
            ProcessName = x.ProcessName,
            BusinessType = x.BusinessType,
            Version = x.Version,
            Status = x.Status,
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<ApproveProcessDesignDto?> GetAsync(long id)
    {
        var record = await repository.GetAsync(id);
        return record is null ? null : ToDto(record);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<IdDto>> CreateAsync(ApproveProcessCreateDto input)
    {
        input.TrimStringFields();
        try
        {
            var key = input.ProcessKey?.Trim() ?? string.Empty;
            var name = input.ProcessName?.Trim() ?? string.Empty;
            var businessType = input.BusinessType?.Trim() ?? string.Empty;
            var error = ValidateIdentity(key, name, businessType);
            if (error is not null)
            {
                return Problem(HttpStatusCode.BadRequest, error);
            }

            if (await repository.KeyExistsAsync(key))
            {
                return Problem(HttpStatusCode.BadRequest, "流程编码已存在，请换一个编码或从已有版本生成新版本");
            }

            var defId = IdGenerater.GetNextId();
            var startId = IdGenerater.GetNextId();
            var endId = IdGenerater.GetNextId();
            var transitionId = IdGenerater.GetNextId();
            var record = new ApproveProcessDesignRecord
            {
                Definition = new ApproveProcessDef
                {
                    Id = defId,
                    ProcessKey = key,
                    ProcessName = name,
                    BusinessType = businessType,
                    Version = 1,
                    Status = ApproveProcessStatus.Draft,
                },
                Nodes =
                [
                    new ApproveProcessNode
                    {
                        Id = startId,
                        ProcessDefId = defId,
                        NodeKey = "start",
                        NodeName = "开始",
                        NodeType = ApproveNodeType.Start,
                        Ordinal = 0,
                    },
                    new ApproveProcessNode
                    {
                        Id = endId,
                        ProcessDefId = defId,
                        NodeKey = "end",
                        NodeName = "结束",
                        NodeType = ApproveNodeType.End,
                        Ordinal = 1,
                    },
                ],
                Transitions =
                [
                    new ApproveProcessTransition
                    {
                        Id = transitionId,
                        ProcessDefId = defId,
                        FromNodeId = startId,
                        ToNodeId = endId,
                        Name = string.Empty,
                        Priority = 100,
                        IsDefault = true,
                    },
                ],
                Groups = [],
                Items = [],
                Resolvers = [],
                LayoutJson = """{"start":{"x":80,"y":180},"end":{"x":460,"y":180}}""",
            };

            await repository.InsertAsync(record);
            return new IdDto(defId);
        }
        catch (ApprovalDomainException ex)
        {
            return Problem(HttpStatusCode.BadRequest, ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> SaveAsync(long id, ApproveProcessSaveDto input)
    {
        input.TrimStringFields();
        try
        {
            var def = await repository.FindDefAsync(id);
            if (def is null)
            {
                return Problem(HttpStatusCode.NotFound, "流程不存在");
            }

            if (!ApproveToken.Equals(def.Status, ApproveProcessStatus.Draft))
            {
                return Problem(HttpStatusCode.BadRequest, "已发布的流程不能直接修改，请先生成新版本");
            }

            var errors = ValidateGraph(input);
            if (errors.Count > 0)
            {
                return Problem(HttpStatusCode.BadRequest, string.Join("；", errors.Take(8)));
            }

            await repository.ReplaceDraftAsync(BuildRecord(def, input));
            return ServiceResult();
        }
        catch (ApprovalDomainException ex)
        {
            return Problem(HttpStatusCode.BadRequest, ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> PublishAsync(long id)
    {
        try
        {
            var record = await repository.GetAsync(id);
            if (record is null)
            {
                return Problem(HttpStatusCode.NotFound, "流程不存在");
            }

            if (!ApproveToken.Equals(record.Definition.Status, ApproveProcessStatus.Draft))
            {
                return Problem(HttpStatusCode.BadRequest, "只有草稿可以发布");
            }

            var errors = ValidateGraph(ToDto(record));
            if (errors.Count > 0)
            {
                return Problem(HttpStatusCode.BadRequest, string.Join("；", errors.Take(8)));
            }

            await repository.PublishAsync(id);
            return ServiceResult();
        }
        catch (ApprovalDomainException ex)
        {
            return Problem(HttpStatusCode.BadRequest, ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult<IdDto>> NewVersionAsync(long id)
    {
        try
        {
            var source = await repository.GetAsync(id);
            if (source is null)
            {
                return Problem(HttpStatusCode.NotFound, "流程不存在");
            }

            var defId = IdGenerater.GetNextId();
            var version = await repository.NextVersionAsync(source.Definition.ProcessKey);
            var copy = CopyRecord(source, defId, version);
            await repository.InsertAsync(copy);
            return new IdDto(defId);
        }
        catch (ApprovalDomainException ex)
        {
            return Problem(HttpStatusCode.BadRequest, ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeleteAsync(long id)
    {
        var def = await repository.FindDefAsync(id);
        if (def is null)
        {
            return Problem(HttpStatusCode.NotFound, "流程不存在");
        }

        if (!ApproveToken.Equals(def.Status, ApproveProcessStatus.Draft))
        {
            return Problem(HttpStatusCode.BadRequest, "已发布的流程不能删除");
        }

        await repository.DeleteDraftAsync(id);
        return ServiceResult();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApproveBusinessFieldDto>> ListFieldsAsync(string businessType)
    {
        var type = businessType?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(type))
        {
            return [];
        }

        var fields = await repository.ListFieldsAsync(type);
        return fields.Select(x => new ApproveBusinessFieldDto
        {
            FieldKey = x.FieldKey,
            FieldName = x.FieldName,
            FieldType = x.FieldType,
            ValueSource = x.ValueSource,
            Required = x.Required,
            Enabled = x.Enabled,
            Ordinal = x.Ordinal,
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<ServiceResult> SaveFieldsAsync(string businessType, IReadOnlyList<ApproveBusinessFieldDto> fields)
    {
        var type = businessType?.Trim() ?? string.Empty;
        if (!KeyPattern.IsMatch(type) || type.Length > ApproveBusinessField.BusinessTypeMaxLength)
        {
            return Problem(HttpStatusCode.BadRequest, "业务类型须为字母开头，仅含字母、数字和下划线");
        }

        fields ??= [];
        var errors = new List<string>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entities = new List<ApproveBusinessField>();
        for (var i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            field.TrimStringFields();
            var key = field.FieldKey?.Trim() ?? string.Empty;
            var name = field.FieldName?.Trim() ?? string.Empty;
            if (!KeyPattern.IsMatch(key))
            {
                errors.Add($"第 {i + 1} 个字段键不合法");
                continue;
            }

            if (!keys.Add(key))
            {
                errors.Add($"字段 {key} 重复");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add($"字段 {key} 缺少名称");
            }

            var fieldType = string.IsNullOrWhiteSpace(field.FieldType) ? ApproveFieldType.String : field.FieldType.Trim();
            if (!FieldTypes.Contains(fieldType))
            {
                errors.Add($"字段 {key} 的类型不支持");
            }

            var source = string.IsNullOrWhiteSpace(field.ValueSource) ? ApproveValueSource.Form : field.ValueSource.Trim();
            if (!new[] { ApproveValueSource.Form, ApproveValueSource.System, ApproveValueSource.Derived }.Contains(source, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add($"字段 {key} 的来源不支持");
            }

            entities.Add(new ApproveBusinessField
            {
                Id = IdGenerater.GetNextId(),
                BusinessType = type,
                FieldKey = key,
                FieldName = name,
                FieldType = Canonical(FieldTypes, fieldType),
                ValueSource = source,
                Required = field.Required,
                Enabled = field.Enabled,
                Ordinal = i,
            });
        }

        if (errors.Count > 0)
        {
            return Problem(HttpStatusCode.BadRequest, string.Join("；", errors.Take(8)));
        }

        await repository.ReplaceFieldsAsync(type, entities);
        return ServiceResult();
    }

    private static string? ValidateIdentity(string key, string name, string businessType)
    {
        if (!KeyPattern.IsMatch(key) || key.Length > ApproveProcessDef.ProcessKeyMaxLength)
        {
            return "流程编码须为字母开头，仅含字母、数字和下划线";
        }

        if (string.IsNullOrWhiteSpace(name) || name.Length > ApproveProcessDef.ProcessNameMaxLength)
        {
            return "请填写流程名称";
        }

        if (!KeyPattern.IsMatch(businessType) || businessType.Length > ApproveProcessDef.BusinessTypeMaxLength)
        {
            return "业务类型须为字母开头，仅含字母、数字和下划线";
        }

        return null;
    }

    private static List<string> ValidateGraph(ApproveProcessSaveDto input)
    {
        var errors = new List<string>();
        var name = input.ProcessName?.Trim() ?? string.Empty;
        var businessType = input.BusinessType?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add("请填写流程名称");
        }

        if (!KeyPattern.IsMatch(businessType))
        {
            errors.Add("业务类型须为字母开头，仅含字母、数字和下划线");
        }

        var nodes = input.Nodes ?? [];
        var transitions = input.Transitions ?? [];
        if (nodes.Count == 0)
        {
            errors.Add("流程至少要有开始和结束节点");
            return errors;
        }

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes)
        {
            var key = node.NodeKey?.Trim() ?? string.Empty;
            var label = string.IsNullOrWhiteSpace(node.NodeName) ? key : node.NodeName.Trim();
            if (!KeyPattern.IsMatch(key))
            {
                errors.Add($"节点 {label} 的编码不合法");
                continue;
            }

            if (!keys.Add(key))
            {
                errors.Add($"节点编码 {key} 重复");
            }

            if (string.IsNullOrWhiteSpace(node.NodeName))
            {
                errors.Add($"节点 {key} 缺少名称");
            }

            if (ApproveToken.Equals(node.NodeType, ApproveNodeType.Approve))
            {
                ValidateApproveNode(node, label, errors);
            }
            else if (ApproveNodeType.HasForm(node.NodeType) || ApproveToken.Equals(node.NodeType, ApproveNodeType.Notify))
            {
                ValidateCollectiveNode(node, label, errors);
            }
            else if (!ApproveToken.Equals(node.NodeType, ApproveNodeType.Start)
                     && !ApproveToken.Equals(node.NodeType, ApproveNodeType.End))
            {
                errors.Add($"节点 {label} 的类型不支持");
            }
        }

        var startCount = nodes.Count(n => ApproveToken.Equals(n.NodeType, ApproveNodeType.Start));
        if (startCount != 1)
        {
            errors.Add("流程必须有且只有一个开始节点");
        }

        if (!nodes.Any(n => ApproveToken.Equals(n.NodeType, ApproveNodeType.End)))
        {
            errors.Add("流程至少要有一个结束节点");
        }

        var nodeMap = nodes
            .Where(n => KeyPattern.IsMatch(n.NodeKey?.Trim() ?? string.Empty))
            .GroupBy(n => n.NodeKey.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var edgePairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in transitions)
        {
            var from = edge.FromKey?.Trim() ?? string.Empty;
            var to = edge.ToKey?.Trim() ?? string.Empty;
            if (!nodeMap.TryGetValue(from, out var fromNode) || !nodeMap.TryGetValue(to, out var toNode))
            {
                errors.Add("存在指向未知节点的连线");
                continue;
            }

            if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"节点 {fromNode.NodeName} 不能连到自己");
            }

            if (ApproveToken.Equals(fromNode.NodeType, ApproveNodeType.End))
            {
                errors.Add("结束节点不能再往下连");
            }

            if (ApproveToken.Equals(toNode.NodeType, ApproveNodeType.Start))
            {
                errors.Add("不能连回开始节点");
            }

            if (!edgePairs.Add(from + "->" + to))
            {
                errors.Add($"{fromNode.NodeName} 到 {toNode.NodeName} 的连线重复");
            }
        }

        foreach (var node in nodeMap.Values)
        {
            var outs = transitions.Where(t => string.Equals(t.FromKey?.Trim(), node.NodeKey, StringComparison.OrdinalIgnoreCase)).ToList();
            if (ApproveToken.Equals(node.NodeType, ApproveNodeType.End))
            {
                continue;
            }

            if (outs.Count == 0)
            {
                errors.Add($"节点 {node.NodeName} 没有出线");
                continue;
            }

            if (outs.Count == 1)
            {
                continue;
            }

            if (outs.Count(t => t.IsDefault) != 1)
            {
                errors.Add($"节点 {node.NodeName} 有多条出线时，必须指定一条默认线");
            }

            foreach (var edge in outs.Where(t => !t.IsDefault))
            {
                var target = nodeMap.TryGetValue(edge.ToKey?.Trim() ?? string.Empty, out var toNode) ? toNode.NodeName : edge.ToKey;
                if (edge.Conditions is null || edge.Conditions.Count == 0)
                {
                    errors.Add($"到 {target} 的分支没有条件，会挡住其它出线");
                    continue;
                }

                foreach (var condition in edge.Conditions)
                {
                    if (!KeyPattern.IsMatch(condition.FieldKey?.Trim() ?? string.Empty))
                    {
                        errors.Add($"到 {target} 的条件缺少字段");
                    }

                    if (!Operators.Contains(condition.Operator?.Trim() ?? string.Empty))
                    {
                        errors.Add($"到 {target} 的条件运算符不支持");
                    }

                    var valueType = string.IsNullOrWhiteSpace(condition.ValueType) ? ApproveValueType.Const : condition.ValueType.Trim();
                    if (!ValueTypes.Contains(valueType))
                    {
                        errors.Add($"到 {target} 的条件值类型不支持");
                    }
                    else if ((ApproveToken.Equals(valueType, ApproveValueType.Const) || ApproveToken.Equals(valueType, ApproveValueType.Field))
                             && string.IsNullOrWhiteSpace(condition.Value))
                    {
                        errors.Add($"到 {target} 的条件缺少比较值");
                    }
                }
            }
        }

        if (errors.Count > 0 || startCount != 1)
        {
            return errors;
        }

        var start = nodes.First(n => ApproveToken.Equals(n.NodeType, ApproveNodeType.Start));
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        queue.Enqueue(start.NodeKey);
        seen.Add(start.NodeKey);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var edge in transitions.Where(t => string.Equals(t.FromKey?.Trim(), current, StringComparison.OrdinalIgnoreCase)))
            {
                var to = edge.ToKey?.Trim() ?? string.Empty;
                if (seen.Add(to))
                {
                    queue.Enqueue(to);
                }
            }
        }

        foreach (var node in nodeMap.Values.Where(n => !seen.Contains(n.NodeKey)))
        {
            errors.Add($"节点 {node.NodeName} 从开始节点走不到");
        }

        foreach (var node in nodeMap.Values.Where(n => !ApproveToken.Equals(n.NodeType, ApproveNodeType.End)))
        {
            if (!CanReachEnd(node.NodeKey, transitions, nodeMap))
            {
                errors.Add($"节点 {node.NodeName} 走不到结束节点");
            }
        }

        return errors;
    }

    private static void ValidateApproveNode(ApproveDesignNodeDto node, string label, List<string> errors)
    {
        var mode = node.ApproveMode?.Trim() ?? string.Empty;
        if (!Modes.Contains(mode))
        {
            errors.Add($"节点 {label} 未选择审批方式");
        }
        else if (ApproveToken.Equals(mode, ApproveMode.Vote))
        {
            if (!int.TryParse(node.PassRuleValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var percent)
                || percent is < 1 or > 100)
            {
                errors.Add($"节点 {label} 的票决比例须为 1 到 100");
            }
        }

        if (node.Resolvers is null || node.Resolvers.Count == 0)
        {
            errors.Add($"节点 {label} 还没有审批人");
            return;
        }

        RejectSchema(node.FormSchema, label, "审批节点不能配置交付清单", errors);
        foreach (var resolver in node.Resolvers)
        {
            RejectSchema(resolver.FormSchema, label, "审批节点不能配置交付清单", errors);
        }

        ValidateResolvers(node, label, errors);
    }

    private static void ValidateCollectiveNode(ApproveDesignNodeDto node, string label, List<string> errors)
    {
        if (node.Resolvers is null || node.Resolvers.Count == 0)
        {
            errors.Add($"节点 {label} 还没有办理人");
            return;
        }

        ValidateResolvers(node, label, errors);
        if (ApproveNodeType.HasForm(node.NodeType))
        {
            FormSchemaRules.TryCanonicalize(ToSchema(node.FormSchema), label, errors);
            foreach (var resolver in node.Resolvers)
            {
                FormSchemaRules.TryCanonicalize(ToSchema(resolver.FormSchema), $"{label} 的办理人", errors);
            }

            return;
        }

        RejectSchema(node.FormSchema, label, "知会节点不能配置交付清单", errors);
        foreach (var resolver in node.Resolvers)
        {
            RejectSchema(resolver.FormSchema, label, "知会节点不能配置交付清单", errors);
        }
    }

    private static void RejectSchema(ApproveFormSchemaDto? dto, string label, string message, List<string> errors)
    {
        if (!FormSchemaRules.IsBlank(ToSchema(dto)))
        {
            errors.Add($"{label}：{message}");
        }
    }

    private static void ValidateResolvers(ApproveDesignNodeDto node, string label, List<string> errors)
    {
        foreach (var resolver in node.Resolvers)
        {
            var type = resolver.ResolverType?.Trim() ?? string.Empty;
            var value = resolver.ResolverValue?.Trim() ?? string.Empty;
            if (ApproveToken.Equals(type, "Role"))
            {
                errors.Add($"节点 {label} 不能按角色找人");
                continue;
            }

            if (!ResolverTypes.Contains(type))
            {
                errors.Add($"节点 {label} 的找人方式不支持");
                continue;
            }

            if (ApproveToken.Equals(type, ApproveResolverType.Submitter))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"节点 {label} 的找人规则没有填完");
                continue;
            }

            if (ApproveToken.Equals(type, ApproveResolverType.User) && !long.TryParse(value, out _))
            {
                errors.Add($"节点 {label} 的指定用户无效");
            }

            if (ApproveToken.Equals(type, ApproveResolverType.PositionInFixedDept)
                && (resolver.FixedDeptId is null || resolver.FixedDeptId <= 0))
            {
                errors.Add($"节点 {label} 要选择固定部门");
            }
        }
    }

    private static bool CanReachEnd(
        string fromKey,
        IReadOnlyList<ApproveDesignTransitionDto> transitions,
        IReadOnlyDictionary<string, ApproveDesignNodeDto> nodes)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { fromKey };
        var queue = new Queue<string>();
        queue.Enqueue(fromKey);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var edge in transitions.Where(t => string.Equals(t.FromKey?.Trim(), current, StringComparison.OrdinalIgnoreCase)))
            {
                var to = edge.ToKey?.Trim() ?? string.Empty;
                if (!nodes.TryGetValue(to, out var node))
                {
                    continue;
                }

                if (ApproveToken.Equals(node.NodeType, ApproveNodeType.End))
                {
                    return true;
                }

                if (seen.Add(to))
                {
                    queue.Enqueue(to);
                }
            }
        }

        return false;
    }

    private static ApproveProcessDesignRecord BuildRecord(ApproveProcessDef def, ApproveProcessSaveDto input)
    {
        var nodes = new List<ApproveProcessNode>();
        var idByKey = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < input.Nodes.Count; i++)
        {
            var dto = input.Nodes[i];
            var id = IdGenerater.GetNextId();
            var key = dto.NodeKey.Trim();
            idByKey[key] = id;
            var nodeType = CanonicalNodeType(dto.NodeType);
            var isApprove = ApproveToken.Equals(nodeType, ApproveNodeType.Approve);
            nodes.Add(new ApproveProcessNode
            {
                Id = id,
                ProcessDefId = def.Id,
                NodeKey = key,
                NodeName = dto.NodeName.Trim(),
                NodeType = nodeType,
                ApproveMode = isApprove ? Canonical(Modes, dto.ApproveMode!) : null,
                PassRuleType = isApprove ? PassRule(dto) : null,
                PassRuleValue = isApprove && ApproveToken.Equals(dto.ApproveMode, ApproveMode.Vote) ? dto.PassRuleValue?.Trim() : null,
                RejectRuleType = isApprove ? ApproveRejectRule.AnyReject : null,
                FormSchema = ApproveNodeType.HasForm(nodeType) ? WriteSchema(dto.FormSchema, dto.NodeName) : null,
                Ordinal = i,
            });
        }

        var transitions = new List<ApproveProcessTransition>();
        var groups = new List<ApproveConditionGroup>();
        var items = new List<ApproveConditionItem>();
        var resolvers = new List<ApproveResolver>();
        var outs = input.Transitions
            .GroupBy(t => t.FromKey.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var pair in outs)
        {
            var list = pair.Value;
            if (list.Count == 1)
            {
                list[0].IsDefault = true;
                list[0].Conditions = [];
            }
        }

        foreach (var dto in input.Transitions)
        {
            var id = IdGenerater.GetNextId();
            transitions.Add(new ApproveProcessTransition
            {
                Id = id,
                ProcessDefId = def.Id,
                FromNodeId = idByKey[dto.FromKey.Trim()],
                ToNodeId = idByKey[dto.ToKey.Trim()],
                Name = (dto.Name ?? string.Empty).Trim(),
                Priority = dto.Priority,
                IsDefault = dto.IsDefault,
            });

            if (dto.IsDefault || dto.Conditions is null || dto.Conditions.Count == 0)
            {
                continue;
            }

            var groupId = IdGenerater.GetNextId();
            groups.Add(new ApproveConditionGroup
            {
                Id = groupId,
                OwnerType = ApproveConditionOwner.Transition,
                OwnerId = id,
                GroupName = string.IsNullOrWhiteSpace(dto.Name) ? "Root" : dto.Name.Trim(),
                GroupLogic = ApproveToken.Equals(dto.GroupLogic, ApproveGroupLogic.Or) ? ApproveGroupLogic.Or : ApproveGroupLogic.And,
                GroupType = ApproveGroupType.Root,
                Ordinal = 0,
            });

            for (var i = 0; i < dto.Conditions.Count; i++)
            {
                var condition = dto.Conditions[i];
                var valueType = Canonical(ValueTypes, string.IsNullOrWhiteSpace(condition.ValueType) ? ApproveValueType.Const : condition.ValueType);
                items.Add(new ApproveConditionItem
                {
                    Id = IdGenerater.GetNextId(),
                    GroupId = groupId,
                    FieldKey = condition.FieldKey.Trim(),
                    Operator = Canonical(Operators, condition.Operator),
                    Value = ApproveToken.Equals(valueType, ApproveValueType.Initiator) || ApproveToken.Equals(valueType, ApproveValueType.InitiatorDept)
                        ? null
                        : condition.Value?.Trim(),
                    ValueType = valueType,
                    Ordinal = i,
                });
            }
        }

        foreach (var node in input.Nodes.Where(n => ApproveNodeType.IsWork(n.NodeType)))
        {
            for (var i = 0; i < node.Resolvers.Count; i++)
            {
                var resolver = node.Resolvers[i];
                var type = Canonical(ResolverTypes, resolver.ResolverType);
                string? ext = null;
                if (ApproveToken.Equals(type, ApproveResolverType.PositionInFixedDept) && resolver.FixedDeptId is long fixedDept && fixedDept > 0)
                {
                    ext = JsonSerializer.Serialize(new Dictionary<string, long> { ["DeptId"] = fixedDept });
                }

                resolvers.Add(new ApproveResolver
                {
                    Id = IdGenerater.GetNextId(),
                    ProcessNodeId = idByKey[node.NodeKey.Trim()],
                    ResolverType = type,
                    ResolverValue = ApproveToken.Equals(type, ApproveResolverType.Submitter) ? string.Empty : resolver.ResolverValue.Trim(),
                    ResolverExt = ext,
                    FormSchema = ApproveNodeType.HasForm(node.NodeType) ? WriteSchema(resolver.FormSchema, node.NodeName) : null,
                    Ordinal = i,
                });
            }
        }

        return new ApproveProcessDesignRecord
        {
            Definition = new ApproveProcessDef
            {
                Id = def.Id,
                ProcessKey = def.ProcessKey,
                ProcessName = input.ProcessName.Trim(),
                BusinessType = input.BusinessType.Trim(),
                Version = def.Version,
                Status = def.Status,
            },
            Nodes = nodes,
            Transitions = transitions,
            Groups = groups,
            Items = items,
            Resolvers = resolvers,
            LayoutJson = WriteLayout(input.Nodes),
        };
    }

    private static ApproveProcessDesignRecord CopyRecord(ApproveProcessDesignRecord source, long defId, int version)
    {
        var nodeIdMap = source.Nodes.ToDictionary(x => x.Id, _ => IdGenerater.GetNextId());
        var transitionIdMap = source.Transitions.ToDictionary(x => x.Id, _ => IdGenerater.GetNextId());
        var groupIdMap = source.Groups.ToDictionary(x => x.Id, _ => IdGenerater.GetNextId());

        return new ApproveProcessDesignRecord
        {
            Definition = new ApproveProcessDef
            {
                Id = defId,
                ProcessKey = source.Definition.ProcessKey,
                ProcessName = source.Definition.ProcessName,
                BusinessType = source.Definition.BusinessType,
                Version = version,
                Status = ApproveProcessStatus.Draft,
            },
            Nodes = source.Nodes.Select(x => new ApproveProcessNode
            {
                Id = nodeIdMap[x.Id],
                ProcessDefId = defId,
                NodeKey = x.NodeKey,
                NodeName = x.NodeName,
                NodeType = x.NodeType,
                ApproveMode = x.ApproveMode,
                PassRuleType = x.PassRuleType,
                PassRuleValue = x.PassRuleValue,
                RejectRuleType = x.RejectRuleType,
                RejectRuleValue = x.RejectRuleValue,
                FormSchema = x.FormSchema,
                Ordinal = x.Ordinal,
            }).ToList(),
            Transitions = source.Transitions.Select(x => new ApproveProcessTransition
            {
                Id = transitionIdMap[x.Id],
                ProcessDefId = defId,
                FromNodeId = nodeIdMap[x.FromNodeId],
                ToNodeId = nodeIdMap[x.ToNodeId],
                Name = x.Name,
                Priority = x.Priority,
                IsDefault = x.IsDefault,
            }).ToList(),
            Groups = source.Groups.Select(x => new ApproveConditionGroup
            {
                Id = groupIdMap[x.Id],
                OwnerType = x.OwnerType,
                OwnerId = transitionIdMap.TryGetValue(x.OwnerId, out var ownerId) ? ownerId : x.OwnerId,
                ParentGroupId = x.ParentGroupId is long parent && groupIdMap.TryGetValue(parent, out var parentId) ? parentId : null,
                GroupName = x.GroupName,
                GroupLogic = x.GroupLogic,
                GroupType = x.GroupType,
                Ordinal = x.Ordinal,
            }).ToList(),
            Items = source.Items.Select(x => new ApproveConditionItem
            {
                Id = IdGenerater.GetNextId(),
                GroupId = groupIdMap[x.GroupId],
                FieldKey = x.FieldKey,
                Operator = x.Operator,
                Value = x.Value,
                ValueType = x.ValueType,
                Ordinal = x.Ordinal,
            }).ToList(),
            Resolvers = source.Resolvers.Select(x => new ApproveResolver
            {
                Id = IdGenerater.GetNextId(),
                ProcessNodeId = nodeIdMap[x.ProcessNodeId],
                ResolverType = x.ResolverType,
                ResolverValue = x.ResolverValue,
                ResolverExt = x.ResolverExt,
                FormSchema = x.FormSchema,
                Ordinal = x.Ordinal,
            }).ToList(),
            LayoutJson = source.LayoutJson,
        };
    }

    private static ApproveProcessDesignDto ToDto(ApproveProcessDesignRecord record)
    {
        var layout = ReadLayout(record.LayoutJson);
        var keyByNodeId = record.Nodes.ToDictionary(x => x.Id, x => x.NodeKey);
        var dto = new ApproveProcessDesignDto
        {
            Id = record.Definition.Id,
            ProcessKey = record.Definition.ProcessKey,
            ProcessName = record.Definition.ProcessName,
            BusinessType = record.Definition.BusinessType,
            Version = record.Definition.Version,
            Status = record.Definition.Status,
            Nodes = record.Nodes.Select(node =>
            {
                layout.TryGetValue(node.NodeKey, out var point);
                return new ApproveDesignNodeDto
                {
                    NodeKey = node.NodeKey,
                    NodeName = node.NodeName,
                    NodeType = node.NodeType,
                    ApproveMode = node.ApproveMode,
                    PassRuleType = node.PassRuleType,
                    PassRuleValue = node.PassRuleValue,
                    RejectRuleType = node.RejectRuleType,
                    FormSchema = ToSchemaDto(node.FormSchema),
                    X = point.X,
                    Y = point.Y,
                    Resolvers = record.Resolvers
                        .Where(r => r.ProcessNodeId == node.Id)
                        .OrderBy(r => r.Ordinal)
                        .Select(r => new ApproveDesignResolverDto
                        {
                            ResolverType = r.ResolverType,
                            ResolverValue = r.ResolverValue,
                            FixedDeptId = ReadFixedDeptId(r.ResolverExt),
                            FormSchema = ToSchemaDto(r.FormSchema),
                        })
                        .ToList(),
                };
            }).ToList(),
            Transitions = record.Transitions.Select(edge =>
            {
                var groups = record.Groups.Where(g => g.OwnerId == edge.Id).Select(g => g.Id).ToHashSet();
                var root = record.Groups.FirstOrDefault(g => g.OwnerId == edge.Id && g.ParentGroupId is null);
                return new ApproveDesignTransitionDto
                {
                    FromKey = keyByNodeId.GetValueOrDefault(edge.FromNodeId, string.Empty),
                    ToKey = keyByNodeId.GetValueOrDefault(edge.ToNodeId, string.Empty),
                    Name = edge.Name,
                    Priority = edge.Priority,
                    IsDefault = edge.IsDefault,
                    GroupLogic = root?.GroupLogic ?? ApproveGroupLogic.And,
                    Conditions = record.Items
                        .Where(item => groups.Contains(item.GroupId))
                        .OrderBy(item => item.Ordinal)
                        .Select(item => new ApproveDesignConditionDto
                        {
                            FieldKey = item.FieldKey,
                            Operator = item.Operator,
                            Value = item.Value,
                            ValueType = item.ValueType,
                        })
                        .ToList(),
                };
            }).ToList(),
        };
        return dto;
    }

    private static string PassRule(ApproveDesignNodeDto node)
    {
        if (ApproveToken.Equals(node.ApproveMode, ApproveMode.Vote))
        {
            return ApprovePassRule.Percent;
        }

        if (ApproveToken.Equals(node.ApproveMode, ApproveMode.AndSign))
        {
            return ApprovePassRule.All;
        }

        return ApprovePassRule.Any;
    }

    private static string CanonicalNodeType(string value)
    {
        string[] types =
        [
            ApproveNodeType.Start,
            ApproveNodeType.Approve,
            ApproveNodeType.Collaborate,
            ApproveNodeType.Notify,
            ApproveNodeType.Task,
            ApproveNodeType.End,
        ];
        return types.FirstOrDefault(type => ApproveToken.Equals(value, type)) ?? ApproveNodeType.Approve;
    }

    private static string? WriteSchema(ApproveFormSchemaDto? dto, string label)
    {
        var errors = new List<string>();
        var json = FormSchemaRules.TryCanonicalize(ToSchema(dto), label, errors);
        if (errors.Count > 0)
        {
            throw new ApprovalDomainException(string.Join("；", errors.Take(4)));
        }

        return json;
    }

    private static ApproveFormSchema? ToSchema(ApproveFormSchemaDto? dto)
    {
        if (dto is null)
        {
            return null;
        }

        return new ApproveFormSchema
        {
            AllowAttachment = dto.AllowAttachment,
            AttachmentRequired = dto.AttachmentRequired,
            Fields = (dto.Fields ?? []).Select(field => new ApproveFormField
            {
                Key = field.Key ?? string.Empty,
                Name = field.Name ?? string.Empty,
                Type = field.Type ?? string.Empty,
                Required = field.Required,
                Options = field.Options ?? [],
            }).ToList(),
        };
    }

    private static ApproveFormSchemaDto? ToSchemaDto(string? json)
    {
        var schema = FormSchemaRules.TryRead(json);
        if (FormSchemaRules.IsBlank(schema) || schema is null)
        {
            return null;
        }

        return new ApproveFormSchemaDto
        {
            AllowAttachment = schema.AllowAttachment,
            AttachmentRequired = schema.AttachmentRequired,
            Fields = schema.Fields.Select(field => new ApproveFormFieldDto
            {
                Key = field.Key,
                Name = field.Name,
                Type = field.Type,
                Required = field.Required,
                Options = field.Options,
            }).ToList(),
        };
    }

    private static string Canonical(HashSet<string> allowed, string value)
    {
        var found = allowed.FirstOrDefault(x => ApproveToken.Equals(x, value));
        return found ?? value.Trim();
    }

    private static string WriteLayout(IEnumerable<ApproveDesignNodeDto> nodes)
    {
        var map = new Dictionary<string, Dictionary<string, double>>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            map[node.NodeKey.Trim()] = new Dictionary<string, double>
            {
                ["x"] = node.X,
                ["y"] = node.Y,
            };
        }

        return JsonSerializer.Serialize(map);
    }

    private static Dictionary<string, (double X, double Y)> ReadLayout(string? json)
    {
        var map = new Dictionary<string, (double X, double Y)>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
        {
            return map;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var x = prop.Value.TryGetProperty("x", out var xv) ? xv.GetDouble() : 0;
                var y = prop.Value.TryGetProperty("y", out var yv) ? yv.GetDouble() : 0;
                map[prop.Name] = (x, y);
            }
        }
        catch (JsonException)
        {
            return map;
        }

        return map;
    }

    private static long? ReadFixedDeptId(string? resolverExt)
    {
        if (string.IsNullOrWhiteSpace(resolverExt))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(resolverExt);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (!ApproveToken.Equals(prop.Name, "DeptId"))
                {
                    continue;
                }

                if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt64(out var number) && number > 0)
                {
                    return number;
                }

                if (prop.Value.ValueKind == JsonValueKind.String
                    && long.TryParse(prop.Value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                    && parsed > 0)
                {
                    return parsed;
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }
}
