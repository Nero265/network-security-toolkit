using System.Net;
using System.Net.Http.Json;
using Core.Defense;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;

namespace Tests.Defense;

public sealed class LoginDefensePipelineTests
{
    [Fact]
    public async Task Login_WhenPipelineBlocks_Returns429_AndSkipsUserLookup()
    {
        var userManager = CreateUserManagerMock();
        var pipeline = new DefensePipelineBuilder().Add(new BlockAllHandler()).Build();
        using var factory = CreateFactory(userManager, pipeline);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "alice", password = "secret" });

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        userManager.Verify(m => m.FindByNameAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Login_WithEmptyPipeline_ContinuesToCredentialCheck()
    {
        var userManager = CreateUserManagerMock(); // FindByNameAsync returns null -> unknown user
        using var factory = CreateFactory(userManager, new DefensePipelineBuilder().Build());
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "alice", password = "secret" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        userManager.Verify(m => m.FindByNameAsync("alice"), Times.Once);
    }

    private static Mock<UserManager<IdentityUser>> CreateUserManagerMock() =>
        new(Mock.Of<IUserStore<IdentityUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);

    private static WebApplicationFactory<Program> CreateFactory(
        Mock<UserManager<IdentityUser>> userManager, IDefensePipeline pipeline) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();            // no migrations, seeding or scan worker
                services.RemoveAll<UserManager<IdentityUser>>();
                services.AddScoped(_ => userManager.Object);
                services.RemoveAll<IDefensePipeline>();
                services.AddSingleton(pipeline);
            }));

    private sealed class BlockAllHandler : IDefenseHandler
    {
        public ValueTask<DefenseResult> HandleAsync(
            DefenseContext context, DefenseDelegate next, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(DefenseResult.Block("test block", "TestHandler"));
    }
}