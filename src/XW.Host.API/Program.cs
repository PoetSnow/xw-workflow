using NLog;
using NLog.Web;
using XW.Host.API;
using XW.Shared;

var startAssembly = typeof(Program).Assembly;
var serviceInfo = ServiceInfo.CreateInstance(startAssembly, "XW.System.Repository");

try
{
    LogManager.Setup().LoadConfigurationFromAppSettings();

    var builder = WebApplication.CreateBuilder(args).AddConfiguration(serviceInfo);
    builder.Services.AddAdnc(serviceInfo, builder.Configuration);

    var app = builder.Build();
    app.UseXW();
    app.ChangeThreadPoolSettings()
        .UseRegistrationCenter();

    await app.RunAsync();
}
catch (Exception ex)
{
    Console.WriteLine(ex);
}
finally
{
    LogManager.Shutdown();
}
