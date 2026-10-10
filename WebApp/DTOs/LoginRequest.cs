using System.ComponentModel.DataAnnotations;

namespace WebApp.DTOs;

public sealed class LoginRequest
{
    [Required(ErrorMessage = "Username is mandatory.")]
    [StringLength(256)]
    public string Username { get; init; } = string.Empty;
    
    [Required(ErrorMessage = "Password is mandatory.")]
    [StringLength(256)]
    public string Password { get; init; } = string.Empty;
}