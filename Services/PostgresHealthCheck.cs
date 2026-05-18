using Infrastructure.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Services;

public sealed class PostgresHealthCheck(
    PostgresConnectionFactory connectionFactory,
    ILogger<PostgresHealthCheck> logger)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1;";
            await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy("PostgreSQL is reachable.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PostgreSQL health check failed.");
            return HealthCheckResult.Unhealthy("PostgreSQL health check failed.", ex);
        }
    }
}
