using Microsoft.EntityFrameworkCore;
using XW.Infra.Repository;
using XW.Infra.Repository.EfCore.MySql;

namespace XW.Approve.Infrastructure.Persistence
{
    /// <summary>
    /// 审批模块的DbContext，属于同一个MySQL数据库，但EF模型是隔离的。
    /// </summary>
    /// <param name="options"></param>
    /// <param name="entityInfo"></param>
    /// <param name="operater"></param>
    public class ApproveDbContext(DbContextOptions<ApproveDbContext> options, EntityInfo entityInfo, Operater operater)
    : MySqlDbContext(options, entityInfo, operater)
    {
    }
}
