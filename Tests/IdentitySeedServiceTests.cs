using Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WebApp.Background;
using WebApp.Options;

namespace Tests;

public sealed class IdentitySeedServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly List<ServiceProvider> _providers = [];

    public IdentitySeedServiceTests() => _connection.Open();

    public void Dispose()
    {
        foreach (var provider in _providers) provider.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task StartAsync_CreatesConfiguredAccounts_WithoutWorkingPasswords()
    {
        var provider = CreateProvider(relaxedPasswordPolicy: true);
        var seeder = CreateSeeder(provider, TwoAccounts("password", "Str0ng!Pass#1"));

        await seeder.StartAsync(CancellationToken.None);

        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        var weak = await users.FindByNameAsync("demo-weak");
        var strong = await users.FindByNameAsync("demo-strong");
        
        Assert.NotNull(weak);
        Assert.NotNull(strong);
        Assert.True(await users.CheckPasswordAsync(weak, "password"));
        Assert.True(await users.CheckPasswordAsync(strong, "Str0ng!Pass#1"));
    }
    
    [Fact]
    public async Task StartAsync_WhenAccountsExist_DoesNotDuplicateOrChangePasswords()
    {
        var provider = CreateProvider(relaxedPasswordPolicy: true);
        await CreateSeeder(provider, TwoAccounts("password", "Str0ng!Pass#1"))
            .StartAsync(CancellationToken.None);
        
        await CreateSeeder(provider, TwoAccounts("other-weak", "Other!Strong#2"))
            .StartAsync(CancellationToken.None);

        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        Assert.Equal(2, users.Users.Count());
        var weak = await users.FindByNameAsync("demo-weak");
        Assert.True(await users.CheckPasswordAsync(weak!, "password"));
        Assert.False(await users.CheckPasswordAsync(weak!, "other-weak"));
    }
    
    [Fact]
    public async Task StartAsync_WhenPasswordMissing_SkipsAccountWithoutThrowing()
    {
        var provider = CreateProvider(relaxedPasswordPolicy: true);
        var options = new SeedOptions
        {
            Accounts =
            {
                ["Weak"] = new SeedAccount { UserName = "demo-weak", Password = "password" },
                ["Strong"] = new SeedAccount { UserName = "demo-strong" } // no password
            }
        };

        await CreateSeeder(provider, options).StartAsync(CancellationToken.None);

        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        Assert.NotNull(await users.FindByNameAsync("demo-weak"));
        Assert.Null(await users.FindByNameAsync("demo-strong"));
    }
    
    [Fact]
    public async Task StartAsync_WhenPolicyRejectsPassword_Throws()
    {
        var provider = CreateProvider(relaxedPasswordPolicy: false);
        var seeder = CreateSeeder(provider, TwoAccounts("password", "Str0ng!Pass#1"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => seeder.StartAsync(CancellationToken.None));
    }

    private ServiceProvider CreateProvider(bool relaxedPasswordPolicy)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite(_connection));
        services.AddScoped(sp =>
            sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());
        services.AddIdentityCore<IdentityUser>(options =>
            {
                if (!relaxedPasswordPolicy) return;
                options.Password.RequiredLength = 4;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddEntityFrameworkStores<AppDbContext>();

        var provider = services.BuildServiceProvider();
        _providers.Add(provider);

        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
        return provider;
    }

    private static IdentitySeedService CreateSeeder(ServiceProvider provider, SeedOptions options) =>
        new(provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(options),
            NullLogger<IdentitySeedService>.Instance);

    private static SeedOptions TwoAccounts(string weakPassword, string strongPassword) => new()
    {
        Accounts = 
        {
            ["Weak"] = new SeedAccount { UserName = "demo-weak", Password = weakPassword },
            ["Strong"] = new SeedAccount { UserName = "demo-strong", Password = strongPassword }
        }
    };
}