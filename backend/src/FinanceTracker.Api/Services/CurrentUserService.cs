using System.Security.Claims;
using FinanceTracker.Application.Auth.Interfaces;

namespace FinanceTracker.Api.Services;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public Guid? UserId
    {
        get
        {
            var subject = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContextAccessor.HttpContext?.User.FindFirstValue("sub");

            return Guid.TryParse(subject, out var userId) ? userId : null;
        }
    }

    public bool IsAuthenticated => UserId.HasValue;
}
