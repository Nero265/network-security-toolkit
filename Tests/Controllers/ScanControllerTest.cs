using Core.Jobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WebApp.Controllers;
using WebApp.DTOs;
using WebApp.Options;

namespace Tests.Controllers;

public sealed class ScanControllerTest
{
    private readonly ILogger<ScanController> _logger = NullLogger<ScanController>.Instance;
    
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
        var controller = new ScanController(store.Object, queue.Object, options, _logger)
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
        var controller = new ScanController(null!, null!, options, _logger);

        var request = new ScanRequest
        {
            Host = "127.0.0.1",
            StartPort = 1,
            EndPort = 1001
        };

        var result = await controller.StartTcpScan(request);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task StartTcpScan_ActiveJobsUnderLimit_ReturnsAccepted()
    {
        var store = new Mock<IScanJobStore>();
        store.Setup(s => s.CountActive()).Returns(9);

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

        var options = Options.Create(new ScanApiOptions { MaxPortsPerScan = 1000, MaxActiveJobs = 10 });
        var controller = new ScanController(store.Object, queue.Object, options, _logger)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var request = new ScanRequest { Host = "127.0.0.1", StartPort = 1, EndPort = 100 };

        var result = await controller.StartTcpScan(request);

        Assert.IsType<AcceptedAtActionResult>(result);
    }
    
    [Fact]
    public async Task StartTcpScan_ActiveJobsAtLimit_ReturnsServiceUnavailable()
    {
        var store = new Mock<IScanJobStore>();
        store.Setup(s => s.CountActive()).Returns(10);

        var options = Options.Create(new ScanApiOptions { MaxPortsPerScan = 1000, MaxActiveJobs = 10 });
        var controller = new ScanController(store.Object, null!, options, _logger);

        var request = new ScanRequest { Host = "127.0.0.1", StartPort = 1, EndPort = 100 };

        var result = await controller.StartTcpScan(request);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
    }
    
    [Fact]
    public async Task StartTcpScan_PrivateHost_ReturnsAccepted()
    {
        var store = new Mock<IScanJobStore>();
        store.Setup(s => s.CountActive()).Returns(0);
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

        var options = Options.Create(new ScanApiOptions
            { MaxPortsPerScan = 1000, MaxActiveJobs = 10, AllowPublicTargets = false });
        var controller = new ScanController(store.Object, queue.Object, options, _logger)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var request = new ScanRequest { Host = "127.0.0.1", StartPort = 1, EndPort = 100 };

        var result = await controller.StartTcpScan(request);

        Assert.IsType<AcceptedAtActionResult>(result);
    }
    
    [Fact]
    public async Task StartTcpScan_PublicHost_ReturnsForbidden()
    {
        var store = new Mock<IScanJobStore>();
        store.Setup(s => s.CountActive()).Returns(0);

        var options = Options.Create(new ScanApiOptions
            { MaxPortsPerScan = 1000, MaxActiveJobs = 10, AllowPublicTargets = false });
        var controller = new ScanController(store.Object, null!, options, _logger)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var request = new ScanRequest { Host = "8.8.8.8", StartPort = 1, EndPort = 100 };

        var result = await controller.StartTcpScan(request);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }
    
    [Fact]
    public async Task StartTcpScan_UnresolvableHost_ReturnsBadRequest()
    {
        var store = new Mock<IScanJobStore>();
        store.Setup(s => s.CountActive()).Returns(0);

        var options = Options.Create(new ScanApiOptions
            { MaxPortsPerScan = 1000, MaxActiveJobs = 10, AllowPublicTargets = false });
        var controller = new ScanController(store.Object, null!, options, _logger)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var request = new ScanRequest
            { Host = "this-host-does-not-exist.invalid", StartPort = 1, EndPort = 100 };

        var result = await controller.StartTcpScan(request);

        Assert.IsType<BadRequestObjectResult>(result);
    }
    
    [Fact]
    public async Task StartTcpScan_AllowPublicTargetsTrue_PublicHost_ReturnsAccepted()
    {
        var store = new Mock<IScanJobStore>();
        store.Setup(s => s.CountActive()).Returns(0);
        var expectedJob = new ScanJob
        {
            Id = Guid.NewGuid(),
            Host = "192.0.2.1",
            Ports = new List<int> { 1, 2, 3 },
            CreatedAt = DateTimeOffset.UtcNow
        };
        store.Setup(s => s.Create(It.IsAny<string>(), It.IsAny<List<int>>()))
            .Returns(expectedJob);

        var queue = new Mock<IScanJobQueue>();
        queue.Setup(q => q.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var options = Options.Create(new ScanApiOptions
            { MaxPortsPerScan = 1000, MaxActiveJobs = 10, AllowPublicTargets = true });
        var controller = new ScanController(store.Object, queue.Object, options, _logger)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var request = new ScanRequest { Host = "192.0.2.1", StartPort = 1, EndPort = 100 };

        var result = await controller.StartTcpScan(request);

        Assert.IsType<AcceptedAtActionResult>(result);
    }
    
    [Fact]
    public async Task StartTcpScan_PublicHost_LogsWarning()
    {
        var store = new Mock<IScanJobStore>();
        store.Setup(s => s.CountActive()).Returns(0);
    
        var logger = new Mock<ILogger<ScanController>>();
    
        var options = Options.Create(new ScanApiOptions
            { MaxPortsPerScan = 1000, MaxActiveJobs = 10, AllowPublicTargets = false });
        var controller = new ScanController(store.Object, null!, options, logger.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    
        var request = new ScanRequest { Host = "192.0.2.1", StartPort = 1, EndPort = 100 };
    
        await controller.StartTcpScan(request);
    
        logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("192.0.2.1")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}