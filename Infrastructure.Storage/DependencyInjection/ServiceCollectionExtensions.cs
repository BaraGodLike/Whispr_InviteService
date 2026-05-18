using Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Storage.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInviteStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Invites")
            ?? throw new InvalidOperationException("ConnectionStrings:Invites is required.");

        services.AddSingleton(new PostgresConnectionFactory(connectionString));
        services.AddScoped<IInviteRepository, PostgresInviteRepository>();

        return services;
    }
}
