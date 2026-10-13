using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XW.Infra.Repository;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace XW.Shared.Application.Registrar;

public abstract partial class AbstractApplicationDependencyRegistrar
{
    /// <summary>
    /// 注册 EF Core 上下文与仓储（单 DbContext 微服务模式 <c>AddAdncInfraEfCoreMySql</c>）。
    /// </summary>
    protected virtual void AddEfCoreContext()
    {
        AddOperater(Services);

        var connectionString = Configuration[NodeConsts.Mysql_ConnectionString] ?? throw new ArgumentNullException($"connectionString is null");
        var versionString = Configuration[NodeConsts.Mysql_ServerVersion] ?? "11.7.2";
        var serverTypeString = Configuration[NodeConsts.Mysql_ServerType] ?? $"{nameof(ServerType.MariaDb)}";
        var serverVersion = Enum.TryParse(serverTypeString, out ServerType serverType) ? ServerVersion.Create(new Version(versionString), serverType) : throw new ArgumentException($"serverTypeString is invalid: {serverTypeString}");
        //var migrationsAssemblyName = Configuration.GetValue<string>(NodeConsts.Mysql_MigrationsAssembly)
        //    ?? throw new InvalidDataException($"{NodeConsts.Mysql_MigrationsAssembly} is null");
        var splittingBehavior = QuerySplittingBehavior.SplitQuery;
        Services.AddAdncInfraEfCoreMySql(RepositoryOrDomainLayerAssembly, optionsBuilder =>
         {
             optionsBuilder.UseLowerCaseNamingConvention();
             optionsBuilder.UseMySql(connectionString, serverVersion, mySqlOptions =>
             {
                 mySqlOptions
                 .MinBatchSize(4)
                 //.MigrationsAssembly(migrationsAssemblyName)
                 .UseQuerySplittingBehavior(splittingBehavior);
             });
         }, Lifetime);
    }

    /// <summary>
    /// 从当前 HTTP 用户上下文构造 <see cref="Operater"/>（审计字段写入人）。
    /// </summary>
    protected void AddOperater(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        services.Add(new ServiceDescriptor(typeof(Operater), provider =>
        {
            var userContext = provider.GetRequiredService<UserContext>();
            return new Operater
            {
                Id = userContext.Id == 0 ? 1000000000000 : userContext.Id,
                Account = userContext.Account.IsNullOrEmpty() ? "system" : userContext.Account,
                Name = userContext.Name.IsNullOrEmpty() ? "system" : userContext.Name
            };

        }, Lifetime));
    }
}
