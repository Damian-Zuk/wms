namespace Wms.Application.Common.Auth.Dtos;

public record RegisterRequest(
    string Email,
    string UserName,
    string Password,
    string FirstName,
    string LastName,
    string Role);
