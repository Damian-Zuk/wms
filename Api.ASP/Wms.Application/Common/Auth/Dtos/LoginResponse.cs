namespace Wms.Application.Common.Auth.Dtos;

public record LoginResponse(string Token, DateTime ExpiresAt, UserDto User);
