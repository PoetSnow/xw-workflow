using Microsoft.EntityFrameworkCore;
using XW.Approve.Domain.Aggregates;
using XW.Approve.Domain.Entities;
using XW.Infra.Repository.EfCore;
using XW.Shared.Domain.Entities;
using System.Reflection;

namespace XW.Approve.Infrastructure.Persistence;

public class EntityInfo : AbstractEntityInfo
{
    protected override List<Assembly> GetEntityAssemblies() => [typeof(ApproveInstance).Assembly, GetType().Assembly];

    /// <summary>配置实体已不再继承 EfEntity，按领域基类登记，注释和软删过滤仍生效。</summary>
    protected override List<Type> GetEntityTypes(IEnumerable<Assembly> assemblies)
    {
        return assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.FullName != null
                && typeof(DomainEntity).IsAssignableFrom(type)
                && !type.IsAbstract)
            .ToList();
    }

    protected override void SetTableName(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApproveInstance>(entity =>
        {
            entity.ToTable("appr_instance");
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.HasMany(x => x.Nodes).WithOne().HasForeignKey(x => x.InstanceId);
            entity.HasMany(x => x.Tasks).WithOne().HasForeignKey(x => x.InstanceId);
            entity.HasMany(x => x.Histories).WithOne().HasForeignKey(x => x.InstanceId);
        });
        modelBuilder.Entity<ApproveHistory>(entity =>
        {
            entity.ToTable("appr_history");
            entity.Property(x => x.Id).ValueGeneratedNever();
        });
        modelBuilder.Entity<ApproveInstanceNode>(entity =>
        {
            entity.ToTable("appr_instance_node");
            entity.Property(x => x.Id).ValueGeneratedNever();
        });
        modelBuilder.Entity<ApproveTask>(entity =>
        {
            entity.ToTable("appr_task");
            entity.Property(x => x.Id).ValueGeneratedNever();
        });
        modelBuilder.Entity<ApproveTaskFile>(entity =>
        {
            entity.ToTable("appr_task_file");
            entity.Property(x => x.Id).ValueGeneratedNever();
        });
        modelBuilder.Entity<ApproveBusinessField>().ToTable("appr_business_field");
        modelBuilder.Entity<ApproveConditionGroup>().ToTable("appr_condition_group");
        modelBuilder.Entity<ApproveConditionItem>().ToTable("appr_condition_item");
        modelBuilder.Entity<ApproveProcessBinding>().ToTable("appr_process_binding");
        modelBuilder.Entity<ApproveProcessDef>().ToTable("appr_process_def");
        modelBuilder.Entity<ApproveProcessNode>().ToTable("appr_process_node");
        modelBuilder.Entity<ApproveProcessTransition>().ToTable("appr_process_transition");
        modelBuilder.Entity<ApproveResolver>().ToTable("appr_resolver");
    }
}
