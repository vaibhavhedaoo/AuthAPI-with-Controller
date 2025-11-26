using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Text;
using AuthService.Data;
using AuthAPIwithController.Models;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

// ------------------------------------------------------------
// 🔥 FIX: FORCE CONFIG RELOAD + ENV VARS HAVE PRIORITY
// ------------------------------------------------------------
builder.Configuration.Sources.Clear();
builder.Configuration
       .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
       .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
       .AddEnvironmentVariables();

// ------------------------------------------------------------
// DEBUG ENVIRONMENT VARIABLE LOADING
// ------------------------------------------------------------
Console.WriteLine("DEBUG ENV Jwt__Key       = " + Environment.GetEnvironmentVariable("Jwt__Key"));
Console.WriteLine("DEBUG ENV Jwt__Issuer    = " + Environment.GetEnvironmentVariable("Jwt__Issuer"));
Console.WriteLine("DEBUG CONFIG Jwt__Key    = " + builder.Configuration["Jwt__Key"]);
Console.WriteLine("DEBUG CONFIG Jwt__Issuer = " + builder.Configuration["Jwt__Issuer"]);

// ------------------------------------------------------------
// PORT (Elastic Beanstalk / Docker / Linux)
// ------------------------------------------------------------
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseUrls($"http://*:{port}");

// ------------------------------------------------------------
// LOGGING (Serilog)
// ------------------------------------------------------------
var logFolder = "/var/app/current/logs";
Directory.CreateDirectory(logFolder);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File($"{logFolder}/app-log.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// ------------------------------------------------------------
// SERVICES
// ------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Auth API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        In = ParameterLocation.Header
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[0]
        }
    });
});

// ------------------------------------------------------------
// DATABASE
// ------------------------------------------------------------
var connectionString = Environment.GetEnvironmentVariable("DefaultConnection");

builder.Services.AddDbContext<AppDBContext>(options =>
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(8, 0, 36)),
        mySqlOptions => mySqlOptions.EnableRetryOnFailure()
    )
);

// ------------------------------------------------------------
// IDENTITY
// ------------------------------------------------------------
builder.Services
    .AddIdentity<User, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 6;
    })
    .AddEntityFrameworkStores<AppDBContext>()
    .AddDefaultTokenProviders();

// ------------------------------------------------------------
// JWT AUTHENTICATION (Environment Variables)
// ------------------------------------------------------------
var jwtKey = Environment.GetEnvironmentVariable("Jwt__Key");
var jwtIssuer = Environment.GetEnvironmentVariable("Jwt__Issuer");
Console.WriteLine("JWT Issuer: " + jwtIssuer);

if (string.IsNullOrWhiteSpace(jwtKey))
    throw new Exception("Missing environment variable: Jwt__Key");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;

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


builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ADMIN", policy =>
        policy.RequireRole("ADMIN"));

    options.AddPolicy("USER", policy =>
        policy.RequireRole("USER"));
});
builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, CustomAuthorizationHandler>();

// ------------------------------------------------------------
// BUILD PIPELINE
// ------------------------------------------------------------
var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

//if (app.Environment.IsDevelopment())
//{
//    app.UseHttpsRedirection();
//}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok("Healthy"));
app.UseCors();
app.Run();
