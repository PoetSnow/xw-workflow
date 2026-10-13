using XW.Infra.EventBus.Tracker;

namespace XW.Shared.Application.Services.Trackers;

/// <summary>
/// 按 <see cref="TrackerKind"/> 选择具体 <see cref="IMessageTracker"/> 实现。
/// </summary>
public sealed class MessageTrackerFactory(IEnumerable<IMessageTracker> trackers)
{
    public IEnumerable<IMessageTracker> _trackers = trackers;

    public IMessageTracker Create(TrackerKind kind = TrackerKind.Db)
    {
        if (_trackers.IsNullOrEmpty())
        {
            return new NullMessageTrackerService();
        }

        return _trackers.FirstOrDefault(x => x.Kind == kind) ?? new NullMessageTrackerService();
    }
}
