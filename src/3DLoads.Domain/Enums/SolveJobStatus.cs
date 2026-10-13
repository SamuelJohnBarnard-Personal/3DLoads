namespace ThreeDLoads.Domain.Enums;

// Lifecycle states of an async solve job.
public enum SolveJobStatus
{
    Queued = 1,
    Running = 2,
    Completed = 3,
    Failed = 4
}