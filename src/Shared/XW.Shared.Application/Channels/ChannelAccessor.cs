using System.Threading.Channels;

namespace XW.Shared.Application.Channels;

/// <summary>
/// 有界 Channel 单例访问器（队列满时丢弃最旧消息）。
/// </summary>
public sealed class ChannelAccessor<TModel>
{
    private static readonly Lazy<ChannelAccessor<TModel>> _lazy = new(() => new ChannelAccessor<TModel>());

    static ChannelAccessor()
    {
    }

    private ChannelAccessor()
    {
        var channelOptions = new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        };
        var channel = Channel.CreateBounded<TModel>(channelOptions);
        Writer = channel.Writer;
        Reader = channel.Reader;
    }

    public static ChannelAccessor<TModel> Instance => _lazy.Value;

    public ChannelWriter<TModel> Writer { get; private set; }

    public ChannelReader<TModel> Reader { get; private set; }
}
