using Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Tests.Auth;

public sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    // in-memory SQLite lives only as long as this connection stays open
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public AuthApiFactory() => _connection.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // no recovery service, seed service or scan worker in these tests
            services.RemoveAll<IHostedService>();

            // swap the real app.db for the in-memory connection
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextFactory<AppDbContext>>();
            services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite(_connection));
        });
    }

    public async Task SeedUserAsync(string userName, string password)
    {
        using var scope = Services.CreateScope();

        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            await db.Database.EnsureCreatedAsync();
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var result = await userManager.CreateAsync(new IdentityUser(userName), password);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}