namespace Kanban.Api.Tests.Infrastructure;

/// <summary>
/// Base class for integration tests that need a clean Postgres database before each test.
///
/// Implements <see cref="IAsyncLifetime"/> so xUnit invokes <see cref="InitializeAsync"/>
/// before every test method, which delegates to Respawn to truncate the public schema.
///
/// Use this whenever a test class shares a factory with <see cref="IClassFixture{T}"/> —
/// without per-test reset, tests in the same class share state and become order-dependent.
/// </summary>
public abstract class IntegrationTestBase<TFactory> : IAsyncLifetime
    where TFactory : KanbanApiFactoryBase
{
    protected TFactory Factory { get; }

    protected IntegrationTestBase(TFactory factory)
    {
        Factory = factory;
    }

    public Task InitializeAsync() => Factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;
}
