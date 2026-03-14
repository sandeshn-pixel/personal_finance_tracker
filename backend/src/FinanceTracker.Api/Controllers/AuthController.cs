using FluentValidation;
using FinanceTracker.Application.Auth.DTOs;
using FinanceTracker.Application.Auth.Exceptions;
using FinanceTracker.Application.Auth.Interfaces;
using FinanceTracker.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAuthService authService,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    ICurrentUserService currentUserService,
    IOptions<JwtOptions> jwtOptions,
    IWebHostEnvironment environment) : ControllerBase
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    private readonly IWebHostEnvironment _environment = environment;

    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await registerValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return BuildValidationProblem(validationResult);
        }

        try
        {
            var result = await authService.RegisterAsync(request, GetIpAddress(), GetUserAgent(), cancellationToken);
            AppendRefreshCookie(result.RefreshToken, result.RefreshTokenExpiresUtc);
            return StatusCode(StatusCodes.Status201Created, result.Response);
        }
        catch (AuthException ex)
        {
            return Conflict(new ProblemDetails { Title = "Registration failed.", Detail = ex.Message, Status = StatusCodes.Status409Conflict });
        }
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await loginValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return BuildValidationProblem(validationResult);
        }

        try
        {
            var result = await authService.LoginAsync(request, GetIpAddress(), GetUserAgent(), cancellationToken);
            AppendRefreshCookie(result.RefreshToken, result.RefreshTokenExpiresUtc);
            return Ok(result.Response);
        }
        catch (AuthException)
        {
            return Unauthorized(new ProblemDetails { Title = "Authentication failed.", Detail = "Invalid credentials.", Status = StatusCodes.Status401Unauthorized });
        }
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[_jwtOptions.RefreshCookieName];
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Unauthorized(new ProblemDetails { Title = "Session unavailable.", Detail = "Refresh token is missing.", Status = StatusCodes.Status401Unauthorized });
        }

        try
        {
            var result = await authService.RefreshAsync(refreshToken, GetIpAddress(), GetUserAgent(), cancellationToken);
            AppendRefreshCookie(result.RefreshToken, result.RefreshTokenExpiresUtc);
            return Ok(result.Response);
        }
        catch (AuthException)
        {
            ClearRefreshCookie();
            return Unauthorized(new ProblemDetails { Title = "Session expired.", Detail = "Please sign in again.", Status = StatusCodes.Status401Unauthorized });
        }
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[_jwtOptions.RefreshCookieName];
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            await authService.RevokeAsync(refreshToken, cancellationToken);
        }

        ClearRefreshCookie();
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(AuthenticatedUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        if (!currentUserService.UserId.HasValue)
        {
            return Unauthorized();
        }

        var user = await authService.GetCurrentUserAsync(currentUserService.UserId.Value, cancellationToken);
        return user is null ? Unauthorized() : Ok(user);
    }

    private string? GetIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private string? GetUserAgent() => Request.Headers.UserAgent.ToString();

    private void AppendRefreshCookie(string refreshToken, DateTime expiresUtc)
    {
        Response.Cookies.Append(_jwtOptions.RefreshCookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = GetRefreshCookieSameSite(),
            Expires = expiresUtc,
            IsEssential = true,
            Path = "/api/auth"
        });
    }

    private void ClearRefreshCookie()
    {
        Response.Cookies.Delete(_jwtOptions.RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = GetRefreshCookieSameSite(),
            Path = "/api/auth"
        });
    }

    private SameSiteMode GetRefreshCookieSameSite()
        => _environment.IsDevelopment() ? SameSiteMode.None : SameSiteMode.Strict;

    private IActionResult BuildValidationProblem(FluentValidation.Results.ValidationResult validationResult)
    {
        foreach (var error in validationResult.Errors)
        {
            ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }

        return ValidationProblem(ModelState);
    }
}
