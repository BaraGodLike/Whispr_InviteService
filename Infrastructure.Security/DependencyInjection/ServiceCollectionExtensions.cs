using Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Security.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInviteSecurity(this IServiceCollection services)
    {
        services.AddSingleton<IInviteHasher, HmacInviteHasher>();
        services.AddSingleton<ISecretProtector, AesGcmSecretProtector>();
        services.AddSingleton<IPowService, HmacPowService>();

        return services;
    }
}
