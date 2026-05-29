using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Wms.Application.Common.Auth;

namespace Wms.Infrastructure.Identity;

public class CurrentUserService : ICurrentUserService
{
    public string? UserId { get; }
    public string? UserName { get; }

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        var user = httpContextAccessor.HttpContext?.User;
        UserId = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        UserName = user?.FindFirstValue(ClaimTypes.Name);
    }
}
