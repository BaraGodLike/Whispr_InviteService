using Application.Options;
using Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInviteOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<InviteOptions>(configuration.GetSection("Invite"));
        services.Configure<LockoutOptions>(configuration.GetSection("Lockout"));
        services.Configure<PowOptions>(configuration.GetSection("Pow"));
        services.Configure<EncryptionOptions>(configuration.GetSection("Encryption"));
        services.Configure<HashingOptions>(configuration.GetSection("Hashing"));

        services.AddSingleton(sp => sp.GetRequiredService<IOptions<InviteOptions>>().Value);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<LockoutOptions>>().Value);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<PowOptions>>().Value);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<EncryptionOptions>>().Value);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<HashingOptions>>().Value);

        return services;
    }

    public static IServiceCollection AddInviteApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<InviteApplicationService>();
        return services;
    }

    public static IServiceCollection AddInviteCleanupServices(this IServiceCollection services)
    {
        services.AddScoped<InviteCleanupService>();
        return services;
    }
}
