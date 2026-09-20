using System.Net;
using System.Net.Sockets;
using Core;
using Core.Jobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WebApp.DTOs;
using WebApp.Options;

namespace WebApp.Controllers;

[ApiController]
[Route("api/scan")]
public sealed class ScanController : ControllerBase
{
    private readonly IScanJobStore _jobStore;
    private readonly IScanJobQueue _jobQueue;
    private readonly ScanApiOptions _options;
    private readonly ILogger<ScanController> _logger;

    public ScanController(
        IScanJobStore jobStore,
        IScanJobQueue jobQueue,
        IOptions<ScanApiOptions> options,
        ILogger<ScanController> logger)
    {
        _jobStore = jobStore;
        _jobQueue = jobQueue;
        _options = options.Value;
        _logger = logger;
    }

    [HttpPost("tcp")]
    public async Task<ActionResult> StartTcpScan([FromBody] ScanRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (request.StartPort > request.EndPort)
        {
            return BadRequest(new { Message = "StartPort must be equal or lower than EndPort." });
        }

        var portCount = request.EndPort - request.StartPort + 1;
        if (portCount > _options.MaxPortsPerScan)
        {
            _logger.LogWarning(
                "Scan request for host {Host} rejected: requested {PortCount} ports exceeds max {MaxPortsPerScan}.",
                request.Host, portCount, _options.MaxPortsPerScan);
            return BadRequest(new
            {
                Message =
                    $"Requested port count ({portCount}) exceeds the allowed maximum ({_options.MaxPortsPerScan})."
            });
        }

        if (_jobStore.CountActive() >= _options.MaxActiveJobs)
        {
            _logger.LogWarning(
                "Scan request for host {Host} rejected: server at capacity ({ActiveCount}/{MaxActiveJobs} active jobs).",
                request.Host, _jobStore.CountActive(), _options.MaxActiveJobs);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                Message = $"Server is at capacity ({_options.MaxActiveJobs} active jobs). Try again later."
            });
        }

        if (!_options.AllowPublicTargets)
        {
            IPAddress[] addresses;
            try
            {
                addresses = await Dns.GetHostAddressesAsync(request.Host, HttpContext.RequestAborted);
            }
            catch (SocketException)
            {
                _logger.LogWarning("Scan request rejected: could not resolve host {Host}.", request.Host);

                return BadRequest(new { Message = $"Could not resolve host '{request.Host}'." });
            }

            if (addresses.Length == 0 || !PrivateNetworkRanges.IsAllowed(addresses[0]))
            {
                _logger.LogWarning(
                    "Scan request for host {Host} rejected: resolved address {ResolvedAddress} is outside allowed private/loopback ranges.",
                    request.Host, addresses.Length > 0 ? addresses[0].ToString() : "N/A");

                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    Message =
                        $"Scanning public targets is not allowed. Host '{request.Host}' resolves outside the allowed private/loopback ranges."
                });
            }
        }


        var ports = new List<int>();
        for (int port = request.StartPort; port <= request.EndPort; port++)
        {
            ports.Add(port);
        }

        var job = _jobStore.Create(request.Host, ports);

        _logger.LogInformation(
            "Scan job {JobId} created for host {Host} with {PortCount} ports.",
            job.Id, job.Host, portCount);

        //we put ID of job in our channel (waiting queue)
        //we use await because EnqueueAsync is asynchronous operation which lasts short(just write in channel)
        await _jobQueue.EnqueueAsync(job.Id, HttpContext.RequestAborted);

        // We immediately return 202 Accepted and ID of job to the client (non-blocking)
        return AcceptedAtAction(
            nameof(GetJobStatus),
            new { id = job.Id },
            new { JobId = job.Id, Status = job.Status });
    }

    [HttpGet("{id}/status")]
    public IActionResult GetJobStatus(Guid id)
    {
        var job = _jobStore.Get(id);
        if (job == null)
        {
            return NotFound(new { Message = $"Scan job with ID '{id}' not found." });
        }

        return Ok(new
        {
            job.Id,
            job.Host,
            job.Status,
            Results = job.Status == ScanJobStatus.Completed ? job.Results : null,
            job.Error,
            job.CreatedAt,
            job.CompletedAt
        });
    }
}