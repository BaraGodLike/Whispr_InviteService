using Application.Abstractions;
using Application.DependencyInjection;
using Infrastructure.Security;
using Infrastructure.Security.DependencyInjection;
using Infrastructure.Storage.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services
    .AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"]);
builder.Services.AddGrpcHealthChecks();
builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
builder.Services.AddInviteOptions(builder.Configuration);
builder.Services.AddInviteSecurity();
builder.Services.AddInviteStorage(builder.Configuration);
builder.Services.AddInviteApplicationServices();

var app = builder.Build();

app.MapGrpcService<InviteGrpcService>();
app.MapGrpcHealthChecksService();
app.MapGet("/", () => "Use a gRPC client to communicate with this service.");

app.Run();
