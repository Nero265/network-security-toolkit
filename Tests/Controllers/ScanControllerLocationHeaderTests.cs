using System.Net;
using System.Net.Http.Json;
using Core.Jobs;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;

namespace Tests.Controllers;

public sealed class ScanControllerLocationHeaderTests
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ScanControllerLocationHeaderTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostTcpScan_ReturnsAccepted_WithLocationHeaderToStatusEndpoint()
    {
        var expectedJob = new ScanJob
        {
            Id = Guid.NewGuid(),
            Host = "127.0.0.1",
            Ports = new List<int> { 1, 2, 3 },
            CreatedAt = DateTimeOffset.UtcNow
        };

        var store = new Mock<IScanJobStore>();
        store.Setup(s => s.CountActive()).Returns(0);
        store.Setup(s => s.Create(It.IsAny<string>(), It.IsAny<List<int>>()))
            .Returns(expectedJob);

        var queue = new Mock<IScanJobQueue>();
        queue.Setup(q => q.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IScanJobStore>();
                services.AddSingleton(store.Object);

                services.RemoveAll<IScanJobQueue>();
                services.AddSingleton(queue.Object);
                
                services.RemoveAll<IHostedService>();
            });
        }).CreateClient();

        var response = await client.PostAsJsonAsync("/api/scan/tcp", new
        {
            Host = "127.0.0.1",
            StartPort = 1,
            EndPort = 100
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal($"/api/scan/{expectedJob.Id}/status", response.Headers.Location!.AbsolutePath);
    }
}