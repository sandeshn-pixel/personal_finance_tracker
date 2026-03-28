using FluentValidation;
using FinanceTracker.Api.Options;
using FinanceTracker.Application.Auth.DTOs;
using FinanceTracker.Application.Auth.Exceptions;
using FinanceTracker.Application.Auth.Interfaces;
using FinanceTracker.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAuthService authService,
    IPasswordResetEmailSender passwordResetEmailSender,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    IValidator<ForgotPasswordRequest> forgotPasswordValidator,
    IValidator<ResetPasswordRequest> resetPasswordValidator,
    ICurrentUserService currentUserService,
    IOptions<JwtOptions> jwtOptions,
    IOptions<FrontendOptions> frontendOptions,
    IOptions<EmailOptions> emailOptions,
    IWebHostEnvironment environment,
    ILogger<AuthController> logger) : ControllerBase
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    private readonly FrontendOptions _frontendOptions = frontendOptions.Value;
    private readonly EmailOptions _emailOptions = emailOptions.Value;
    private readonly IWebHostEnvironment _environment = environment;

    [EnableRateLimiting("AuthSensitive")]
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

    [EnableRateLimiting("AuthSensitive")]
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

    [EnableRateLimiting("AuthSensitive")]
    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(ForgotPasswordResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await forgotPasswordValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return BuildValidationProblem(validationResult);
        }

        var token = await authService.RequestPasswordResetAsync(request, GetIpAddress(), GetUserAgent(), cancellationToken);
        var resetUrl = !string.IsNullOrWhiteSpace(token)
            ? BuildResetUrl(request.Email, token)
            : null;
        string? debugStatus = null;

        if (string.IsNullOrWhiteSpace(resetUrl))
        {
            logger.LogInformation("Password reset requested for {Email}, but no matching account was found.", request.Email.Trim());
            if (_environment.IsDevelopment())
            {
                debugStatus = "No matching account was found for this email.";
            }
        }

        if (!string.IsNullOrWhiteSpace(resetUrl))
        {
            if (!_emailOptions.Enabled)
            {
                if (_environment.IsDevelopment())
                {
                    debugStatus = "Email delivery is disabled locally. Use the development reset link below.";
                }
            }
            else
            {
                try
                {
                    await passwordResetEmailSender.SendResetLinkAsync(request.Email.Trim(), resetUrl, cancellationToken);

                    if (_environment.IsDevelopment())
                    {
                        debugStatus = "SMTP accepted the password reset email.";
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError("Unable to send password reset email for {Email}. Error: {Error}", request.Email.Trim(), ex.Message);

                    if (_environment.IsDevelopment())
                    {
                        debugStatus = $"Password reset email failed to send. {ex.Message}";
                    }
                }
            }
        }

        return Accepted(new ForgotPasswordResponse(
            "If the email exists, a password reset email has been prepared.",
            _environment.IsDevelopment() && !_emailOptions.Enabled ? resetUrl : null,
            _environment.IsDevelopment() ? debugStatus : null));
    }

    [EnableRateLimiting("AuthSensitive")]
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await resetPasswordValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return BuildValidationProblem(validationResult);
        }

        await authService.ResetPasswordAsync(request, cancellationToken);
        ClearRefreshCookie();
        return NoContent();
    }

    [EnableRateLimiting("AuthSession")]
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

    [EnableRateLimiting("AuthSession")]
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
    [EnableRateLimiting("AuthSession")]
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

    private string BuildResetUrl(string email, string token)
    {
        var frontendOrigin = _environment.IsDevelopment()
            ? "http://localhost:5173"
            : _frontendOptions.AllowedOrigins.FirstOrDefault(origin => Uri.TryCreate(origin, UriKind.Absolute, out _)) ?? "http://localhost:5173";

        return $"{frontendOrigin.TrimEnd('/')}/reset-password?email={Uri.EscapeDataString(email.Trim())}&token={Uri.EscapeDataString(token)}";
    }

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
        => SameSiteMode.None;

    private IActionResult BuildValidationProblem(FluentValidation.Results.ValidationResult validationResult)
    {
        foreach (var error in validationResult.Errors)
        {
            ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }

        return ValidationProblem(ModelState);
    }
}
