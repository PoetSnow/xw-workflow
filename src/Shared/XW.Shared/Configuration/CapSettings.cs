namespace XW.Shared.Configuration;

/// <summary>
/// CAP 事件总线配置（单体应用，从 appsettings 的 Cap 节点绑定）。
/// </summary>
public class CapSettings
{
    /// <summary>
    /// 应用短名，用于消费者组：cap.{AppName}.{env}。
    /// </summary>
    public string AppName { get; set; } = "xw";

    /// <summary>
    /// CAP 消息版本号。
    /// </summary>
    public string Version { get; set; } = "v1";

    /// <summary>
    /// RabbitMQ 连接标识；未配置时使用 <see cref="AppName"/>。
    /// </summary>
    public string? ClientProvidedName { get; set; }
}
