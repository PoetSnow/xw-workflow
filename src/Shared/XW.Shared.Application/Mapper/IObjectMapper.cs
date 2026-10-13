using XW.Infra.Repository;

namespace XW.Shared.Application.Mapper;

/// <summary>
/// 对象映射抽象（AutoMapper 封装）。
/// </summary>
public interface IObjectMapper
{
    TDestination Map<TDestination>(object source);

    TDestination Map<TSource, TDestination>(TSource source)
        where TSource : class
        where TDestination : class;

    TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
        where TSource : class
        where TDestination : class;

    TDestination Map<TDestination>(object source, long id)
        where TDestination : Entity;
}
