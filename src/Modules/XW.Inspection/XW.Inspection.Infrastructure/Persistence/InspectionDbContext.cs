using Microsoft.EntityFrameworkCore;
using XW.Infra.Repository;
using XW.Infra.Repository.EfCore.MySql;

namespace XW.Inspection.Infrastructure.Persistence;

/// <summary>检测项目模块的库。和宿主共用 MySQL，模型互相隔离。</summary>
public class InspectionDbContext(DbContextOptions<InspectionDbContext> options, EntityInfo entityInfo, Operater operater)
    : MySqlDbContext(options, entityInfo, operater)
{
    /// <summary>法规目录。</summary>
    public DbSet<Domain.InspectionRegulation> Regulations => Set<Domain.InspectionRegulation>();

    /// <summary>项目。</summary>
    public DbSet<Domain.InspectionProject> Projects => Set<Domain.InspectionProject>();
}
