using XW.Infra.Helper.Internal;
using XW.Infra.Helper.Internal.Encrypt;

namespace XW.Infra.Helper;

public static class InfraHelper
{
    private static readonly EncryptProivder _encypt = new();
    private static readonly HashConsistentGenerater _hashConsistentGenerater = new();
    private static readonly Internal.HttpContextAccessor _accessor = new();

    static InfraHelper()
    {
    }

    public static EncryptProivder Encrypt => _encypt;

    public static HashConsistentGenerater HashConsistent => _hashConsistentGenerater;

    public static Internal.HttpContextAccessor HttpContextAccessor => _accessor;
}
