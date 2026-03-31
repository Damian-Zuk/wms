
namespace Wms.Application.Accounts.Dtos;

public record RegisterRequest(
    string Email,
    string UserName,
    string Password,
    string FirstName,
    string LastName,
    string Role);
