using Identity.Client.Application.UseCases;
using Identity.Client.Infrastructure;
using Identity.Client.Persistence;
using Microsoft.AspNetCore.Authentication;
using OpenIddict.Validation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

builder.AddServiceDefaults();
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddIdentityClientPersistence(configuration);
builder.Services.AddIdentityClientInfrastructure();
builder.Services.AddScoped<RegisterUserUseCase>();
builder.Services.AddScoped<ConfirmEmailUseCase>();
builder.Services.AddScoped<LoginWithPasswordUseCase>();
builder.Services.AddScoped<RefreshTokenUseCase>();
builder.Services.AddScoped<LogoutUseCase>();
builder.Services.AddScoped<ForgotPasswordUseCase>();
builder.Services.AddScoped<ResetPasswordUseCase>();
builder.Services.AddScoped<ChangePasswordUseCase>();

builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
            .UseDbContext<IdentityClientDbContext>();
    })
    .AddServer(options =>
    {
        var accessTokenMinutes = configuration.GetValue("IdentityPolicies:TokenPolicy:AccessTokenMinutes", 15);
        var refreshTokenDays = configuration.GetValue("IdentityPolicies:TokenPolicy:RefreshTokenDays", 14);

        options.SetTokenEndpointUris("/connect/token");
        options.AllowPasswordFlow();
        options.AllowRefreshTokenFlow();
        options.AcceptAnonymousClients();

        options.AddDevelopmentEncryptionCertificate();
        options.AddDevelopmentSigningCertificate();
        options.DisableAccessTokenEncryption();

        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(accessTokenMinutes));
        options.SetRefreshTokenLifetime(TimeSpan.FromDays(refreshTokenDays));
        options.UseAspNetCore()
            .EnableTokenEndpointPassthrough();
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
});

builder.Services.AddAuthorization();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapOpenApi();

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
