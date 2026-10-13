using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using XW.Infra.Repository;

namespace XW.Infra.Repository.EfCore.MySql;

internal static class MySqlRowVersionMapping
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(IConcurrency).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var property = entityType.FindProperty(nameof(IConcurrency.RowVersion));
            if (property is null || property.ClrType != typeof(byte[]))
            {
                continue;
            }

            property.SetColumnType("timestamp(6)");
            property.SetValueConverter(MySqlTimestampRowVersionConverter.Instance);
            property.IsConcurrencyToken = true;
            property.ValueGenerated = ValueGenerated.OnAddOrUpdate;
            property.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
            property.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
        }
    }
}
