using Npgsql;

using Testcontainers.PostgreSql;

namespace FleetTrack.IntegrationTests;

public class PostgresContainerTests
{
    private readonly PostgreSqlContainer _container;

    public PostgresContainerTests()
    {
        _container = new PostgreSqlBuilder("postgres:17")
           .Build();
    }

    [OneTimeSetUp]
    public async Task Setup()
    {
        await _container.StartAsync();
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        await _container.DisposeAsync();
    }

    [Test]
    public async Task SelectVersion_ReturnsPostgreSQL17()
    {
        // Arrange
        var connectionString = _container.GetConnectionString();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        // Act
        await using var command = new NpgsqlCommand("SELECT version()", connection);
        var result = await command.ExecuteScalarAsync();

        // Assert
        Assert.That(result, Does.Contain("PostgreSQL 17"));
    }
}