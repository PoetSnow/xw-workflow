using XW.Infra.EventBus;
using XW.Infra.EventBus.Tracker;

namespace XW.Shared.Application.Services.Trackers;

/// <summary>
/// 基于 Redis 的 CAP 消息幂等追踪（当前为占位实现）。
/// </summary>
public class RedisMessageTrackerService : IMessageTracker
{
    public TrackerKind Kind => TrackerKind.Redis;

    public async Task<bool> HasProcessedAsync(long eventId, string trackerName)
    {
        return await Task.FromResult(false);
    }

    public async Task MarkAsProcessedAsync(long eventId, string trackerName)
    {
        await Task.CompletedTask;
    }

    public async Task<bool> HasProcessedAsync(BaseEvent eventModel)
    {
        return await HasProcessedAsync(eventModel.Id, eventModel.EventTarget);
    }

    public async Task MarkAsProcessedAsync(BaseEvent eventModel)
    {
        await MarkAsProcessedAsync(eventModel.Id, eventModel.EventTarget);
    }
}
