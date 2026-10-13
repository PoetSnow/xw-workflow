using Microsoft.EntityFrameworkCore;
using XW.Infra.Repository;
using XW.Infra.Repository.EfCore.MySql;

namespace XW.System.Repository;

/// <summary>
/// System bounded context DbContext (same MySQL database, isolated EF model).
/// </summary>
public class SystemDbContext(DbContextOptions<SystemDbContext> options, EntityInfo entityInfo, Operater operater)
    : MySqlDbContext(options, entityInfo, operater)
{
}
