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
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<AuthController> _logger;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;

    public AuthController(UserManager<User> userManager, SignInManager<User> signInManager, ILogger<AuthController> logger, IEmailService emailService, IConfiguration config, RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
        _emailService = emailService;
        _config = config;
        _roleManager = roleManager;
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
        if (!await _roleManager.RoleExistsAsync("ADMIN"))
        {
            await _roleManager.CreateAsync(new IdentityRole("ADMIN"));
        }
        if (!await _roleManager.RoleExistsAsync("USER"))
        {
            await _roleManager.CreateAsync(new IdentityRole("USER"));
        }

        if (result.Succeeded && request.Role == "ADMIN")
        {
            await _userManager.AddToRoleAsync(user, "ADMIN");
        }
        else if (result.Succeeded && (request.Role!= "ADMIN" || string.IsNullOrEmpty(request.Role)))
        {
            await _userManager.AddToRoleAsync(user, "USER");
        }

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

        var user = await _userManager.FindByNameAsync(request.UserName);
        if (user == null)
            return Unauthorized(new { message = "Invalid username or password" });

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);

        if (!result.Succeeded)
            return Unauthorized(new { message = "Invalid username or password" });

        // Fetch roles for JWT
        var roles = await _userManager.GetRolesAsync(user);

        // Generate JWT with roles
        var token = JwtTokenGenerator.GenerateToken(user, roles, _config);

        return Ok(new
        {
            message = "Logged in successfully",
            userID = user.Id,
            accessToken = token.AccessToken,
            accessTokenExpires = token.AccessTokenExpires,
            refreshToken = token.RefreshToken,
            refreshTokenExpires = token.RefreshTokenExpires
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
