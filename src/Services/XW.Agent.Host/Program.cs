using XW.Agent.Application;
using XW.Agent.Host;
using XW.Agent.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddXwAgentInfrastructure(builder.Configuration);
builder.Services.AddSingleton<XW.Agent.Contracts.Jobs.IJobHandler, XW.Agent.Inspection.ListInspectionItemsHandler>();
builder.Services.AddXwAgentApplication(builder.Configuration);
builder.Services.AddXwAgentTelemetry(builder.Configuration, builder.Environment);
builder.Services.AddControllers()
    .AddApplicationPart(typeof(XW.Agent.Application.Controllers.AgentChatController).Assembly);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "XW Enterprise Agent", Version = "v1" });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "xw-agent",
    layers = new[]
    {
        "Identity", "Context", "Runtime", "Capability", "Tooling",
        "Policy", "Integration", "Knowledge", "Audit", "Telemetry"
    },
    downstream = "Integration adapters only (host/external)",
    telemetry = "OpenTelemetry ActivitySource=XW.Agent",
    note = "No Brain/ERP DB"
}));

app.MapControllers();

await app.RunAsync();
