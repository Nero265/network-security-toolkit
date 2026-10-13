using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebApp.DTOs;

namespace WebApp.Controllers;

public enum LoginOutcome
{
    Success,
    UnknownUser,
    WrongPassword
}

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    UserManager<IdentityUser> userManager,
    SignInManager<IdentityUser> signInManager,
    IPasswordHasher<IdentityUser> passwordHasher,
    ILogger<AuthController> logger) :  ControllerBase
{
    private const string InvalidCredentialsMessage = "Invalid username or password.";

    // Hash of a throwaway password: for unknown users we verify against it,
    // so the response time does not reveal whether the username exists.
    private static readonly string DummyHash =
        new PasswordHasher<IdentityUser>().HashPassword(new IdentityUser(), Guid.NewGuid().ToString());

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var sourceIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var user = await userManager.FindByNameAsync(request.Username);

        LoginOutcome outcome;
        if (user is null)
        {
            passwordHasher.VerifyHashedPassword(new IdentityUser(), DummyHash, request.Password);
            outcome = LoginOutcome.UnknownUser;
        }
        else
        {
            //lockoutOnFailure: false --> lockout belongs to the defense pipeline, added later
            var result = await signInManager.CheckPasswordSignInAsync(
                user, request.Password, lockoutOnFailure: false);
            outcome = result.Succeeded ? LoginOutcome.Success : LoginOutcome.WrongPassword;
        }
        
        logger.Log(
            outcome == LoginOutcome.Success ? LogLevel.Information : LogLevel.Warning,
            "Login attempt for {Username} from {SourceIp} finished with outcome {Outcome}.",
            request.Username, sourceIp, outcome);

        return outcome == LoginOutcome.Success
            ? Ok(new { Message = "Login successful." })
            : Unauthorized(new { Message = InvalidCredentialsMessage });

    }
}