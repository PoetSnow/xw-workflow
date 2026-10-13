using XW.Approve.Domain.Exceptions;
using XW.Approve.Domain.Ports;

namespace XW.Approve.Infrastructure.Files;

/// <summary>待办附件落在应用目录。键只含任务 Id 和文件 Id。</summary>
internal sealed class LocalApproveTaskFileStore(IConfiguration configuration) : IApproveTaskFileStore
{
    private readonly string _root = ResolveRoot(configuration);

    /// <inheritdoc />
    public async Task<string> SaveAsync(long taskId, long fileId, Stream content, CancellationToken cancellationToken = default)
    {
        var key = $"{taskId}/{fileId}";
        var path = Map(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(output, cancellationToken);
        return key;
    }

    /// <inheritdoc />
    public Stream OpenRead(string storageKey)
    {
        var path = Map(storageKey);
        if (!File.Exists(path))
        {
            throw new ApprovalDomainException("附件文件不存在");
        }

        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    /// <inheritdoc />
    public void Delete(string storageKey)
    {
        var path = Map(storageKey);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private string Map(string storageKey)
    {
        var relative = (storageKey ?? string.Empty).Replace('\\', '/').Trim('/');
        if (relative.Length == 0 || relative.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new ApprovalDomainException("附件存储键无效");
        }

        var full = Path.GetFullPath(Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar)));
        var root = Path.GetFullPath(_root);
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
        {
            throw new ApprovalDomainException("附件存储键无效");
        }

        return full;
    }

    private static string ResolveRoot(IConfiguration configuration)
    {
        var configured = configuration["Approve:TaskFileRoot"];
        var root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(AppContext.BaseDirectory, "App_Data", "approve-task-files")
            : configured;
        Directory.CreateDirectory(root);
        return root;
    }
}
