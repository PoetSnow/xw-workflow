using Microsoft.EntityFrameworkCore;

namespace XW.Infra.Repository;

public interface IEntityInfo
{
    void OnModelCreating(ModelBuilder modelBuilder);
}
