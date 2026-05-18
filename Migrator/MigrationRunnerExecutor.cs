using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;

namespace Migrator;

public static class MigrationRunnerExecutor
{
    public static Task MigrateUpAsync(string connectionString, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var services = new ServiceCollection()
            .AddFluentMigratorCore()
            .ConfigureRunner(builder => builder
                .AddPostgres()
                .WithGlobalConnectionString(connectionString)
                .ScanIn(typeof(MigrationRunnerExecutor).Assembly).For.Migrations())
            .BuildServiceProvider(validateScopes: true);

        using var scope = services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();

        cancellationToken.ThrowIfCancellationRequested();
        runner.MigrateUp();

        return Task.CompletedTask;
    }
}
