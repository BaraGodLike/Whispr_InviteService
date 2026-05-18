using Migrator;

var connectionString = args.FirstOrDefault()
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__Invites");

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine(
        "Pass the PostgreSQL connection string as the first argument or via ConnectionStrings__Invites.");
    return 1;
}

await MigrationRunnerExecutor.MigrateUpAsync(connectionString, CancellationToken.None);
return 0;
