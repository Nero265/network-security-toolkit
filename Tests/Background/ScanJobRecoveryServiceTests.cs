using Core;
using Core.Jobs;
using Data;
using Microsoft.Extensions.Logging.Abstractions;
using Tests.Jobs;
using WebApp.Background;

namespace Tests.Background;

public sealed class ScanJobRecoveryServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public async Task StartAsync_MarksPendingAndRunningAsFailed_AndKeepsCompleted()
    {
        var store = new SqliteScanJobStore(_database.Factory);

        var pending = store.Create("127.0.0.1", new List<int> { 80 });
        var running = store.Create("127.0.0.1", new List<int> { 80 });
        store.MarkRunning(running.Id);
        var completed = store.Create("127.0.0.1", new List<int> { 80 });
        store.MarkRunning(completed.Id);
        store.MarkCompleted(completed.Id, new List<PortScanResult>());

        var service = new ScanJobRecoveryService(
            _database.Factory, NullLogger<ScanJobRecoveryService>.Instance);

        await service.StartAsync(CancellationToken.None);
        
        Assert.Equal(ScanJobStatus.Failed, store.Get(pending.Id)!.Status);
        Assert.Equal(ScanJobStatus.Failed, store.Get(running.Id)!.Status);
        Assert.Equal("Interrupted by application restart", store.Get(running.Id)!.Error);
        Assert.Equal(ScanJobStatus.Completed, store.Get(completed.Id)!.Status);
        Assert.Equal(0, store.CountActive());
    }
    
    public void Dispose() => _database.Dispose();
}