using Microsoft.EntityFrameworkCore;

namespace XW.Infra.Repository.EfCore;

public class NullEntityInfo : IEntityInfo
{
    public void OnModelCreating(ModelBuilder modelBuilder)
    {
    }
}
