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

var cleanupService = scope.ServiceProvider.GetRequiredService<InviteCleanupService>();
await cleanupService.DeleteExpiredInvitesAsync(CancellationToken.None);
