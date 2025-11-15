using AuthAPIwithController.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly ILogger<AuthController> _logger;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;

    public AuthController(UserManager<User> userManager, SignInManager<User> signInManager, ILogger<AuthController> logger, IEmailService emailService, IConfiguration config)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
        _emailService = emailService;
        _config = config;
    }

    // -------------------------
    // Register
    // -------------------------
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        _logger.LogInformation("Register started with request: {@Request}", request);
        var user = new User
        {
            UserName = request.UserName,
            Email = request.Email
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {

            _logger.LogError("Bad Request", request);
            return BadRequest(result.Errors);
        }


        var emailBody = $"<h1>Welcome {request.Email}</h1><p>Thank you for registering!</p>";
        //await _emailService.SendEmailAsync(request.Email, "Welcome to Our App", emailBody);
        // You can add custom behavior here (send email, log, etc.)
        return Ok(new { message = "User registered successfully" });
    }

    // -------------------------
    // Login
    // -------------------------
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        _logger.LogInformation("Login request received...");
        var result = await _signInManager.PasswordSignInAsync(request.UserName, request.Password, false, false);

        if (!result.Succeeded)
            return Unauthorized(new { message = "Invalid credentials" });

        // Generate JWT
        //var jwtKey = builder.Configuration["Jwt:Key"];// "SuperSecretKey12345"; // or read from configuration
        //var jwtIssuer = builder.Configuration["Jwt:Key"]; // or read from configuration
        var token = JwtTokenGenerator.GenerateToken(request.UserName, _config);

        return Ok(new
        {
            message = "Logged in successfully",
            token
        });
    }
    // -------------------------
    // Logout
    // -------------------------
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Ok(new { message = "Logged out successfully" });
    }

    // -------------------------
    // Change password
    // -------------------------
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var user = await _userManager.FindByNameAsync(request.UserName);
        if (user == null) return NotFound();

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

        if (!result.Succeeded) return BadRequest(result.Errors);

        return Ok(new { message = "Password changed successfully" });
    }
}
