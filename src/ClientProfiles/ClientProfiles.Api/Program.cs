
using ClientProfiles.Api.Infrastructure.Repository;
using ClientProfiles.Api.Interfaces;
using ClientProfiles.Api.Services;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Validation.AspNetCore;
using System.Text.Json.Serialization;
using ClientProfiles.Api.DTOs.Responses;

namespace ClientProfiles.Api;

public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("ClientProfiles.Api startup: creating builder.");

        var builder = WebApplication.CreateBuilder(args);
		var configuration = builder.Configuration;
		builder.AddServiceDefaults();
		Console.WriteLine("ClientProfiles.Api startup: service defaults configured.");

        // Add services to the container.
        builder.Services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
		});
		Console.WriteLine("ClientProfiles.Api startup: controllers configured.");

		// Configure Entity Framework Core with SQL Server
		builder.Services.AddDbContext<ClientProfilesDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("ClientProfilesDb")));

		// Register the Repositories
        builder.Services.AddScoped<IClientRepository, ClientRepository>();
        builder.Services.AddScoped<IPersonRepository, PersonRepository>();
        builder.Services.AddScoped<IOrganizationRepository, OrganizationRepository>();
		builder.Services.AddScoped<IAddressRepository, AddressRepository>();
        builder.Services.AddScoped<IPhoneRepository, PhoneRepository>();

		// Register the Services
        builder.Services.AddScoped<IClientService, ClientService>();
        builder.Services.AddScoped<IAddressService, AddressService>();
        builder.Services.AddScoped<IPhoneService, PhoneService>();

		//Register the mappers
        MappingConfig.Configure();
		Console.WriteLine("ClientProfiles.Api startup: mapping configured.");

		// Configure OpenIddict validation
		var authAuthority = configuration.GetValue<string>("AuthSettings:Authority")
			?? throw new InvalidOperationException("AuthSettings:Authority is not configured.");
		var authAudience = configuration.GetValue<string>("AuthSettings:Audience")
			?? throw new InvalidOperationException("AuthSettings:Audience is not configured.");
		Console.WriteLine($"ClientProfiles.Api startup: configuring OpenIddict authority '{authAuthority}' audience '{authAudience}'.");

		builder.Services.AddOpenIddict()
            .AddValidation(options =>
            {
				options.SetIssuer(authAuthority);
                options.AddAudiences(authAudience);
                options.UseSystemNetHttp();
                options.UseAspNetCore();
			});
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
        });


		// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
		builder.Services.AddOpenApi();

        Console.WriteLine("ClientProfiles.Api startup: building app.");
        var app = builder.Build();
		var logger = app.Services.GetRequiredService<ILogger<Program>>();
		logger.LogInformation(
			"Clients API configured with authority {Authority} and audience {Audience}.",
			authAuthority,
			authAudience);

        app.MapDefaultEndpoints();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();
		app.UseRouting();
		app.Use(async (context, next) =>
		{
			logger.LogInformation(
				"Clients request reached pre-auth middleware: {Method} {Path}. Authorization header present: {HasAuthorization}.",
				context.Request.Method,
				context.Request.Path,
				context.Request.Headers.ContainsKey("Authorization"));

			await next();

			logger.LogInformation(
				"Clients request completed after auth pipeline: {Method} {Path} => {StatusCode}.",
				context.Request.Method,
				context.Request.Path,
				context.Response.StatusCode);
		});
		app.UseAuthentication();
		app.UseAuthorization();
        app.MapControllers();
		Console.WriteLine("ClientProfiles.Api startup: running app.");
        app.Run();
    }
}
