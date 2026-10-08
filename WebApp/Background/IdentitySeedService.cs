using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using WebApp.Options;

namespace WebApp.Background;

public sealed class IdentitySeedService(
    IServiceScopeFactory scopeFactory,
    IOptions<SeedOptions> options,
    ILogger<IdentitySeedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        //UserManage is scoped, hosted service is singleton => we make scope
        using var scope = scopeFactory.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        foreach (var (key, account) in options.Value.Accounts)
        {
            if (string.IsNullOrWhiteSpace(account.UserName)
                || string.IsNullOrWhiteSpace(account.Password))
            {
                logger.LogWarning("Seed account {AccountKey} skipped: UserName or Password not configured.", key);
                continue;
            }

            if (await users.FindByNameAsync(account.UserName) is not null)
            {
                continue;
            }
            
            var result = await users.CreateAsync(new IdentityUser { UserName = account.UserName }, account.Password);
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Code));
                throw new InvalidOperationException($"Seding account '{key}' failed: {errors}");
            }
            
            logger.LogInformation("Seeded test account {UserName}.", account.UserName);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}