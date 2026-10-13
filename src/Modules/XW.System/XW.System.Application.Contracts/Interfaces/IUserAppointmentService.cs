using XW.System.Application.Contracts.Dtos.User;

namespace XW.System.Application.Contracts.Interfaces;

/// <summary>用户任职。</summary>
public interface IUserAppointmentService : IAppService
{
    /// <summary>某用户的全部任职。</summary>
    Task<List<UserAppointmentDto>> GetByUserAsync(long userId);

    /// <summary>某用户当前有效任职。</summary>
    Task<List<UserAppointmentDto>> GetActiveByUserAsync(long userId);

    /// <summary>全部有效任职，供审批找人。</summary>
    Task<List<UserAppointmentDto>> GetActiveListAsync();

    /// <summary>整表替换某用户任职。须恰好一份主职。</summary>
    [OperateLog(LogName = "Replace user appointments")]
    [UnitOfWork]
    Task<ServiceResult> ReplaceByUserAsync(long userId, List<UserAppointmentDto> input);
}
