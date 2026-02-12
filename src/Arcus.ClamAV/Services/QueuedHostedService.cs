namespace Arcus.ClamAV.Services;

/// <summary>
/// Background service that processes queued tasks with configurable parallelism.
/// </summary>
public class QueuedHostedService : BackgroundService
{
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly ITelemetryService _telemetryService;
    private readonly ILogger<QueuedHostedService> _logger;
    private readonly int _maxConcurrentWorkers;

    public QueuedHostedService(
        IBackgroundTaskQueue taskQueue,
        ITelemetryService telemetryService,
        ILogger<QueuedHostedService> logger,
        IConfiguration configuration)
    {
        _taskQueue = taskQueue;
        _telemetryService = telemetryService;
        _logger = logger;
        _maxConcurrentWorkers = int.TryParse(
            configuration["BackgroundProcessing:MaxConcurrentWorkers"],
            out var workers) ? workers : 4;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "QueuedHostedService is starting with {Workers} concurrent workers",
            _maxConcurrentWorkers);

        // Create worker tasks
        var workers = new List<Task>();
        for (int i = 0; i < _maxConcurrentWorkers; i++)
        {
            var workerId = i + 1;
            workers.Add(WorkerAsync(workerId, stoppingToken));
        }

        // Wait for all workers to complete
        await Task.WhenAll(workers);

        _logger.LogInformation("QueuedHostedService has stopped");
    }

    private async Task WorkerAsync(int workerId, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker {WorkerId} started", workerId);
        int activeWorkers = 1;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var workItem = await _taskQueue.DequeueAsync(stoppingToken);

                if (workItem == null)
                {
                    continue;
                }

                _logger.LogDebug("Worker {WorkerId} processing task", workerId);

                // Track worker utilization before processing
                _telemetryService.TrackWorkerUtilization(activeWorkers, _maxConcurrentWorkers);

                var success = await workItem(stoppingToken);

                _logger.LogDebug(
                    "Worker {WorkerId} completed task with status: {Status}",
                    workerId,
                    success ? "Success" : "Failed");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Worker {WorkerId} cancelled", workerId);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker {WorkerId} encountered an error processing task", workerId);
            }
        }

        _logger.LogInformation("Worker {WorkerId} stopped", workerId);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("QueuedHostedService is stopping");
        await base.StopAsync(cancellationToken);
    }
}

