using System.ComponentModel.DataAnnotations;

namespace WebApp.DTOs;

public sealed class ScanRequest
{
    [Required(ErrorMessage = "Host (IP address or domain) is mandatory.")]
    public string Host { get; init; } = string.Empty;
    
    [Required]
    [Range(1, 65535, ErrorMessage = "Start port must be between 1 and 65535.")]
    public int StartPort { get; init; }
    
    [Required]
    [Range(1, 65535, ErrorMessage = "End must be between 1 and 65535.")]
    public int EndPort { get; init; }

}