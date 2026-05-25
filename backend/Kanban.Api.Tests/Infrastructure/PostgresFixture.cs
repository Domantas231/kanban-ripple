using Kanban.Api.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace Kanban.Api.Tests.Infrastructure;

/// <summary>
/// Process-wide singleton Postgres server shared by every test factory.
///
/// Boots one Testcontainers Postgres instance the first time a factory is constructed.
/// Each factory then asks for its own database via <see cref="CreateDatabaseAsync"/>,
/// which gives parallel-safe isolation between concurrently-running test classes
/// without paying the cost of a container per class.
///
/// The container is auto-cleaned by the Testcontainers Ryuk reaper when the test
/// process exits — no explicit dispose needed.
/// </summary>
public static class PostgresTestContainer
{
    private static readonly SemaphoreSlim InitLock = new(1, 1);
    private static PostgreSqlContainer? _container;
    private static string? _adminConnectionString;

    /// <summary>
    /// Creates a fresh database on the shared server, applies EF Core migrations,
    /// and returns a connection string + a Respawn instance scoped to that database.
    /// </summary>
    public static async Task<TestDatabaseHandle> CreateDatabaseAsync()
    {
        await EnsureContainerStartedAsync();

        var databaseName = $"kanban_test_{Guid.NewGuid():N}";

        await using (var adminConnection = new NpgsqlConnection(_adminConnectionString))
        {
            await adminConnection.OpenAsync();
            await using var createCommand = adminConnection.CreateCommand();
            createCommand.CommandText = $"CREATE DATABASE \"{databaseName}\"";
            await createCommand.ExecuteNonQueryAsync();
        }

        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(_adminConnectionString)
        {
            Database = databaseName
        };
        var connectionString = connectionStringBuilder.ConnectionString;

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using (var dbContext = new ApplicationDbContext(options))
        {
            await dbContext.Database.MigrateAsync();
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = [new Respawn.Graph.Table("__EFMigrationsHistory")]
        });

        return new TestDatabaseHandle(connectionString, respawner);
    }

    private static async Task EnsureContainerStartedAsync()
    {
        if (_container is not null)
        {
            return;
        }

        await InitLock.WaitAsync();
        try
        {
            if (_container is not null)
            {
                return;
            }

            _container = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("postgres")
                .WithUsername("kanban_test")
                .WithPassword("kanban_test")
                .Build();

            await _container.StartAsync();
            _adminConnectionString = _container.GetConnectionString();
        }
        finally
        {
            InitLock.Release();
        }
    }
}

/// <summary>
/// Per-factory database handle: connection string and a Respawn instance for cleanup.
/// </summary>
public sealed class TestDatabaseHandle
{
    public string ConnectionString { get; }
    private readonly Respawner _respawner;

    internal TestDatabaseHandle(string connectionString, Respawner respawner)
    {
        ConnectionString = connectionString;
        _respawner = respawner;
    }

    /// <summary>
    /// Truncates every table in the public schema (preserving the migrations history).
    /// </summary>
    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }
}
