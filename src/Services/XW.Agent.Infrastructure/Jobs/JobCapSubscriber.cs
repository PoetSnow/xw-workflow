using DotNetCore.CAP;
using XW.Agent.Contracts.Jobs;

namespace XW.Agent.Infrastructure.Jobs;

/// <summary>CAP 运输开关。关闭时不注册订阅，进程内调用不受影响。</summary>
public sealed class JobTransportOptions
{
    /// <summary>配置节名。</summary>
    public const string SectionName = "Agent:Jobs";

    /// <summary>入站主题。调用方把作业发到这里。</summary>
    public const string InboundTopic = "xw.agent.jobs";

    /// <summary>为 true 时订阅入站主题，并按 replyTo 回发结果。</summary>
    public bool CapEnabled { get; set; }

    /// <summary>RabbitMQ 主机。</summary>
    public string RabbitHost { get; set; } = "localhost";

    /// <summary>RabbitMQ 端口。</summary>
    public int RabbitPort { get; set; } = 5672;

    /// <summary>RabbitMQ 用户。</summary>
    public string RabbitUserName { get; set; } = "guest";

    /// <summary>RabbitMQ 密码。</summary>
    public string RabbitPassword { get; set; } = "guest";

    /// <summary>CAP 消息表使用的 PostgreSQL 连接串。空则用知识库连接串。</summary>
    public string? StorageConnectionString { get; set; }
}

/// <summary>收到作业信封后调用分发器，再按 replyTo 把结果发出去。</summary>
public sealed class JobCapSubscriber(IJobDispatcher dispatcher, ICapPublisher publisher) : ICapSubscribe
{
    /// <summary>处理一封入站作业。</summary>
    [CapSubscribe(JobTransportOptions.InboundTopic)]
    public async Task OnJob(JobRequest request, CancellationToken cancellationToken)
    {
        var result = await dispatcher.DispatchAsync(request, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.ReplyTo))
        {
            return;
        }

        await publisher.PublishAsync(request.ReplyTo, result);
    }
}
