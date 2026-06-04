using Identity.Client.Application.Abstractions;
using Identity.Client.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Client.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityClientPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IdentityClientDbContext>(options =>
        {
            options.UseSqlServer(configuration.GetConnectionString("IdentityClientDb"));
            options.UseOpenIddict();
        });

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<IdentityClientDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IEmailConfirmationTokenRepository, EmailConfirmationTokenRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();

        return services;
    }
}
