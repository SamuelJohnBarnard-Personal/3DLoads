using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ThreeDLoads.Api.Contracts;

namespace ThreeDLoads.Api.Controllers;

// Issues short-lived JWT bearer tokens for development/testing.
// Credentials and signing keys are read from configuration (Jwt:* section).
[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IConfiguration configuration) : ControllerBase
{
    [HttpPost("token")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<object> Token(CreateTokenRequest request)
    {
        var configuredUser = configuration["Jwt:DevUsername"] ?? "admin";
        var configuredPass = configuration["Jwt:DevPassword"] ?? "admin";

        if (!string.Equals(request.Username, configuredUser, StringComparison.Ordinal) ||
            !string.Equals(request.Password, configuredPass, StringComparison.Ordinal))
        {
            return Unauthorized();
        }

        var key = configuration["Jwt:Key"] ?? "replace-with-a-long-random-key-for-local-dev";
        var issuer = configuration["Jwt:Issuer"] ?? "3DLoads.Api";
        var audience = configuration["Jwt:Audience"] ?? "3DLoads.Client";

        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: new[]
            {
                new Claim(ClaimTypes.Name, request.Username),
                new Claim(ClaimTypes.Role, "SolverUser")
            },
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: signingCredentials);

        return Ok(new
        {
            token = new JwtSecurityTokenHandler().WriteToken(token)
        });
    }
}