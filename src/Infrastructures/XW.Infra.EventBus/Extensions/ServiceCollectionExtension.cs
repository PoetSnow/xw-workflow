using System.Reflection;
using XW.Infra.EventBus;
using XW.Infra.EventBus.Cap;
using XW.Infra.EventBus.Cap.Filters;
using XW.Infra.EventBus.RabbitMq;
using DotNetCore.CAP;
using DotNetCore.CAP.Filter;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkyApm.Diagnostics.CAP;
using SkyApm.Utilities.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddAdncInfraCap(this IServiceCollection services, IEnumerable<Type> capSubscribes, Action<CapOptions> setupAction, Action<IServiceCollection>? registrarAction = null, ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentNullException.ThrowIfNull(capSubscribes, nameof(capSubscribes));
        ArgumentNullException.ThrowIfNull(setupAction, nameof(setupAction));

        return AddAdncInfraCap<DefaultCapFilter>(services, capSubscribes, setupAction, registrarAction, serviceLifetime);
    }

    public static IServiceCollection AddAdncInfraCap<TSubscribeFilter>(this IServiceCollection services, IEnumerable<Type> capSubscribes, Action<CapOptions> setupAction, Action<IServiceCollection>? registrarAction = null, ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
    where TSubscribeFilter : class, ISubscribeFilter
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentNullException.ThrowIfNull(capSubscribes, nameof(capSubscribes));
        ArgumentNullException.ThrowIfNull(setupAction, nameof(setupAction));

        foreach (var subScriber in capSubscribes)
        {
            if (!subScriber.IsAssignableTo(typeof(ICapSubscribe)))
            {
                throw new InvalidDataException("invalid data type");
            }

            if (services.Any(d => d.ServiceType == subScriber && d.ImplementationType == subScriber))
            {
                continue;
            }

            services.Add(new ServiceDescriptor(subScriber, subScriber, serviceLifetime));
        }

        if (services.HasRegistered(nameof(AddAdncInfraCap)))
        {
            return services;
        }

        registrarAction?.Invoke(services);

        if (IsEnableSkyApm())
        {
            services.AddSkyApmExtensions().AddCap();
        }

        services
            .AddSingleton<IEventPublisher, CapPublisher>()
            .AddCap(setupAction)
            .AddSubscribeFilter<TSubscribeFilter>();

        return services;
    }

    public static IServiceCollection AddXWInfraRabbitMq(this IServiceCollection services, Assembly? assembly, IConfigurationSection rabitmqSection, string clientProvidedName, Action<IServiceCollection>? registrarAction = null)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentNullException.ThrowIfNull(rabitmqSection, nameof(rabitmqSection));
        ArgumentNullException.ThrowIfNullOrWhiteSpace(clientProvidedName, nameof(clientProvidedName));

        if (services.HasRegistered(nameof(AddXWInfraRabbitMq)))
        {
            return services;
        }

        registrarAction?.Invoke(services);
        services.Configure<RabbitMQOptions>(rabitmqSection);

        if (assembly is not null)
        {
            var serviceType = typeof(BaseRabbitMqConsumer);
            var implTypes = assembly.ExportedTypes.Where(type => type.IsAssignableTo(serviceType) && type.IsNotAbstractClass(true));
            if (implTypes is not null && implTypes.Any())
            {
                var descriptors = implTypes.Select(implType => new ServiceDescriptor(typeof(IHostedService), implType, ServiceLifetime.Singleton));
                services.TryAddEnumerable(descriptors);
            }
        }

        return services
             .AddSingleton<IConnectionManager>(provider =>
             {
                 var options = provider.GetRequiredService<IOptions<RabbitMQOptions>>();
                 var logger = provider.GetRequiredService<ILogger<ConnectionManager>>();
                 var clientName = clientProvidedName.IsNullOrWhiteSpace() ? clientProvidedName : "unknow";
                 return ConnectionManager.GetInstance(options, clientName, logger);
             })
             .AddSingleton<RabbitMqProducer>();
    }

    public static bool IsEnableSkyApm()
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES");
        if (string.IsNullOrWhiteSpace(env))
        {
            return false;
        }
        else
        {
            return env.Contains("SkyAPM.Agent.AspNetCore");
        }
    }
}
