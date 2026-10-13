using Microsoft.EntityFrameworkCore;
using XW.Approve.Domain.Aggregates;
using XW.Approve.Domain.Entities;
using XW.Infra.Repository.EfCore;
using System.Reflection;

namespace XW.Approve.Infrastructure.Persistence;

public class EntityInfo : AbstractEntityInfo
{
    protected override List<Assembly> GetEntityAssemblies() => [typeof(ApproveInstance).Assembly, GetType().Assembly];

    protected override void SetTableName(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApproveInstance>(entity =>
        {
            entity.ToTable("appr_instance");
            entity.Ignore(x => x.RowVersion);
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
