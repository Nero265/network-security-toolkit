using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Tests.Jobs;
using WebApp.Background;

namespace Tests.Background;

public sealed class DatabaseMigrationServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new(migrate: false);

    [Fact]
    public async Task StartAsync_OnFreshDatabase_AppliesAllMigrations()
    {
        await CreateService().StartAsync(CancellationToken.None);
        
        await using var db = _database.Factory.CreateDbContext();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(
            db.Database.GetMigrations(),
            await db.Database.GetAppliedMigrationsAsync());
        
        Assert.Equal(0, await db.ScanJobs.CountAsync());
        Assert.Equal(0, await db.Users.CountAsync());
    }
    
    [Fact]
        public async Task StartAsync_WhenCalledTwice_IsIdempotent()
        {
            var service = CreateService();
    
            await service.StartAsync(CancellationToken.None);
            var exception = await Record.ExceptionAsync(() => service.StartAsync(CancellationToken.None));
    
            Assert.Null(exception);
        }
    
    private DatabaseMigrationService CreateService() =>
        new (_database.Factory, NullLogger<DatabaseMigrationService>.Instance);
    
    public void Dispose() => _database.Dispose();
}