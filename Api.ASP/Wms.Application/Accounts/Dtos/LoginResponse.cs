
namespace Wms.Application.Accounts.Dtos;

public record LoginResponse(string Token, DateTime ExpiresAt, UserDto User);
