using XW.Infra.EventBus;
using XW.Infra.EventBus.Tracker;

namespace XW.Shared.Application.Services.Trackers;

/// <summary>
/// 空实现的消息追踪器（未配置任何 Tracker 时使用）。
/// </summary>
public class NullMessageTrackerService : IMessageTracker
{
    public TrackerKind Kind => TrackerKind.Null;
    public async Task<bool> HasProcessedAsync(long eventId, string trackerName) => await Task.FromResult(false);
    public async Task MarkAsProcessedAsync(long eventId, string trackerName) => await Task.CompletedTask;
    public async Task<bool> HasProcessedAsync(BaseEvent eventModel) => await Task.FromResult(false);
    public async Task MarkAsProcessedAsync(BaseEvent eventModel) => await Task.CompletedTask;
}
