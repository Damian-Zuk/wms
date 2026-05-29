namespace Wms.Application.Common.Auth;

public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserName { get; }
}
