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

    public ScanController(IScanJobStore jobStore, IScanJobQueue jobQueue, IOptions<ScanApiOptions> options)
    {
        _jobStore = jobStore;
        _jobQueue = jobQueue;
        _options = options.Value;
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
            return BadRequest(new 
            { 
                Message = $"Requested port count ({portCount}) exceeds the allowed maximum ({_options.MaxPortsPerScan})." 
            });
        }


        var ports = new List<int>();
        for (int port = request.StartPort; port <= request.EndPort; port++)
        {
            ports.Add(port);
        }

        var job = _jobStore.Create(request.Host, ports);


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