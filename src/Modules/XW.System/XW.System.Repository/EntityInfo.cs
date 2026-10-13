using Microsoft.EntityFrameworkCore;
using XW.Infra.Repository.EfCore;
using XW.Shared.Repository.EfCoreEntities;
using XW.System.Repository.Entities;
using System.Reflection;

namespace XW.System.Repository;

public class EntityInfo : AbstractEntityInfo
{
    protected override List<Assembly> GetEntityAssemblies() => [GetType().Assembly, typeof(EventTracker).Assembly];

    protected override void SetTableName(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EventTracker>().ToTable("sys_eventtracker");
        modelBuilder.Entity<Organization>().ToTable("sys_organization");
        modelBuilder.Entity<Menu>().ToTable("sys_menu");
        modelBuilder.Entity<Role>().ToTable("sys_role");
        modelBuilder.Entity<RoleMenuRelation>().ToTable("sys_role_menu_relation");
        modelBuilder.Entity<RoleUserRelation>().ToTable("sys_role_user_relation");
        modelBuilder.Entity<User>().ToTable("sys_user");
        modelBuilder.Entity<Position>().ToTable("sys_position");
        modelBuilder.Entity<UserAppointment>().ToTable("sys_user_appointment");
        modelBuilder.Entity<SysConfig>().ToTable("sys_config");
        modelBuilder.Entity<Dict>().ToTable("sys_dictionary");
        modelBuilder.Entity<DictData>().ToTable("sys_dictionary_data");
    }
}
