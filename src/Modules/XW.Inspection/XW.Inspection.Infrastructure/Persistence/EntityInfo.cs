using System.Reflection;
using Microsoft.EntityFrameworkCore;
using XW.Infra.Repository.EfCore;
using XW.Inspection.Domain;
using XW.Shared.Domain.Entities;

namespace XW.Inspection.Infrastructure.Persistence;

/// <summary>检测项目表名。</summary>
public class EntityInfo : AbstractEntityInfo
{
    /// <inheritdoc />
    protected override List<Assembly> GetEntityAssemblies() => [typeof(InspectionRegulation).Assembly];

    /// <inheritdoc />
    protected override List<Type> GetEntityTypes(IEnumerable<Assembly> assemblies)
    {
        return assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.FullName != null
                && typeof(DomainEntity).IsAssignableFrom(type)
                && !type.IsAbstract)
            .ToList();
    }

    /// <inheritdoc />
    protected override void SetTableName(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InspectionRegulation>(entity =>
        {
            entity.ToTable("insp_regulation");
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.CorpusId).HasMaxLength(128);
            entity.Property(x => x.Name).HasMaxLength(256);
            entity.Property(x => x.StandardNo).HasMaxLength(128);
            entity.Property(x => x.Status).HasMaxLength(16);
            entity.HasIndex(x => x.StandardNo).IsUnique();
            entity.HasMany(x => x.Industries).WithOne().HasForeignKey(x => x.RegulationId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.ProjectTypes).WithOne().HasForeignKey(x => x.RegulationId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<InspectionRegulationIndustry>(entity =>
        {
            entity.ToTable("insp_regulation_industry");
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Industry).HasMaxLength(64);
        });
        modelBuilder.Entity<InspectionRegulationProjectType>(entity =>
        {
            entity.ToTable("insp_regulation_project_type");
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.ProjectType).HasMaxLength(64);
        });
        modelBuilder.Entity<InspectionProject>(entity =>
        {
            entity.ToTable("insp_project");
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Industry).HasMaxLength(64);
            entity.Property(x => x.ProjectType).HasMaxLength(64);
            entity.Property(x => x.BuildKind).HasMaxLength(32);
            entity.HasMany(x => x.Regulations).WithOne().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<InspectionProjectRegulation>(entity =>
        {
            entity.ToTable("insp_project_regulation");
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.CorpusId).HasMaxLength(128);
            entity.Property(x => x.Name).HasMaxLength(256);
            entity.Property(x => x.Source).HasMaxLength(16);
        });
        modelBuilder.Entity<InspectionProjectItem>(entity =>
        {
            entity.ToTable("insp_project_item");
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Name).HasMaxLength(256);
            entity.Property(x => x.Requirement).HasMaxLength(2000);
            entity.Property(x => x.Snippet).HasMaxLength(2000);
            entity.Property(x => x.Source).HasMaxLength(16);
        });
    }
}
