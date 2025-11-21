using Amazon;
using AuthAPIwithController.Models;
using AuthService.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;

// ------------------------------------------------------------
// EB FIX #1 → Bind to Elastic Beanstalk runtime port
// ------------------------------------------------------------
var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
Console.WriteLine($"Elastic Beanstalk PORT = {port}");
builder.WebHost.UseUrls($"http://*:{port}");

// ------------------------------------------------------------
// SAFE SERILOG (NO CLOUDWATCH SINK, NO CRASH)
// ------------------------------------------------------------
var logFolder = "/var/app/current/logs";
var logFile = $"{logFolder}/app-log.txt";
Directory.CreateDirectory(logFolder);

var loggerConfig = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File(
        path: logFile,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7
    );

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
        In = Microsoft.OpenApi.Models.ParameterLocation.Header
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
// Database (Linux path fixed for EB)
// ------------------------------------------------------------
builder.Services.AddDbContext<AppDBContext>(options =>
    options.UseSqlite("DataSource=/var/app/current/appdata.db"));

// ------------------------------------------------------------
// Identity
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
// JWT Auth
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

builder.Services.AddAuthorization();
builder.Services.AddScoped<IEmailService, EmailService>();

// ------------------------------------------------------------
// Build App
// ------------------------------------------------------------
var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// ------------------------------------------------------------
// EB FIX: disable HTTPS redirection (ALB already uses HTTPS)
// ------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ------------------------------------------------------------
// EB Health Endpoints
// ------------------------------------------------------------
app.MapGet("/", () => "API is running on Elastic Beanstalk");
app.MapGet("/health", () => Results.Ok("Healthy"));

app.Run();
