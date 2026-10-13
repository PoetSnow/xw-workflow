using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Linq.Expressions;
using System.Reflection;

namespace XW.Infra.Repository.EfCore;

public abstract class AbstractEntityInfo : IEntityInfo
{
    public virtual void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder, nameof(modelBuilder));

        var assemblies = GetEntityAssemblies();
        foreach (var assembly in assemblies)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);
        }

        var entityTypes = GetEntityTypes(assemblies);
        foreach (var entityType in entityTypes)
        {
            modelBuilder.Entity(entityType, entityTypeBuilder =>
            {
                var metadata = entityTypeBuilder.Metadata;
                var tableComment = metadata.ClrType.GetSummary();
                entityTypeBuilder.ToTable(t => t.HasComment(tableComment));

                var properties = metadata.GetProperties();
                foreach (var property in properties)
                {
                    var propertyName = property.Name;
                    var memberComment = metadata.ClrType?.GetMember(propertyName)?.FirstOrDefault()?.GetSummary();
                    entityTypeBuilder.Property(propertyName).HasComment(memberComment);
                }

                ConfigureSoftDelete(entityTypeBuilder, entityType);
            });
        }

        SetTableName(modelBuilder);
    }

    /// <summary>
    /// 为实现了 <see cref="ISoftDelete"/> 的实体注册全局查询过滤器（排除已软删记录）。
    /// </summary>
    private static void ConfigureSoftDelete(EntityTypeBuilder entityTypeBuilder, Type entityType)
    {
        if (!typeof(ISoftDelete).IsAssignableFrom(entityType))
        {
            return;
        }

        if (entityTypeBuilder.Metadata.GetQueryFilter() is not null)
        {
            return;
        }

        const string fieldName = nameof(ISoftDelete.IsDeleted);
        // 不使用 HasDefaultValue：EF Core 8 + Pomelo 会对默认值列生成 INSERT ... RETURNING，
        // 在 MySQL 5.7 / 未启用 RETURNING 的实例上会报语法错误。IsDeleted 由应用层赋值即可。
        entityTypeBuilder.Property(fieldName).HasColumnOrder(99);

        var parameter = Expression.Parameter(entityType, "e");
        var isDeletedProperty = Expression.Property(
            Expression.Convert(parameter, typeof(ISoftDelete)),
            typeof(ISoftDelete).GetProperty(fieldName)!);
        var filter = Expression.Lambda(
            Expression.Not(isDeletedProperty),
            parameter);
        entityTypeBuilder.HasQueryFilter(filter);
    }

    protected abstract List<Assembly> GetEntityAssemblies();

    protected virtual void SetTableName(ModelBuilder modelBuilder)
    {
    }

    protected virtual List<Type> GetEntityTypes(IEnumerable<Assembly> assemblies)
    {
        var typeList = assemblies?.SelectMany(assembly => assembly.GetTypes()
                                                 .Where(m => m.FullName != null
                                                 && typeof(EfEntity).IsAssignableFrom(m)
                                                 && !m.IsAbstract));

        return typeList?.ToList() ?? [];
    }
}
