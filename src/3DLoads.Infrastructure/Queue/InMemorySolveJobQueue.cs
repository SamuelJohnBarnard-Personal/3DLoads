using System.Threading.Channels;
using ThreeDLoads.Application.Abstractions;

namespace ThreeDLoads.Infrastructure.Queue;

// Thread-safe in-process queue backed by an unbounded System.Threading.Channels channel.
// Suitable for single-instance deployments; swap for a durable queue (e.g. RabbitMQ) for scale-out.
public sealed class InMemorySolveJobQueue : ISolveJobQueue
{
    private readonly Channel<Guid> channel = Channel.CreateUnbounded<Guid>();

    public ValueTask QueueAsync(Guid solveJobId, CancellationToken cancellationToken)
        => channel.Writer.WriteAsync(solveJobId, cancellationToken);

    public ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken)
        => channel.Reader.ReadAsync(cancellationToken);
}