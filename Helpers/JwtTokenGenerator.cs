using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

public static class JwtTokenGenerator
{

    public class JwtTokenResult
    {
        public string AccessToken { get; set; }
        public DateTime AccessTokenExpires { get; set; }
        public string RefreshToken { get; set; }
        public DateTime RefreshTokenExpires { get; set; }
    }

    public static JwtTokenResult GenerateToken(string userName, IConfiguration configuration)
    {
        // 1. Claims
        var claims = new[]
        {
        new Claim(JwtRegisteredClaimNames.Sub, userName),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        new Claim("username", userName)
    };

        // 2. Validate key from configuration
        var keyString = configuration["Jwt:Key"] +"9730350945thisismylongkeytogeneratetokenforthidapplication";
        if (string.IsNullOrWhiteSpace(keyString) || keyString.Length < 32)
        {
            throw new Exception("JWT Key must be at least 32 characters long (256 bits).");
        }

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        // 3. Access token (10 minutes)
        var accessTokenExpiration = DateTime.UtcNow.AddMinutes(10);

        var jwtToken = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: accessTokenExpiration,
            signingCredentials: credentials
        );

        var accessToken = new JwtSecurityTokenHandler().WriteToken(jwtToken);

        // 4. Refresh token (secure random 32 bytes → Base64)
        var refreshToken = GenerateRefreshToken();
        var refreshTokenExpiration = DateTime.UtcNow.AddDays(7);

        // 5. Return combined token result
        return new JwtTokenResult
        {
            AccessToken = accessToken,
            AccessTokenExpires = accessTokenExpiration,
            RefreshToken = refreshToken,
            RefreshTokenExpires = refreshTokenExpiration
        };
    }

    /// <summary>
    /// Generates a cryptographically secure refresh token (Base64 encoded).
    /// </summary>
    private static string GenerateRefreshToken()
    {
        var randomBytes = new byte[32]; // 256-bit refresh token
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }

        return Convert.ToBase64String(randomBytes);
    }

}
