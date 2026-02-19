using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using CVAPI.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Azure.Security.KeyVault.Secrets;
using Azure.Identity;

public class JwtTokenService
{
    private readonly string _secret;
    private readonly string _issuer;
    private readonly SecretClient _secretClient;
    private readonly ILogger<JwtTokenService> _logger;

    public JwtTokenService(IConfiguration configuration, ILogger<JwtTokenService> logger)
    {
        _logger = logger;
        var keyVaultUrl = configuration["KeyVault:Url"];
        _secretClient = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());

        // Use the same secret names as in Program.cs
        _secret = GetSecretFromKeyVault("jwt-secret-secret");
        _issuer = GetSecretFromKeyVault("jwt-issuer");
    }

    private string GetSecretFromKeyVault(string secretName)
    {
        try
        {
            var secret = _secretClient.GetSecret(secretName);
            return secret.Value.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve {SecretName} from Key Vault", secretName);
            throw new InvalidOperationException($"Could not retrieve secret '{secretName}' from Key Vault", ex);
        }
    }

    public string GenerateJwtToken(User user)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        // Add AdminInitials only if the user is a Manager or Admin
        string adminInitials = string.Empty;
        if (user is Manager manager)
        {
            adminInitials = manager.AdminInitials;
        }
        else if (user is Admin admin)
        {
            adminInitials = admin.AdminInitials;
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId),
            new Claim(ClaimTypes.Role, user.UserRole.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        // Add the AdminInitials claim only if it is not empty
        if (!string.IsNullOrEmpty(adminInitials))
        {
            claims.Add(new Claim("initials", adminInitials));
        }

        var token = new JwtSecurityToken(
            issuer: _issuer,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public ClaimsPrincipal DecodeJwtToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Attempted to decode null or empty token");
            return null;
        }

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_secret);

        try
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key)
            };

            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out _);
            _logger.LogDebug("JWT Token successfully validated");
            return principal;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JWT validation failed");
            return null;
        }
    }

    public string GetUserIdFromToken(string token)
    {
        var principal = DecodeJwtToken(token);
        return principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    public Role? GetUserRoleFromToken(string token)
    {
        var principal = DecodeJwtToken(token);
        var roleClaim = principal?.FindFirst(ClaimTypes.Role)?.Value;

        if (Enum.TryParse(roleClaim, out Role role))
        {
            return role;
        }

        return null;
    }
}
