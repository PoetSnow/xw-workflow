# XW Aspire AppHost

统一本地开发环境（Sprint 4）：一键拉起 **XW.Host.API** + **XW.Agent.Host**，并在 Aspire Dashboard 看 Agent Trace。

## 启动

```bash
dotnet run --project src/Aspire/XW.AppHost --launch-profile http
```

浏览器会打开 Aspire Dashboard。资源：

| 资源 | 说明 |
|------|------|
| `xw-host` | 业务 API（产品/物料/库存） |
| `xw-agent` | Enterprise Agent；`Agent__HostApi__BaseUrl` 自动指向 Host |

## Trace

- Agent 已接 OpenTelemetry；在 Aspire 下自动把 span 打到 Dashboard OTLP  
- ActivitySource：`XW.Agent` / `XW.Agent.Llm`  
- 打一发 `POST /api/agent/chat` 后，在 Dashboard → Traces 看完整链路  

## 说明

- **不改变**生产部署方式；AppHost 仅本地/联调  
- Agent / Host 仍可单独 `dotnet run`  
- 未引入 ServiceDefaults（Agent 已有自有 Telemetry，避免重复）  
