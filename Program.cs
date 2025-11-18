using Amazon;
using Amazon.CloudWatchLogs;
using Amazon.Runtime;
using AuthAPIwithController.Models;
using AuthService.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Formatting.Compact;
using Serilog.Sinks.MSSqlServer;
using System.Text;

// ------------------------------------------------------------
// EB FIX #1 → Bind to Elastic Beanstalk runtime port
// ------------------------------------------------------------
var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
Console.WriteLine($"Elastic Beanstalk PORT = {port}");
builder.WebHost.UseUrls($"http://*:{port}");

// ------------------------------------------------------------
// CLEAN & SAFE SERILOG (NO CLOUDWATCH)
// ------------------------------------------------------------
var loggerConfig = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("/var/log/app-log.txt", rollingInterval: RollingInterval.Day);

Log.Logger = loggerConfig.CreateLogger();
builder.Host.UseSerilog();

// ------------------------------------------------------------
// Services
// ------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "My API",
        Version = "v1"
    });

    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter token as: Bearer <token>"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// ------------------------------------------------------------
// Database (fixed path for Linux)
// ------------------------------------------------------------
builder.Services.AddDbContext<AppDBContext>(options =>
    options.UseSqlite("DataSource=/var/app/current/appdata.db"));

// ------------------------------------------------------------
// Identity
// ------------------------------------------------------------
builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<AppDBContext>()
.AddDefaultTokenProviders();

// ------------------------------------------------------------
// JWT Authentication
// ------------------------------------------------------------
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // EB FIX
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

builder.Services.AddAuthorization();
builder.Services.AddScoped<IEmailService, EmailService>();

// ------------------------------------------------------------
// Build app
// ------------------------------------------------------------
var app = builder.Build();

// Swagger (always enabled)
app.UseSwagger();
app.UseSwaggerUI();

// ------------------------------------------------------------
// EB FIX → disable HTTPS redirection on Elastic Beanstalk
// (HTTPS is terminated at the load balancer)
// ------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ------------------------------------------------------------
// Root & Health endpoints
// ------------------------------------------------------------
app.MapGet("/", () => "API is running on Elastic Beanstalk");
app.MapGet("/health", () => Results.Ok("Healthy"));

app.Run();
