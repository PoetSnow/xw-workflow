using XW.Infra.Core.Guard;
using XW.Shared.Application.Extensions;
using DotNetCore.CAP;
using DotNetCore.CAP.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace XW.Shared.Application.Registrar;

public abstract partial class AbstractApplicationDependencyRegistrar
{
    /// <summary>
    /// 注册 CAP 事件总线与最终一致性（分布式事务）。
    /// </summary>
    /// <param name="subscribers">CAP 订阅者类型列表。</param>
    /// <param name="failedThresholdCallback">重试达到上限时的回调（可选）。</param>
    protected virtual void AddCapEventBus(IEnumerable<Type> subscribers, Action<FailedInfo>? failedThresholdCallback = null)
    {
        ArgumentNullException.ThrowIfNull(subscribers, nameof(subscribers));
        Checker.Argument.ThrowIfNullOrCountLEZero(subscribers, nameof(subscribers));

        var connectionString = Configuration.GetValue<string>(NodeConsts.Mysql_ConnectionString) ?? throw new InvalidDataException("MySql ConnectionString is null");
        var rabbitMQOptions = Configuration.GetRequiredSection(NodeConsts.RabbitMq).Get<RabbitMQOptions>() ?? throw new InvalidDataException(nameof(RabbitMQOptions));
        var clientProvidedName = ServiceInfo.Id;
        var version = ServiceInfo.Version;
        var groupName = $"cap.{ServiceInfo.ShortName}.{this.GetEnvShortName()}";
        Services.AddAdncInfraCap(subscribers, capOptions =>
        {
            SetCapBasicInfo(capOptions, version, groupName, failedThresholdCallback);
            SetCapRabbitMQInfo(capOptions, rabbitMQOptions, clientProvidedName);
            SetCapMySqlInfo(capOptions, connectionString);
        }, null, Lifetime);
    }

    protected void SetCapRabbitMQInfo(CapOptions capOptions, RabbitMQOptions rabbitMQOptions, string clientProvidedName)
    {
        capOptions.UseRabbitMQ(mqOptions =>
        {
            mqOptions.HostName = rabbitMQOptions.HostName;
            mqOptions.VirtualHost = rabbitMQOptions.VirtualHost;
            mqOptions.Port = rabbitMQOptions.Port;
            mqOptions.UserName = rabbitMQOptions.UserName;
            mqOptions.Password = rabbitMQOptions.Password;
            mqOptions.ConnectionFactoryOptions = (facotry) =>
            {
                facotry.ClientProvidedName = clientProvidedName;
            };
        });
    }

    protected void SetCapMySqlInfo(CapOptions capOptions, string connectionString)
    {
        capOptions.UseMySql(config =>
        {
            config.ConnectionString = connectionString;
            config.TableNamePrefix = "cap";
        });
    }

    protected void SetCapBasicInfo(CapOptions capOptions, string version, string groupName, Action<FailedInfo>? failedThresholdCallback = null)
    {
        capOptions.Version = version;
        // 默认：cap.queue.{程序集名}，对应 RabbitMQ 队列名
        capOptions.DefaultGroupName = groupName;
        // 默认 60 秒重试间隔；发布/消费失败后约 4 分钟才开始轮询重试（避免状态延迟）
        // 前 3 次立即重试，之后按 FailedRetryInterval 生效
        capOptions.FailedRetryInterval = 60;
        // 默认最大重试 50 次，达到后不再重试
        capOptions.FailedRetryCount = 50;
        // 达到最大重试次数时回调，可用于告警、人工介入等
        capOptions.FailedThresholdCallback = failedThresholdCallback;
        // 成功消息默认保留 24 小时，过期后从持久化存储删除
        capOptions.SucceedMessageExpiredAfter = 24 * 3600;
        // 消费者并行线程数；大于 1 时不保证消息顺序
        capOptions.ConsumerThreadCount = 1;
    }
}
