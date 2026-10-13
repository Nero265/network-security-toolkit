using System.Net;
using System.Net.Http.Json;

namespace Tests.Auth;

public sealed class AuthLoginTests : IAsyncLifetime
{
    private const string UserName = "testuser";
    private const string Password = "Str0ng!Passw0rd";

    private readonly AuthApiFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _factory.SeedUserAsync(UserName, Password);
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private Task<HttpResponseMessage> LoginAsync(string username, string password) =>
        _client.PostAsJsonAsync("/api/auth/login", new { username, password });

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOk()
    {
        var response = await LoginAsync(UserName, Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var response = await LoginAsync(UserName, "wrong-password");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownUser_ReturnsUnauthorized()
    {
        var response = await LoginAsync("nobody", Password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownUserAndWrongPassword_ReturnIdenticalResponses()
    {
        var wrongPassword = await LoginAsync(UserName, "wrong-password");
        var unknownUser = await LoginAsync("nobody", "wrong-password");

        Assert.Equal(wrongPassword.StatusCode, unknownUser.StatusCode);
        Assert.Equal(
            await wrongPassword.Content.ReadAsStringAsync(),
            await unknownUser.Content.ReadAsStringAsync());
    }
}