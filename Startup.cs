using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Text;
using AuthAPIwithController.Models;
using AuthService.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AuthAPIwithController;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        services.AddDbContext<AppDBContext>(options =>
            options.UseSqlite("DataSource=/tmp/appdata.db"));

        services.AddIdentity<User, IdentityRole>()
            .AddEntityFrameworkStores<AppDBContext>()
            .AddDefaultTokenProviders();

        var jwtKey = Configuration["Jwt:Key"];
        var jwtIssuer = Configuration["Jwt:Issuer"];

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey ?? string.Empty))
            };
        });

        services.AddAuthorization();
        services.AddScoped<IEmailService, EmailService>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
    {
        // Serilog request logging (captures request details)
        app.UseSerilogRequestLogging();

        // Global exception handler to ensure exceptions are logged to CloudWatch
        app.Use(async (context, next) =>
        {
            try
            {
                await next();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unhandled exception processing request");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("Internal Server Error");
            }
        });

        // Log startup diagnostic info
        try
        {
            var jwtKey = Configuration["Jwt:Key"];
            logger.LogInformation("Startup: Environment={Environment}, JwtKeyPresent={HasJwt}", env.EnvironmentName, !string.IsNullOrEmpty(jwtKey));

            // Apply EF migrations (SQLite file at /tmp/appdata.db in Lambda)
            using (var scope = app.ApplicationServices.CreateScope())
            {
                var db = scope.ServiceProvider.GetService<AppDBContext>();
                if (db != null)
                {
                    try
                    {
                        db.Database.Migrate();
                        logger.LogInformation("Database migrations applied. DB path=/tmp/appdata.db");
                    }
                    catch (Exception mex)
                    {
                        logger.LogError(mex, "Error applying database migrations");
                    }
                }
                else
                {
                    logger.LogWarning("AppDBContext not registered; skipping migrations");
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during startup diagnostics");
        }

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapGet("/", () => "API is running on Lambda");
            endpoints.MapGet("/health", () => Results.Ok("Healthy"));
        });
    }
}
