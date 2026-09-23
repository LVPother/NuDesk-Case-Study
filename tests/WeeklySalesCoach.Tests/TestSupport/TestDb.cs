using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WeeklySalesCoach.Data;

namespace WeeklySalesCoach.Tests.TestSupport;

/// <summary>An isolated in-memory SQLite database that lives as long as this object.</summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        Options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        using var db = CreateContext();
        db.Database.EnsureCreated();
        Factory = new Factory_(this);
    }

    public DbContextOptions<AppDbContext> Options { get; }
    public IDbContextFactory<AppDbContext> Factory { get; }

    public AppDbContext CreateContext() => new(Options);

    public void Dispose() => _connection.Dispose();

    private sealed class Factory_(TestDb db) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => db.CreateContext();
    }
}
