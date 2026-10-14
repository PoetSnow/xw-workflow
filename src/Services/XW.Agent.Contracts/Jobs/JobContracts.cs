using System.Text.Json;
using XW.Agent.Contracts.Knowledge;

namespace XW.Agent.Contracts.Jobs;

/// <summary>作业请求。input 由业务包自己定义，底座不解析。</summary>
public sealed class JobRequest
{
    /// <summary>调用方生成的作业 Id，结果原样带回。</summary>
    public string JobId { get; init; } = string.Empty;

    /// <summary>处理器名称。</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>这次可用的资料集编号。不用检索时为空列表。</summary>
    public IReadOnlyList<string> CorpusIds { get; init; } = [];

    /// <summary>业务包自己的 JSON。</summary>
    public JsonElement Input { get; init; }

    /// <summary>结果送回的路由。进程内调用可空。</summary>
    public string? ReplyTo { get; init; }
}

/// <summary>作业结果。output 由业务包自己定义。</summary>
public sealed class JobResult
{
    /// <summary>成功。</summary>
    public const string Succeeded = "succeeded";

    /// <summary>失败。</summary>
    public const string Failed = "failed";

    /// <summary>作业 Id。</summary>
    public required string JobId { get; init; }

    /// <summary>succeeded 或 failed。</summary>
    public required string Status { get; init; }

    /// <summary>这次用到的引用。没有检索时为空。</summary>
    public IReadOnlyList<KnowledgeHit> Hits { get; init; } = [];

    /// <summary>业务包自己的 JSON。</summary>
    public JsonElement? Output { get; init; }

    /// <summary>失败原因。</summary>
    public string? Error { get; init; }
}

/// <summary>一种作业的处理器。由 Host 点名登记。</summary>
public interface IJobHandler
{
    /// <summary>处理器名称，对应请求里的 kind。</summary>
    string Kind { get; }

    /// <summary>处理一封作业。可以调用检索、模型和工具。</summary>
    Task<JobResult> HandleAsync(JobRequest request, CancellationToken cancellationToken = default);
}

/// <summary>按 kind 把作业信封交给已登记的处理器。</summary>
public interface IJobDispatcher
{
    /// <summary>分发。不解析 input。未知 kind 返回 failed。</summary>
    Task<JobResult> DispatchAsync(JobRequest request, CancellationToken cancellationToken = default);
}
