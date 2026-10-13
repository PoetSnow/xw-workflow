var builder = DistributedApplication.CreateBuilder(args);

// 业务下游：XW 单体 Host（产品/物料/库存等 Console API）
var hostApi = builder.AddProject<Projects.XW_Host_API>("xw-host")
    .WithExternalHttpEndpoints();

// Enterprise Agent：NL 编排入口；HostApi 指向 Aspire 解析出的 Host 地址
var agent = builder.AddProject<Projects.XW_Agent_Host>("xw-agent")
    .WithExternalHttpEndpoints()
    .WaitFor(hostApi)
    .WithEnvironment("Agent__HostApi__BaseUrl", hostApi.GetEndpoint("http"))
    // Trace 交给 Aspire Dashboard（OTEL_* 由运行时注入）；控制台导出可关
    .WithEnvironment("Telemetry__ConsoleExporter", "false")
    .WithEnvironment("Telemetry__Enabled", "true");

builder.Build().Run();
