namespace ThreeDLoads.Application.Abstractions;

// Async FIFO queue that decouples the API (producer) from the background solver (consumer).
public interface ISolveJobQueue
{
    ValueTask QueueAsync(Guid solveJobId, CancellationToken cancellationToken);
    ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken);
}