using XW.System.Application.Contracts.Dtos.Position;

namespace XW.System.Application.Contracts.Interfaces;

/// <summary>岗位目录。</summary>
public interface IPositionService : IAppService
{
    /// <summary>创建岗位。</summary>
    [OperateLog(LogName = "Create position")]
    Task<ServiceResult<IdDto>> CreateAsync(PositionCreationDto input);

    /// <summary>更新岗位。</summary>
    [OperateLog(LogName = "Update position")]
    Task<ServiceResult> UpdateAsync(long id, PositionUpdationDto input);

    /// <summary>删除岗位。</summary>
    [OperateLog(LogName = "Delete position")]
    [UnitOfWork]
    Task<ServiceResult> DeleteAsync(long[] ids);

    /// <summary>按主键取岗位。</summary>
    Task<PositionDto?> GetAsync(long id);

    /// <summary>分页查询岗位。</summary>
    Task<PageModelDto<PositionDto>> GetPagedAsync(SearchPagedDto input);

    /// <summary>岗位选项。</summary>
    Task<List<OptionTreeDto>> GetOptionsAsync(bool? status = null);

    /// <summary>启用中的岗位，供审批设计器和找人。</summary>
    Task<List<PositionDto>> GetEnabledListAsync();
}
