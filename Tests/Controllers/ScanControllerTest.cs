using Core.Jobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using WebApp.Controllers;
using WebApp.DTOs;
using WebApp.Options;

namespace Tests.Controllers;

public sealed class ScanControllerTest
{
    [Fact]
    public async Task StartTcpScan_PortCountWithinLimit_ReturnsAccepted()
    {
        var store = new Mock<IScanJobStore>();
        var expectedJob = new ScanJob
        {
            Id = Guid.NewGuid(),
            Host = "127.0.0.1",
            Ports = new List<int> { 1, 2, 3 },
            CreatedAt = DateTimeOffset.UtcNow
        };

        store.Setup(s => s.Create(It.IsAny<string>(), It.IsAny<List<int>>()))
            .Returns(expectedJob);
        
        var queue = new Mock<IScanJobQueue>();
        queue.Setup(q => q.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var options = Options.Create(new ScanApiOptions { MaxPortsPerScan = 1000 });
        var controller = new ScanController(store.Object, queue.Object, options)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var request = new ScanRequest
        {
            Host = "127.0.0.1",
            StartPort = 1,
            EndPort = 1000
        };

        var result = await controller.StartTcpScan(request);

        Assert.IsType<AcceptedAtActionResult>(result);
    }
    
    [Fact]
    public async Task StartTcpScan_PortCountExceedsLimit_ReturnsBadRequest()
    {
        var options = Options.Create(new ScanApiOptions { MaxPortsPerScan = 1000 });
        var controller = new ScanController(null!, null!, options);

        var request = new ScanRequest
        {
            Host = "127.0.0.1",
            StartPort = 1,
            EndPort = 1001
        };

        var result = await controller.StartTcpScan(request);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}