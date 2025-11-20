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
using System;
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

        // Detect if running in Lambda/container
        var isLambda = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME")) ||
                       !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"));

        if (isLambda)
        {
            // Use in-memory DB for Lambda/container
            services.AddDbContext<AppDBContext>(options =>
                options.UseInMemoryDatabase("AuthDb"));
        }
        else
        {
            // Use SQLite for local/EB
            services.AddDbContext<AppDBContext>(options =>
                options.UseSqlite("DataSource=/var/app/current/appdata.db"));
        }

        services.AddIdentity<User, IdentityRole>()
            .AddEntityFrameworkStores<AppDBContext>()
            .AddDefaultTokenProviders();

        var jwtKey = Configuration["Jwt:Key"];
        var jwtIssuer = Configuration["Jwt:Issuer"];

        if (!string.IsNullOrEmpty(jwtKey))
        {
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
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
                };
            });
        }
        else
        {
            Log.Warning("Jwt:Key not configured. Skipping JWT authentication setup.");
        }

        services.AddAuthorization();
        services.AddScoped<IEmailService, EmailService>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
    {
        app.UseSerilogRequestLogging();

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

        var isLambda = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME")) ||
                       !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"));

        try
        {
            var jwtKey = Configuration["Jwt:Key"];
            logger.LogInformation("Startup: Environment={Environment}, JwtKeyPresent={HasJwt}, IsLambda={IsLambda}", env.EnvironmentName, !string.IsNullOrEmpty(jwtKey), isLambda);

            if (!isLambda)
            {
                using (var scope = app.ApplicationServices.CreateScope())
                {
                    var db = scope.ServiceProvider.GetService<AppDBContext>();
                    if (db != null)
                    {
                        try
                        {
                            db.Database.Migrate();
                            logger.LogInformation("Database migrations applied. DB path=/var/app/current/appdata.db");
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
            else
            {
                logger.LogInformation("Running in Lambda/container; using InMemory database at runtime");
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

        var jwtConfigured = !string.IsNullOrEmpty(Configuration["Jwt:Key"]);
        if (jwtConfigured)
        {
            app.UseAuthentication();
        }

        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapGet("/", () => "API is running on Lambda/Container");
            endpoints.MapGet("/health", () => Results.Ok("Healthy"));
        });
    }
}
