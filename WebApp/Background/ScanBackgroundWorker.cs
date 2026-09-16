using Core;
using Core.Jobs;

namespace WebApp.Background;

public sealed class ScanBackgroundWorker : BackgroundService
{
    private readonly IScanJobQueue _queue;
    private readonly IScanJobStore _jobStore;
    private readonly IPortScanner _portScanner;
    private readonly ILogger<ScanBackgroundWorker> _logger;

    public ScanBackgroundWorker(
        IScanJobQueue queue,
        IScanJobStore jobStore,
        IPortScanner portScanner,
        ILogger<ScanBackgroundWorker> logger)
    {
        _queue = queue;
        _jobStore = jobStore;
        _portScanner = portScanner;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scan Background Worker is successfully started.");
        
        // This loop is working until app shutdown (stoppingToken)
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 1. We wait for job to appear in channel; if there is none, no CPU consuming
                Guid jobId = await _queue.DequeueAsync(stoppingToken);

                _logger.LogInformation("Took job {jobId} from waiting queue.", jobId);

                // 2. Take out the details of job to see what we are scanning
                var job = _jobStore.Get(jobId);
                if (job == null)
                {
                    _logger.LogWarning("Job {jobId} is found in queue, but does not exist in storage", jobId);
                    continue;
                }

                // 3. Changing status -> Running
                _jobStore.MarkRunning(jobId);

                // 4. Isolated try-catch block for the scanning operation to prevent hanging states
                try
                {
                    _logger.LogInformation("Scan is started for job {jobId} on host {Host}...", jobId, job.Host);

                    // Starting real scanner from Core/
                    var results = await _portScanner.ScanAsync(job.Host, job.Ports, stoppingToken);

                    // 5. We finish the job
                    _jobStore.MarkCompleted(jobId, results);

                    _logger.LogInformation("Job {jobId} is successfully finished.", jobId);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // If scanning fails (e.g., SocketException on unresolvable host), we catch it here
                    // and safely move the job state to Failed so the API client receives a definitive response.
                    _jobStore.MarkFailed(jobId, ex.Message);
                    _logger.LogError(ex, "Job {jobId} failed during network scan.", jobId);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown behavior when stoppingToken cancels DequeueAsync
                break;
            }
            catch (Exception ex)
            {
                // Outer safety net for unexpected errors (e.g., store or logger failures)
                _logger.LogError(ex, "Fatal error inside background worker loop.");
            }
        }
    }
}
