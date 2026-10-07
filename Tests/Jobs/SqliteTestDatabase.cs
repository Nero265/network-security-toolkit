using Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WebApp.Components;

namespace Tests.Jobs;

internal sealed class SqliteTestDatabase
{
    private readonly SqliteConnection _connection = new SqliteConnection("Data Source=:memory:");
    
    public IDbContextFactory<AppDbContext> Factory { get; }

    public SqliteTestDatabase()
    {
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        Factory = new TestDbContextFactory(options);
        
        using var db = Factory.CreateDbContext();
        db.Database.Migrate();
    }

    public void Dispose() => _connection.Dispose();

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options)
        : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);
    }
}