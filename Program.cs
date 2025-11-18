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
using Serilog.Sinks.AwsCloudWatch;
using Serilog.Sinks.MSSqlServer;
using System.Text;

// ------------------------------------------------------------
// EB FIX #1 → Bind to Elastic Beanstalk runtime port (PORT var)
// ------------------------------------------------------------
var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
Console.WriteLine($"Elastic Beanstalk PORT = {port}");
builder.WebHost.UseUrls($"http://*:{port}");

// ------------------------------------------------------------
// Read AWS settings (AccessKey/SecretKey are OPTIONAL on EB)
// ------------------------------------------------------------
var awsAccessKey = builder.Configuration["AWS:AccessKey"];
var awsSecretKey = builder.Configuration["AWS:SecretKey"];
var awsRegion = builder.Configuration["AWS:Region"];

// ------------------------------------------------------------
// Configure Serilog
// ------------------------------------------------------------
var writeTo = builder.Configuration["Logging:WriteTo"];
var loggerConfig = new LoggerConfiguration().MinimumLevel.Debug();

switch (writeTo)
{
    case "Console":
        loggerConfig.WriteTo.Console();
        break;

    case "File":
        // EB FIX → Linux path instead of Windows
        loggerConfig.WriteTo.File(
            "/var/log/app-log.txt",
            rollingInterval: RollingInterval.Day,
            restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information
        );
        break;

    case "Database":
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        loggerConfig.WriteTo.MSSqlServer(
            connectionString: connectionString,
            sinkOptions: new MSSqlServerSinkOptions
            {
                TableName = "Logs",
                AutoCreateSqlTable = true
            },
            restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information
        );
        break;

    case "CloudWatch":
        // EB FIX #2 → Use instance profile (IAM role) NOT AccessKey in JSON
        var cwClient = new AmazonCloudWatchLogsClient(
            RegionEndpoint.GetBySystemName(awsRegion)
        );

        var cloudWatchOptions = new CloudWatchSinkOptions
        {
            LogGroupName = "AuthAPI-Logs",
            LogStreamNameProvider = new DefaultLogStreamProvider(),
            MinimumLogEventLevel = Serilog.Events.LogEventLevel.Information,
            CreateLogGroup = true
        };

        loggerConfig.WriteTo.AmazonCloudWatch(cloudWatchOptions, cwClient);
        break;

    default:
        loggerConfig.WriteTo.Console();
        break;
}

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
// Database
// ------------------------------------------------------------
builder.Services.AddDbContext<AppDBContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

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
    // EB FIX #3 → Disable HTTPS metadata requirement
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
// Build application
// ------------------------------------------------------------
var app = builder.Build();

// Swagger
app.UseSwagger();
app.UseSwaggerUI();

// ------------------------------------------------------------
// EB FIX #4 → Disable HTTPS redirection on EB
// EB ALB terminates HTTPS, backend MUST accept HTTP
// ------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection(); // only for local
}

// Authentication/Authorization
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ------------------------------------------------------------
// EB FIX #5 → Root & Health endpoints
// ------------------------------------------------------------
app.MapGet("/", () => "API is running on Elastic Beanstalk");
app.MapGet("/health", () => Results.Ok("Healthy"));

app.Run();
