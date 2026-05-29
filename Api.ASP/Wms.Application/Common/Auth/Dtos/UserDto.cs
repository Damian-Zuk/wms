namespace Wms.Application.Common.Auth.Dtos;

public record UserDto(
    string Id,
    string Email,
    string UserName,
    string FirstName,
    string LastName,
    IList<string> Roles);
