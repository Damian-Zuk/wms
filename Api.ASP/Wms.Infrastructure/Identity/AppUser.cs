using Microsoft.AspNetCore.Identity;

namespace Wms.Infrastructure.Identity;

public class AppUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}
