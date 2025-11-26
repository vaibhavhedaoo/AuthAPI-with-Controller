using AuthAPIwithController.Models;
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

    public static JwtTokenResult GenerateToken(User user, IList<string> roles, IConfiguration config)
    {
        // Load JWT settings correctly (using colon syntax)
        var key = config["Jwt:Key"];
        var issuer = config["Jwt:Issuer"];

        if (string.IsNullOrWhiteSpace(key))
            throw new Exception("Jwt:Key is missing in configuration.");

        if (string.IsNullOrWhiteSpace(issuer))
            throw new Exception("Jwt:Issuer is missing in configuration.");

        // Build Claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.UserName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        // Signing Key
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        // Expirations
        var accessTokenExpires = DateTime.UtcNow.AddHours(8);
        var refreshTokenExpires = DateTime.UtcNow.AddDays(7);

        // Create Access Token
        var jwtToken = new JwtSecurityToken(
            issuer: issuer,
            audience: issuer,
            claims: claims,
            expires: accessTokenExpires,
            signingCredentials: creds
        );

        var accessToken = new JwtSecurityTokenHandler().WriteToken(jwtToken);

        // Create Refresh Token
        var refreshToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

        // FINAL RESULT
        return new JwtTokenResult
        {
            AccessToken = accessToken,
            AccessTokenExpires = accessTokenExpires,
            RefreshToken = refreshToken,
            RefreshTokenExpires = refreshTokenExpires
        };
    }
}