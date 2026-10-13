using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace XW.Infra.Repository.EfCore.MySql;

/// <summary>
/// MySQL <c>timestamp(6)</c> is surfaced as <see cref="DateTime"/>; domain concurrency tokens are <c>byte[]</c>.
/// </summary>
internal sealed class MySqlTimestampRowVersionConverter : ValueConverter<byte[], DateTime>
{
    public static readonly MySqlTimestampRowVersionConverter Instance = new();

    private MySqlTimestampRowVersionConverter()
        : base(bytes => ToDateTime(bytes), dateTime => ToBytes(dateTime))
    {
    }

    private static DateTime ToDateTime(byte[]? bytes)
    {
        if (bytes is null || bytes.Length < 8)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        return new DateTime(BitConverter.ToInt64(bytes, 0), DateTimeKind.Unspecified);
    }

    private static byte[] ToBytes(DateTime dateTime) => BitConverter.GetBytes(dateTime.Ticks);
}
