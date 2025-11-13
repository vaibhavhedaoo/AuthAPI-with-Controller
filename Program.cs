using AuthAPIwithController.Models;
using AuthService.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Sinks.MSSqlServer;
using Serilog.Sinks.AwsCloudWatch;
using Amazon.CloudWatchLogs;
using Amazon;
using Amazon.Extensions.NETCore.Setup;
using System.Text;

// ---------- Build app ----------
var builder = WebApplication.CreateBuilder(args);

// ---------- Configure Serilog ----------
var writeTo = builder.Configuration["Logging:WriteTo"];
var loggerConfig = new LoggerConfiguration()
    .MinimumLevel.Debug();

switch (writeTo)
{
    case "Console":
        loggerConfig.WriteTo.Console();
        break;
    case "File":
        loggerConfig.WriteTo.File("logs/app_log_.txt", rollingInterval: RollingInterval.Day);
        break;
    case "Database":
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        loggerConfig.WriteTo.MSSqlServer(
            connectionString: connectionString,
            sinkOptions: new MSSqlServerSinkOptions
            {
                TableName = "Logs",
                AutoCreateSqlTable = true
            });
        break;
    case "CloudWatch":
        var awsOptions = builder.Configuration.GetAWSOptions();
        var client = awsOptions.CreateServiceClient<IAmazonCloudWatchLogs>();
        loggerConfig.WriteTo.AmazonCloudWatch(
            logGroup: "AuthAPI-Logs",
            logStreamPrefix: "api",
            cloudWatchClient: client);
        break;
    default:
        loggerConfig.WriteTo.Console();
        break;
}

Log.Logger = loggerConfig.CreateLogger();
builder.Host.UseSerilog();

// ---------- Add Services ----------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---------- Database ----------
builder.Services.AddDbContext<AppDBContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// ---------- Identity ----------
builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<AppDBContext>()
.AddDefaultTokenProviders();

// ---------- JWT Authentication ----------
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
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

// ---------- Build & Run ----------
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
