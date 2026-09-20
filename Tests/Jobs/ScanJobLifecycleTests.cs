using Core;
using Core.Jobs;

namespace Tests;

public sealed class ScanJobLifecycleTests
{
    [Fact]
    public void ScanJob_ShouldFollowCorrectLifecycle_FromCreatedToCompleted()
    {
        // Arrange - Preparing storage and test data
        IScanJobStore store = new InMemoryScanJobStore();
        string testHost = "127.0.0.1";
        var testPorts = new List<int> { 80, 443, 8080 };
        
        // Act & Assert
        // (Pending)
        var createdJob = store.Create(testHost, testPorts);

        Assert.NotNull(createdJob);
        Assert.NotEqual(Guid.Empty, createdJob.Id);
        Assert.Equal(testHost, createdJob.Host);
        Assert.Equal(testPorts, createdJob.Ports);
        Assert.Equal(ScanJobStatus.Pending, createdJob.Status);
        Assert.Null(createdJob.Results); // results are null at the start
        Assert.Null(createdJob.Error);
        //Assert.True(createdJob.CompletedAt <= DateTimeOffset.UtcNow);
        Assert.NotEqual(default, createdJob.CreatedAt);
        Assert.True((DateTimeOffset.UtcNow - createdJob.CreatedAt).TotalSeconds < 5);
        Assert.Null(createdJob.CompletedAt);
        
        // Is it in storage(correctly) and can we read it?
        var fetchedJob = store.Get(createdJob.Id);
        Assert.NotNull(fetchedJob);
        Assert.Equal(ScanJobStatus.Pending, fetchedJob.Status);
        
        // Act & Assert 
        // (Running)
        var runningJob = store.MarkRunning(createdJob.Id);
        
        Assert.Equal(ScanJobStatus.Running, runningJob.Status);
        Assert.Null(runningJob.Results); //still nothing
        Assert.Null(runningJob.CompletedAt);
        
        //checking if saved object in "database" has new status
        var fetchedRunningJob = store.Get(createdJob.Id);
        Assert.NotNull(fetchedRunningJob);
        Assert.Equal(ScanJobStatus.Running, fetchedRunningJob.Status);
        
        // Act & Assert 
        // (Completed)
        var mockResult = new List<PortScanResult>
        {
            new PortScanResult(80, PortState.Open),
            new PortScanResult(443, PortState.Closed)
        };

        var completedJob = store.MarkCompleted(createdJob.Id, mockResult);
        
        Assert.Equal(ScanJobStatus.Completed, completedJob.Status);
        Assert.NotNull(completedJob.Results);
        Assert.Equal(2, completedJob.Results.Count);
        
        //check on each result inside the list
        Assert.Equal(80, completedJob.Results[0].Port);
        Assert.Equal(PortState.Open, completedJob.Results[0].State);
        
        Assert.Equal(443, completedJob.Results[1].Port);
        Assert.Equal(PortState.Closed, completedJob.Results[1].State);

        Assert.NotNull(completedJob.CompletedAt);
        //Assert.True(completedJob.CompletedAt >= completedJob.CreatedAt);
        Assert.NotEqual(default, completedJob.CompletedAt);
        Assert.True((DateTimeOffset.UtcNow - completedJob.CompletedAt.Value).TotalSeconds < 5);

        
        //final check from storage
        var fetchedCompletedJob = store.Get(createdJob.Id);
        Assert.NotNull(fetchedCompletedJob);
        Assert.Equal(ScanJobStatus.Completed, fetchedCompletedJob.Status);
        Assert.NotNull(fetchedCompletedJob.Results);
    }
    
    [Fact]
    public void ScanJob_ShouldFailCorrectly_WhenMarkedAsFailed()
    {
        // Arrange
        IScanJobStore store = new InMemoryScanJobStore();
        var job = store.Create("localhost", new List<int> { 22 });
        string errorMessage = "Host unreachable";

        // Act
        var failedJob = store.MarkFailed(job.Id, errorMessage);

        // Assert
        Assert.Equal(ScanJobStatus.Failed, failedJob.Status);
        Assert.Equal(errorMessage, failedJob.Error);
        Assert.Null(failedJob.Results);
        Assert.NotNull(failedJob.CompletedAt);

        var fetchedFailedJob = store.Get(job.Id);
        Assert.NotNull(fetchedFailedJob);
        Assert.Equal(ScanJobStatus.Failed, fetchedFailedJob.Status);
        Assert.Equal(errorMessage, fetchedFailedJob.Error);
    }

    [Fact]
    public void Update_ShouldThrowKeyNotFoundException_WhenJobDoesNotExist()
    {
        // Arrange
        IScanJobStore store = new InMemoryScanJobStore();
        Guid nonExistentId = Guid.NewGuid();

        // Act & Assert
        Assert.Throws<KeyNotFoundException>(() => store.MarkRunning(nonExistentId));
        Assert.Throws<KeyNotFoundException>(() => store.MarkCompleted(nonExistentId, new List<PortScanResult>()));
        Assert.Throws<KeyNotFoundException>(() => store.MarkFailed(nonExistentId, "Error"));
    }
}
