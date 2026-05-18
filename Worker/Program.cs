using Application.Abstractions;
using Application.DependencyInjection;
using Application.Services;
using Infrastructure.Storage.DependencyInjection;
using Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
builder.Services.AddInviteStorage(builder.Configuration);
builder.Services.AddInviteCleanupServices();

using var host = builder.Build();
using var scope = host.Services.CreateScope();
var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger("WorkerLifecycle");

logger.LogInformation("Invite cleanup worker started.");

try
{
    var cleanupService = scope.ServiceProvider.GetRequiredService<InviteCleanupService>();
    await cleanupService.DeleteExpiredInvitesAsync(CancellationToken.None);
    logger.LogInformation("Invite cleanup worker completed.");
}
catch (Exception ex)
{
    logger.LogError(ex, "Invite cleanup worker failed.");
    throw;
}
