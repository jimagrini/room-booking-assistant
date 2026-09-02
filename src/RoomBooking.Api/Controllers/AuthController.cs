using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomBooking.Api.Authentication;
using RoomBooking.Api.Contracts;

namespace RoomBooking.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IChallengeUserStore userStore,
    IJwtTokenService tokenService)
    : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public ActionResult<LoginResponse> Login(LoginRequest request)
    {
        var user = userStore.Authenticate(
            request.Username,
            request.Password);
        if (user is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Authentication failed.",
                Detail = "The username or password is invalid.",
                Extensions =
                {
                    ["code"] = "auth.invalid_credentials"
                }
            });
        }

        var token = tokenService.CreateToken(user);
        return Ok(new LoginResponse(
            token.AccessToken,
            token.ExpiresAtUtc,
            new AuthenticatedUserDto(user.Id, user.Username)));
    }
}
