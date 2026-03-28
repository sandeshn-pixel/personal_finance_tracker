using FinanceTracker.Application.Auth.Interfaces;
using FinanceTracker.Application.Settings.DTOs;
using FinanceTracker.Application.Settings.Interfaces;
using FinanceTracker.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/settings")]
public sealed class SettingsController(
    ISettingsService settingsService,
    ICurrentUserService currentUserService,
    IOptions<JwtOptions> jwtOptions,
    IWebHostEnvironment environment) : ControllerBase
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    private readonly IWebHostEnvironment _environment = environment;

    [HttpGet]
    [ProducesResponseType(typeof(UserSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!currentUserService.UserId.HasValue)
        {
            return Unauthorized();
        }

        return Ok(await settingsService.GetAsync(currentUserService.UserId.Value, cancellationToken));
    }

    [HttpPut("profile")]
    [ProducesResponseType(typeof(ProfileSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        if (!currentUserService.UserId.HasValue)
        {
            return Unauthorized();
        }

        return Ok(await settingsService.UpdateProfileAsync(currentUserService.UserId.Value, request, cancellationToken));
    }

    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (!currentUserService.UserId.HasValue)
        {
            return Unauthorized();
        }

        await settingsService.ChangePasswordAsync(currentUserService.UserId.Value, request, cancellationToken);
        return NoContent();
    }

    [HttpPut("preferences")]
    [ProducesResponseType(typeof(PreferenceSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdatePreferencesRequest request, CancellationToken cancellationToken)
    {
        if (!currentUserService.UserId.HasValue)
        {
            return Unauthorized();
        }

        return Ok(await settingsService.UpdatePreferencesAsync(currentUserService.UserId.Value, request, cancellationToken));
    }

    [HttpPut("notifications")]
    [ProducesResponseType(typeof(NotificationSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateNotifications([FromBody] UpdateNotificationSettingsRequest request, CancellationToken cancellationToken)
    {
        if (!currentUserService.UserId.HasValue)
        {
            return Unauthorized();
        }

        return Ok(await settingsService.UpdateNotificationsAsync(currentUserService.UserId.Value, request, cancellationToken));
    }

    [HttpPut("financial-defaults")]
    [ProducesResponseType(typeof(FinancialDefaultsSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateFinancialDefaults([FromBody] UpdateFinancialDefaultsRequest request, CancellationToken cancellationToken)
    {
        if (!currentUserService.UserId.HasValue)
        {
            return Unauthorized();
        }

        return Ok(await settingsService.UpdateFinancialDefaultsAsync(currentUserService.UserId.Value, request, cancellationToken));
    }

    [HttpGet("sample-data-status")]
    [ProducesResponseType(typeof(SampleDataSeedStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSampleDataStatus(CancellationToken cancellationToken)
    {
        if (!currentUserService.UserId.HasValue)
        {
            return Unauthorized();
        }

        return Ok(await settingsService.GetSampleDataSeedStatusAsync(currentUserService.UserId.Value, cancellationToken));
    }

    [HttpPost("sample-data")]
    [ProducesResponseType(typeof(SeedSampleDataResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SeedSampleData(CancellationToken cancellationToken)
    {
        if (!currentUserService.UserId.HasValue)
        {
            return Unauthorized();
        }

        return Ok(await settingsService.SeedSampleDataAsync(currentUserService.UserId.Value, cancellationToken));
    }

    [HttpPost("logout-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        if (!currentUserService.UserId.HasValue)
        {
            return Unauthorized();
        }

        await settingsService.LogoutAllSessionsAsync(currentUserService.UserId.Value, cancellationToken);
        ClearRefreshCookie();
        return NoContent();
    }

    private void ClearRefreshCookie()
    {
        Response.Cookies.Delete(_jwtOptions.RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = _environment.IsDevelopment() ? SameSiteMode.None : SameSiteMode.Strict,
            Path = "/api/auth"
        });
    }
}
