using System.Threading.Channels;

namespace Arcus.ClamAV.Services;

/// <summary>
/// Channel-based implementation of background task queue for processing tasks asynchronously.
/// </summary>
public class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<Func<CancellationToken, Task<bool>>> _queue;
    private readonly ILogger<BackgroundTaskQueue> _logger;
    private readonly ITelemetryService _telemetryService;

    public BackgroundTaskQueue(int capacity, ILogger<BackgroundTaskQueue> logger, ITelemetryService telemetryService)
    {
        _logger = logger;
        _telemetryService = telemetryService;
        
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        
        _queue = Channel.CreateBounded<Func<CancellationToken, Task<bool>>>(options);
        
        _logger.LogInformation("BackgroundTaskQueue initialized with capacity: {Capacity}", capacity);
    }

    public async Task EnqueueTask(Func<CancellationToken, Task<bool>> taskFunc)
    {
        if (taskFunc == null)
            throw new ArgumentNullException(nameof(taskFunc));

        await _queue.Writer.WriteAsync(taskFunc);
        _logger.LogDebug("Task enqueued successfully");
        
        // Track queue depth when task is enqueued
        _telemetryService.TrackQueueDepth(_queue.Reader.Count);
    }

    public async Task<Func<CancellationToken, Task<bool>>?> DequeueAsync(CancellationToken cancellationToken)
    {
        try
        {
            var workItem = await _queue.Reader.ReadAsync(cancellationToken);
            return workItem;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Dequeue operation cancelled");
            return null;
        }
    }
}

