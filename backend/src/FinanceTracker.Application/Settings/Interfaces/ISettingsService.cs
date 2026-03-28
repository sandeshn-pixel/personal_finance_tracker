using FinanceTracker.Application.Settings.DTOs;

namespace FinanceTracker.Application.Settings.Interfaces;

public interface ISettingsService
{
    Task<UserSettingsDto> GetAsync(Guid userId, CancellationToken cancellationToken);
    Task<ProfileSettingsDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken);
    Task<PreferenceSettingsDto> UpdatePreferencesAsync(Guid userId, UpdatePreferencesRequest request, CancellationToken cancellationToken);
    Task<NotificationSettingsDto> UpdateNotificationsAsync(Guid userId, UpdateNotificationSettingsRequest request, CancellationToken cancellationToken);
    Task<FinancialDefaultsSettingsDto> UpdateFinancialDefaultsAsync(Guid userId, UpdateFinancialDefaultsRequest request, CancellationToken cancellationToken);
    Task<SampleDataSeedStatusDto> GetSampleDataSeedStatusAsync(Guid userId, CancellationToken cancellationToken);
    Task<SeedSampleDataResultDto> SeedSampleDataAsync(Guid userId, CancellationToken cancellationToken);
    Task LogoutAllSessionsAsync(Guid userId, CancellationToken cancellationToken);
}
