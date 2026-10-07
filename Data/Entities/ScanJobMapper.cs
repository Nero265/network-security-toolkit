using System.Text.Json;
using System.Text.Json.Serialization;
using Core;
using Core.Jobs;

namespace Data.Entities;

internal static class ScanJobMapper
{
    private static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static ScanJob ToDomain(this ScanJobEntity e) => new()
    {
        Id = e.Id,
        Host = e.Host,
        Ports = JsonSerializer.Deserialize<List<int>>(e.PortsJson, Json)!,
        Type = e.Type,
        Status = e.Status,
        Results = e.ResultsJson is null
            ? null
            : JsonSerializer.Deserialize<List<PortScanResult>>(e.ResultsJson, Json),
        Error = e.Error,
        CreatedAt = e.CreatedAt,
        CompletedAt = e.CompletedAt
    };

    public static ScanJobEntity ToEntity(this ScanJob j) => new()
    {
        Id = j.Id,
        Host = j.Host,
        PortsJson = JsonSerializer.Serialize(j.Ports, Json),
        Type = j.Type,
        Status = j.Status,
        ResultsJson = j.Results is null ? null : JsonSerializer.Serialize(j.Results, Json),
        Error = j.Error,
        CreatedAt = j.CreatedAt,
        CompletedAt = j.CompletedAt
    };
    
    public static string SerializeResults(IReadOnlyList<PortScanResult> results) =>
        JsonSerializer.Serialize(results, Json);
}