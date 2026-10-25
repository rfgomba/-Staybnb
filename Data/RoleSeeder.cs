using Microsoft.AspNetCore.Identity;
using Staybnb.Models;

namespace Staybnb.Data;

public static class RoleSeeder
{
    private static readonly string[] Roles =
        { AppRoles.SuperAdmin, AppRoles.Admin, AppRoles.Host, AppRoles.Guest };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var config = services.GetRequiredService<IConfiguration>();
        var db = services.GetRequiredService<ApplicationDbContext>();

        // 1. Roles
        foreach (var role in Roles)
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));

        // 2. SuperAdmin account
        var email = config["SuperAdmin:Email"] ?? "superadmin@staybnb.co.za";
        var password = config["SuperAdmin:Password"] ?? "SuperAdmin@123";

        var superAdmin = await userManager.FindByEmailAsync(email);
        if (superAdmin == null)
        {
            superAdmin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = "Super",
                LastName = "Admin"
            };
            var result = await userManager.CreateAsync(superAdmin, password);
            if (!result.Succeeded)
                throw new Exception("SuperAdmin seed failed: " +
                    string.Join("; ", result.Errors.Select(e => e.Description)));

            db.ActivityLogs.Add(new ActivityLog
            {
                UserId = superAdmin.Id,
                Action = "SuperAdminSeeded",
                ActivityType = ActivityType.System
            });
            await db.SaveChangesAsync();
        }

        // SuperAdmin also gets Admin so it can use the Admin dashboard
        foreach (var role in new[] { AppRoles.SuperAdmin, AppRoles.Admin })
            if (!await userManager.IsInRoleAsync(superAdmin, role))
                await userManager.AddToRoleAsync(superAdmin, role);

        // 3. Amenities
        if (!db.Amenities.Any())
        {
            db.Amenities.AddRange(
                new Amenity { Name = "Wifi", IconClass = "fa-solid fa-wifi" },
                new Amenity { Name = "Kitchen", IconClass = "fa-solid fa-utensils" },
                new Amenity { Name = "Washer", IconClass = "fa-solid fa-shirt" },
                new Amenity { Name = "Dryer", IconClass = "fa-solid fa-wind" },
                new Amenity { Name = "Air Conditioning", IconClass = "fa-solid fa-snowflake" },
                new Amenity { Name = "Heating", IconClass = "fa-solid fa-fire" },
                new Amenity { Name = "Free Parking", IconClass = "fa-solid fa-car" },
                new Amenity { Name = "Pool", IconClass = "fa-solid fa-water-ladder" },
                new Amenity { Name = "TV", IconClass = "fa-solid fa-tv" },
                new Amenity { Name = "Braai Area", IconClass = "fa-solid fa-fire-burner" });
            await db.SaveChangesAsync();
        }
    }
}
