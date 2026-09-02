using Microsoft.AspNetCore.Mvc;
using RoomBooking.Api.Authentication;
using RoomBooking.Api.Contracts;
using RoomBooking.Api.Controllers;

namespace RoomBooking.UnitTests.Api;

public sealed class AuthControllerTests
{
    private static readonly ChallengeUser User = new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "User1",
        "unused-test-hash");

    [Fact]
    public void Login_WithValidCredentials_ReturnsTokenAndUser()
    {
        var expiresAt = new DateTimeOffset(
            2026, 9, 2, 13, 0, 0, TimeSpan.Zero);
        var controller = new AuthController(
            new StubUserStore(User),
            new StubTokenService(
                new IssuedToken("test-token", expiresAt)));

        var action = controller.Login(
            new LoginRequest("User1", "test-password"));

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var response = Assert.IsType<LoginResponse>(ok.Value);
        Assert.Equal("test-token", response.AccessToken);
        Assert.Equal(expiresAt, response.ExpiresAtUtc);
        Assert.Equal(User.Id, response.User.Id);
        Assert.Equal("User1", response.User.Username);
    }

    [Fact]
    public void Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        var controller = new AuthController(
            new StubUserStore(null),
            new StubTokenService(
                new IssuedToken(
                    "unused",
                    DateTimeOffset.UtcNow)));

        var action = controller.Login(
            new LoginRequest("User1", "wrong"));

        var unauthorized = Assert.IsType<
            UnauthorizedObjectResult>(action.Result);
        var problem = Assert.IsType<ProblemDetails>(
            unauthorized.Value);
        Assert.Equal(
            "auth.invalid_credentials",
            problem.Extensions["code"]);
    }

    private sealed class StubUserStore(ChallengeUser? user)
        : IChallengeUserStore
    {
        public ChallengeUser? Authenticate(
            string username,
            string password)
        {
            return user;
        }
    }

    private sealed class StubTokenService(IssuedToken token)
        : IJwtTokenService
    {
        public IssuedToken CreateToken(ChallengeUser user)
        {
            return token;
        }
    }
}
