using Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Tests.Jobs;

internal sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new SqliteConnection("Data Source=:memory:");

    public IDbContextFactory<AppDbContext> Factory { get; }

    public SqliteTestDatabase(bool migrate = true)
    {
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        Factory = new TestDbContextFactory(options);

        if (migrate)
        {
            using var db = Factory.CreateDbContext();
            db.Database.Migrate();
        }
    }

    public void Dispose() => _connection.Dispose();

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options)
        : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);
    }
}