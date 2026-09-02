using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RoomBooking.Application.Abstractions;

namespace RoomBooking.Api.Authentication;

internal sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string SigningKey { get; init; } = string.Empty;
    public int LifetimeMinutes { get; init; } = 30;
}

internal sealed class ChallengeUserOptions
{
    public const string SectionName = "ChallengeUsers";
    public string Password { get; init; } = string.Empty;
}

public sealed record ChallengeUser(
    Guid Id,
    string Username,
    string PasswordHash);

public interface IChallengeUserStore
{
    ChallengeUser? Authenticate(string username, string password);
}

internal sealed class ChallengeUserStore(
    IPasswordHasher<ChallengeUser> passwordHasher,
    IOptions<ChallengeUserOptions> options)
    : IChallengeUserStore
{
    private readonly IReadOnlyList<ChallengeUser> _users =
    [
        CreateUser(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "User1",
            options.Value.Password,
            passwordHasher),
        CreateUser(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "User2",
            options.Value.Password,
            passwordHasher)
    ];

    public ChallengeUser? Authenticate(
        string username,
        string password)
    {
        if (string.IsNullOrWhiteSpace(username)
            || string.IsNullOrEmpty(password))
        {
            return null;
        }

        var user = _users.SingleOrDefault(
            candidate => string.Equals(
                candidate.Username,
                username.Trim(),
                StringComparison.OrdinalIgnoreCase));

        if (user is null)
        {
            return null;
        }

        var result = passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            password);

        return result == PasswordVerificationResult.Failed
            ? null
            : user;
    }

    private static ChallengeUser CreateUser(
        Guid id,
        string username,
        string password,
        IPasswordHasher<ChallengeUser> hasher)
    {
        var user = new ChallengeUser(id, username, string.Empty);
        return user with
        {
            PasswordHash = hasher.HashPassword(user, password)
        };
    }
}

public interface IJwtTokenService
{
    IssuedToken CreateToken(ChallengeUser user);
}

public sealed record IssuedToken(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc);

internal sealed class JwtTokenService(
    IOptions<JwtOptions> options,
    TimeProvider timeProvider)
    : IJwtTokenService
{
    public IssuedToken CreateToken(ChallengeUser user)
    {
        var jwtOptions = options.Value;
        var nowUtc = timeProvider.GetUtcNow();
        var expiresAtUtc = nowUtc.AddMinutes(
            jwtOptions.LifetimeMinutes);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: jwtOptions.Audience,
            claims: claims,
            notBefore: nowUtc.UtcDateTime,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: credentials);
        return new IssuedToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAtUtc);
    }
}

internal sealed class HttpCurrentUser(
    IHttpContextAccessor httpContextAccessor)
    : ICurrentUser
{
    public Guid UserId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User
                .FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(value, out var userId)
                || userId == Guid.Empty)
            {
                throw new UnauthorizedAccessException(
                    "An authenticated user is required.");
            }
            return userId;
        }
    }
}
