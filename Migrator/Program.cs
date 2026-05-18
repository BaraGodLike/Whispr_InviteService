using Migrator;
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddSimpleConsole()
        .SetMinimumLevel(LogLevel.Warning);
});

var logger = loggerFactory.CreateLogger("Migrator");

var connectionString = args.FirstOrDefault()
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__Invites");

if (string.IsNullOrWhiteSpace(connectionString))
{
    logger.LogError(
        "Migration failed because the PostgreSQL connection string was not provided.");
    return 1;
}

try
{
    await MigrationRunnerExecutor.MigrateUpAsync(connectionString, CancellationToken.None);
    return 0;
}
catch (Exception ex)
{
    logger.LogError(ex, "Migration execution failed.");
    return 1;
}
