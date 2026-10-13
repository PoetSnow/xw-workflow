using Microsoft.EntityFrameworkCore;

namespace XW.Infra.Repository.EfCore.SqlServer;

public class SqlServerDbContext(DbContextOptions options, IEntityInfo entityInfo, Operater operater) : AdncDbContext(options, entityInfo, operater)
{ }
