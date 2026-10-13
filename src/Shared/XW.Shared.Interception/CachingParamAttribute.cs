namespace XW.Shared.Interception;

/// <summary>标记参与缓存键生成的参数。</summary>
[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
public class CachingParamAttribute : Attribute
{
    /// <summary>标记参与缓存键生成的参数。</summary>
    public CachingParamAttribute()
    {
    }
}
