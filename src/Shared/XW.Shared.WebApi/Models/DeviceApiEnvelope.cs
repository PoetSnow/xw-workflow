namespace XW.Shared.WebApi.Models;

/// <summary>
/// 设备域统一 API 响应包络：{ code, result, message, success }。
/// </summary>
public sealed class DeviceApiEnvelope
{
    public int Code { get; set; }
    public object? Result { get; set; }
    public string Message { get; set; } = "success";
    public bool Success { get; set; } = true;

    public static DeviceApiEnvelope Ok(object? result, string message = "success") => new()
    {
        Code = 200,
        Result = result,
        Message = message,
        Success = true
    };

    public static DeviceApiEnvelope Fail(int code, string message) => new()
    {
        Code = code,
        Result = null,
        Message = message,
        Success = false
    };
}
