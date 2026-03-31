using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace Wms.Infrastructure.Identity;

public static class AdminSeeder
{
    public static async Task SeedAsync(
        UserManager<AppUser> userManager,
        IConfiguration configuration)
    {
        var section = configuration.GetSection("AdminAccount");

        var email = section["Email"]!;
        var userName = section["UserName"]!;
        var password = section["Password"]!;
        var firstName = section["FirstName"] ?? string.Empty;
        var lastName = section["LastName"] ?? string.Empty;

        if (await userManager.FindByEmailAsync(email) is not null)
            return;

        var admin = new AppUser
        {
            Email = email,
            UserName = userName,
            FirstName = firstName,
            LastName = lastName,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(admin, password);

        if (result.Succeeded)
            await userManager.AddToRoleAsync(admin, "Admin");
    }
}
