using Microsoft.EntityFrameworkCore;
using PayableServices.Infrastructure;
using PayableServices.Persistence;
using Pagarte.Security;
using PayableServices.Api.Security;
using PayableServices.Application.Abstractions;

namespace PayableServices.Api;

public class Program
{
    public static async Task Main(string[] args) => await (await BuildAsync(args)).RunAsync();

    public static async Task<WebApplication> BuildAsync(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ApplicationName = typeof(Program).Assembly.GetName().Name });

        builder.Services.AddControllers();
        builder.Services.AddOpenApi();
        builder.Services.AddPayableServicesInfrastructure(builder.Configuration, builder.Environment);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentActorProvider, HttpCurrentActorProvider>();

        builder.Services.AddPagarteJwtSecurity(builder.Configuration, builder.Environment);
        builder.Services.AddClientIdentityResolution(builder.Configuration, builder.Environment);

        configure?.Invoke(builder);
        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PayableServicesDbContext>();
            await dbContext.Database.MigrateAsync();
            await PayableServicesDbSeeder.SeedAsync(dbContext);
        }

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseExceptionHandler(handler => handler.Run(async context =>
        {
            context.Response.StatusCode = 503;
            await context.Response.WriteAsJsonAsync(new { code = "SERVICE_UNAVAILABLE" });
        }));
        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseClientIdentityResolution();
        app.MapControllers();
        return app;
    }
}
