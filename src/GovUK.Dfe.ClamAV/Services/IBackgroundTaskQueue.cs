namespace GovUK.Dfe.ClamAV.Services;

/// <summary>
/// Interface for queueing background tasks for asynchronous processing.
/// </summary>
public interface IBackgroundTaskQueue
{
    /// <summary>
    /// Enqueues a task function to be executed in the background.
    /// </summary>
    /// <param name="taskFunc">The task function to execute. Returns true on success, false on failure.</param>
    /// <returns>A task that completes when the task is enqueued.</returns>
    Task EnqueueTask(Func<CancellationToken, Task<bool>> taskFunc);

    /// <summary>
    /// Dequeues a task from the queue for processing.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The task function to execute, or null if cancelled.</returns>
    Task<Func<CancellationToken, Task<bool>>?> DequeueAsync(CancellationToken cancellationToken);
}
