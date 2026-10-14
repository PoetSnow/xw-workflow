using XW.Agent.Contracts.Jobs;

namespace XW.Agent.Infrastructure.Jobs;

/// <summary>进程内作业分发。不读取 input 和 output 的内容。</summary>
public sealed class JobDispatcher : IJobDispatcher
{
    private readonly Dictionary<string, IJobHandler> _handlers;

    /// <summary>收集已登记的处理器。没有处理器也可以构造。</summary>
    public JobDispatcher(IEnumerable<IJobHandler> handlers)
    {
        _handlers = new Dictionary<string, IJobHandler>(StringComparer.Ordinal);
        foreach (var handler in handlers)
        {
            if (string.IsNullOrWhiteSpace(handler.Kind))
            {
                throw new InvalidOperationException("IJobHandler.Kind 不能为空。");
            }

            if (!_handlers.TryAdd(handler.Kind.Trim(), handler))
            {
                throw new InvalidOperationException($"重复的作业 kind：{handler.Kind}");
            }
        }
    }

    /// <inheritdoc />
    public async Task<JobResult> DispatchAsync(JobRequest request, CancellationToken cancellationToken = default)
    {
        var jobId = request.JobId?.Trim() ?? string.Empty;
        if (jobId.Length == 0)
        {
            return Failed(string.Empty, "jobId is required");
        }

        var kind = request.Kind?.Trim() ?? string.Empty;
        if (kind.Length == 0 || !_handlers.TryGetValue(kind, out var handler))
        {
            return Failed(jobId, "unknown kind");
        }

        try
        {
            var result = await handler.HandleAsync(request, cancellationToken);
            if (result is null)
            {
                return Failed(jobId, "handler returned null");
            }

            return new JobResult
            {
                JobId = string.IsNullOrWhiteSpace(result.JobId) ? jobId : result.JobId,
                Status = result.Status,
                Hits = result.Hits ?? [],
                Output = result.Output,
                Error = result.Error
            };
        }
        catch (Exception ex)
        {
            return Failed(jobId, ex.Message);
        }
    }

    private static JobResult Failed(string jobId, string error) =>
        new()
        {
            JobId = jobId,
            Status = JobResult.Failed,
            Error = error
        };
}
