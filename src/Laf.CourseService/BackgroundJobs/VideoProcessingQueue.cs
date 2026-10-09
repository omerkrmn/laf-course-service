using System.Threading.Channels;

namespace Laf.CourseService.BackgroundJobs;

public class VideoProcessingQueue
{
    private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = true
    });

    public ValueTask QueueVideoAsync(Guid videoId) => _queue.Writer.WriteAsync(videoId);
    public ValueTask<Guid> DequeueVideoAsync(CancellationToken cancellationToken) => _queue.Reader.ReadAsync(cancellationToken);
}
