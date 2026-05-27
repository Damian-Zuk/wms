using Wms.Application.Common.Interfaces;

namespace Wms.Tests.Common;

public sealed class TestCurrentUserService : ICurrentUserService
{
    public string? UserId => "test-user-id";
    public string? UserName => "test-user";
}
