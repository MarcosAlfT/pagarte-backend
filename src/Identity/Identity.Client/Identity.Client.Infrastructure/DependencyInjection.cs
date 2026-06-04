using Identity.Client.Application.Abstractions;
using Identity.Client.Infrastructure.Notifications;
using Identity.Client.Infrastructure.Policies;
using Identity.Client.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Client.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityClientInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ICurrentActorProvider, CurrentActorProvider>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<ITokenGenerator, SecureTokenGenerator>();
        services.AddSingleton<ITokenHasher, Sha256TokenHasher>();
        services.AddSingleton<IPolicyProvider, ConfigurationPolicyProvider>();
        services.AddSingleton<INotificationPublisher, ConsoleNotificationPublisher>();
        services.AddScoped<ITokenService, OpenIddictTokenService>();

        return services;
    }
}
