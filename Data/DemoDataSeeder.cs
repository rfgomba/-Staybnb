using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Staybnb.Models;
using Staybnb.Services;

namespace Staybnb.Data;

/// <summary>
/// Seeds a realistic demo marketplace (users in every role, listings, bookings in every status,
/// a check-in awaiting verification, reviews, messages, notifications and activity logs).
/// Runs once: it skips itself if the demo host already exists. Disable with "SeedDemoData": false.
/// </summary>
public static class DemoDataSeeder
{
    public const string DemoPassword = "Demo@123";
    private const string MarkerEmail = "elsie@staybnb.co.za";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var bookings = services.GetRequiredService<IBookingService>();
        var files = services.GetRequiredService<IFileStorageService>();

        // Databases seeded by the earlier version get the new titles and photos
        await RefreshLegacyDemoListingsAsync(db);

        if (await users.FindByEmailAsync(MarkerEmail) != null)
        {
            Console.WriteLine("[Staybnb] Demo data already present.");
            return;
        }
        Console.WriteLine("[Staybnb] Seeding demo data...");

        // ---------- Users ----------
        var lunga = await CreateUserAsync(users, "Lunga", "Aphane", "lunga@staybnb.co.za", AppRoles.Admin, 60);
        var elsie = await CreateUserAsync(users, "Elsie", "Masuku", MarkerEmail, AppRoles.Host, 55);
        var neroshen = await CreateUserAsync(users, "Neroshen", "Govender", "neroshen@staybnb.co.za", AppRoles.Host, 50);
        var muzi = await CreateUserAsync(users, "Muzi", "Mango", "muzi@staybnb.co.za", AppRoles.Guest, 20);
        var banele = await CreateUserAsync(users, "Banele", "Mthembu", "banele@staybnb.co.za", AppRoles.Guest, 45);
        var sboniso = await CreateUserAsync(users, "Sboniso", "Mango", "sboniso@staybnb.co.za", AppRoles.Guest, 40);
        var nthabiseng = await CreateUserAsync(users, "Nthabiseng", "Maluleke", "nthabiseng@staybnb.co.za", AppRoles.Guest, 30);

        // ---------- Properties ----------
        var amenities = await db.Amenities.ToDictionaryAsync(a => a.Name);
        List<Amenity> Pick(params string[] names) => names.Where(amenities.ContainsKey).Select(n => amenities[n]).ToList();
        var steps = CheckInStep.ToJson(CheckInStep.Defaults());

        HostProperty Listing(ApplicationUser host, string title, string type, string city, string address, decimal price,
            decimal cleaning, int guests, int bedrooms, int beds, int baths, List<Amenity> am, string description, params string[] images) =>
            new()
            {
                HostId = host.Id, Title = title, PropertyType = type, City = city, Address = address,
                PricePerNight = price, CleaningFee = cleaning, MaxGuests = guests, Bedrooms = bedrooms, Beds = beds,
                Bathrooms = baths, Amenities = am, Description = description, IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-40),
                Images = images.Select(i => new PropertyImage { ImageUrl = $"/images/demo/{i}" }).ToList(),
                CheckInProcess = new CheckInProcess { Title = $"Check-in for {title}", StepsJson = steps }
            };

        var cabin = Listing(elsie, "Sunny Garden Townhouse", "House", "Durban", "12 Acacia Close, Hillcrest", 1450, 250, 4, 2, 3, 2,
            Pick("Wifi", "Kitchen", "Washer", "Free Parking", "TV"),
            "A bright single-storey townhouse in a quiet, secure complex. Covered parking at the door, a walled garden and a sunny patio for morning coffee.",
            "townhouse.jpg", "simplex-entrance.jpg", "patio-view.jpg");
        var apartment = Listing(elsie, "Modern Brick Duplex", "Apartment", "Umhlanga", "5 Palm Grove, Umhlanga Rocks", 1780, 250, 5, 3, 3, 2,
            Pick("Wifi", "Kitchen", "Air Conditioning", "TV", "Free Parking"),
            "A face-brick duplex with a private covered patio opening onto a leafy garden. Upstairs balcony, secure parking and close to the beach.",
            "duplex-patio.jpg", "patio-view.jpg", "garden-side.jpg");
        var cottage = Listing(elsie, "Leafy Garden Cottage", "Cottage", "Ballito", "21 Dolphin Crescent, Ballito", 2100, 300, 4, 2, 2, 1,
            Pick("Wifi", "Kitchen", "Braai Area", "Free Parking", "TV"),
            "A peaceful brick cottage surrounded by mature fruit trees, with a stepping-stone garden path and a shaded outdoor area.",
            "garden-side.jpg", "patio-view.jpg");
        var villa = Listing(neroshen, "Palm Court Villa", "Villa", "Pretoria", "3 Palm Court, Faerie Glen", 2400, 400, 6, 3, 4, 3,
            Pick("Wifi", "Pool", "Kitchen", "Heating", "Braai Area", "Free Parking"),
            "A spacious double-storey villa behind a private walled courtyard, shaded by tall palms. Double garage, balcony and a large garden with a covered patio.",
            "palm-court.jpg", "duplex-patio.jpg", "garden-side.jpg", "patio-view.jpg");
        var house = Listing(neroshen, "Family Home with Double Garage", "House", "Nelspruit", "8 Jacaranda Street, Sonheuwel", 1150, 150, 6, 3, 3, 2,
            Pick("Wifi", "Kitchen", "Washer", "Free Parking", "Braai Area"),
            "A family-friendly double-storey home with a double garage, paved driveway and a big garden, close to the Kruger gates and Riverside Mall.",
            "garage-front.jpg", "garden-side.jpg", "patio-view.jpg");
        var bush = Listing(neroshen, "Quiet Simplex in Secure Estate", "House", "Hazyview", "Unit 14, Aloe Estate, Hazyview", 980, 200, 4, 2, 2, 1,
            Pick("Wifi", "Braai Area", "Free Parking", "Air Conditioning"),
            "A neat simplex with a carport and walled garden in a gated estate. Perfect for a quiet weekend away.",
            "simplex-entrance.jpg", "townhouse.jpg");

        // Pending application from a Guest, ready for an Admin to approve (shows the Guest to Host swap)
        var karoo = Listing(muzi, "Hillside Garden Guesthouse", "Guesthouse", "Bloemfontein", "17 Hill Street, Westdene", 950, 150, 4, 2, 3, 1,
            Pick("Wifi", "Kitchen", "Braai Area", "Free Parking"),
            "A welcoming guesthouse with a private garden patio, secure parking and easy access to the city centre.",
            "patio-view.jpg", "garden-side.jpg");
        karoo.IsActive = false;
        karoo.CheckInProcess = null;

        var live = new[] { cabin, apartment, cottage, villa, house, bush };
        db.HostProperties.AddRange(live);
        db.HostProperties.Add(karoo);

        foreach (var p in live)
            db.HostApplications.Add(new HostApplication
            {
                ApplicationUserId = p.HostId, Property = p, Status = ApplicationStatus.Approved,
                AppliedAt = DateTime.UtcNow.AddDays(-42), ReviewedAt = DateTime.UtcNow.AddDays(-41)
            });
        db.HostApplications.Add(new HostApplication
        {
            ApplicationUserId = muzi.Id, Property = karoo, Status = ApplicationStatus.Pending, AppliedAt = DateTime.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();

        // ---------- Bookings in every status ----------
        var today = DateTime.Today;
        Booking Stay(HostProperty p, ApplicationUser guest, int startInDays, int nights, int guests,
            BookingStatus status, PaymentStatus payment, int createdDaysAgo)
        {
            var q = bookings.CalculateQuote(p, today.AddDays(startInDays), today.AddDays(startInDays + nights));
            var created = DateTime.UtcNow.AddDays(-createdDaysAgo);
            return new Booking
            {
                PropertyId = p.Id, GuestId = guest.Id, NumberOfGuests = guests,
                CheckInDate = today.AddDays(startInDays), CheckOutDate = today.AddDays(startInDays + nights),
                CleaningFee = q.CleaningFee, ServiceFee = q.ServiceFee, TotalPrice = q.Total,
                Status = status, CreatedAt = created,
                Payment = new Payment { Amount = q.Total, Status = payment, CreatedAt = created }
            };
        }

        var b1 = Stay(cabin, banele, -20, 2, 4, BookingStatus.Completed, PaymentStatus.Paid, 30);
        var b2 = Stay(apartment, sboniso, -12, 3, 2, BookingStatus.Completed, PaymentStatus.Paid, 25);
        var b3 = Stay(villa, nthabiseng, -1, 3, 3, BookingStatus.CheckedIn, PaymentStatus.Paid, 10);
        var b4 = Stay(cottage, banele, 5, 2, 2, BookingStatus.Approved, PaymentStatus.Paid, 4);       // check-in awaiting verification
        var b5 = Stay(house, sboniso, 10, 2, 4, BookingStatus.Approved, PaymentStatus.Pending, 3);    // approved, payment due
        var b6 = Stay(cabin, nthabiseng, 14, 2, 2, BookingStatus.Pending, PaymentStatus.Pending, 1);  // waiting for host
        var b7 = Stay(bush, banele, 20, 3, 2, BookingStatus.Pending, PaymentStatus.Pending, 0);
        var b8 = Stay(apartment, nthabiseng, 7, 1, 1, BookingStatus.Rejected, PaymentStatus.Failed, 6);
        var b9 = Stay(villa, sboniso, 25, 2, 2, BookingStatus.Cancelled, PaymentStatus.Failed, 5);
        db.Bookings.AddRange(b1, b2, b3, b4, b5, b6, b7, b8, b9);
        await db.SaveChangesAsync();

        // ---------- Check-ins with sample ID documents ----------
        var docA = WriteSampleDocument(files, "Nthabiseng Maluleke");
        var docB = WriteSampleDocument(files, "Banele Mthembu");
        db.GuestCheckIns.AddRange(
            new GuestCheckIn
            {
                BookingId = b3.Id, CheckInProcessId = villa.CheckInProcess!.Id, Status = CheckInStatus.Verified,
                Documents = { new GuestDocument { DocumentType = "ID or Passport", FileName = docA, Status = DocumentStatus.Verified } }
            },
            new GuestCheckIn
            {
                BookingId = b4.Id, CheckInProcessId = cottage.CheckInProcess!.Id, Status = CheckInStatus.Submitted,
                Documents = { new GuestDocument { DocumentType = "ID or Passport", FileName = docB, Status = DocumentStatus.Pending } }
            });

        // ---------- Reviews for completed stays ----------
        db.Reviews.AddRange(
            new Review { PropertyId = cabin.Id, ReviewerId = banele.Id, Rating = 5, CreatedAt = DateTime.UtcNow.AddDays(-17),
                Comment = "Lovely, sunny townhouse. Spotless, secure and the host was super responsive." },
            new Review { PropertyId = apartment.Id, ReviewerId = sboniso.Id, Rating = 4, CreatedAt = DateTime.UtcNow.AddDays(-8),
                Comment = "Beautiful patio and garden, close to the beach. Parking was easy. Would stay again." });

        // ---------- Messages ----------
        db.Messages.AddRange(
            new Message { SenderId = banele.Id, ReceiverId = elsie.Id, Content = "Hi Elsie, is early check-in possible at the cottage?", Timestamp = DateTime.UtcNow.AddHours(-30), IsRead = true },
            new Message { SenderId = elsie.Id, ReceiverId = banele.Id, Content = "Hi Banele! Yes, from 12:00 is fine. I'll verify your ID shortly.", Timestamp = DateTime.UtcNow.AddHours(-29), IsRead = true },
            new Message { SenderId = banele.Id, ReceiverId = elsie.Id, Content = "Thank you, see you soon!", Timestamp = DateTime.UtcNow.AddHours(-2) },
            new Message { SenderId = nthabiseng.Id, ReceiverId = neroshen.Id, Content = "Is the pool heated this time of year?", Timestamp = DateTime.UtcNow.AddHours(-5) });

        // ---------- Notifications ----------
        void Notify(ApplicationUser u, string title, string body, NotificationType type, string link, int hoursAgo, bool read = false) =>
            db.Notifications.Add(new Notification { UserId = u.Id, Title = title, Body = body, Type = type, Link = link,
                CreatedAt = DateTime.UtcNow.AddHours(-hoursAgo), IsRead = read });

        Notify(elsie, "New Booking Request", "Nthabiseng Maluleke requested Sunny Garden Townhouse (2 nights).", NotificationType.BookingRequested, $"/HostBookings/Details/{b6.Id}", 20);
        Notify(elsie, "Check-in Documents Submitted", "Banele submitted an ID for Leafy Garden Cottage. Please verify it.", NotificationType.CheckInUpdate, $"/HostBookings/Details/{b4.Id}", 6);
        Notify(elsie, "New Message", "Banele Mthembu: Thank you, see you soon!", NotificationType.NewMessage, $"/Messages?with={banele.Id}", 2);
        Notify(neroshen, "New Booking Request", "Banele Mthembu requested Quiet Simplex in Secure Estate (3 nights).", NotificationType.BookingRequested, $"/HostBookings/Details/{b7.Id}", 1);
        Notify(neroshen, "New Message", "Nthabiseng Maluleke: Is the pool heated this time of year?", NotificationType.NewMessage, $"/Messages?with={nthabiseng.Id}", 5);
        Notify(banele, "Booking Approved", "Your stay at Leafy Garden Cottage was approved.", NotificationType.BookingApproved, $"/Guest/Booking/{b4.Id}", 70, true);
        Notify(sboniso, "Booking Approved", "Your stay at Family Home with Double Garage was approved. Complete payment and check-in.", NotificationType.BookingApproved, $"/Guest/Booking/{b5.Id}", 48);
        Notify(nthabiseng, "Booking Rejected", "Your request for Modern Brick Duplex was not accepted.", NotificationType.BookingRejected, $"/Guest/Booking/{b8.Id}", 120, true);
        Notify(muzi, "Host Application Submitted", "Your application for 'Hillside Garden Guesthouse' is under review.", NotificationType.General, "/HostApplication/Status", 24);
        foreach (var admin in await users.GetUsersInRoleAsync(AppRoles.Admin))
            Notify(admin, "New Host Application", "'Hillside Garden Guesthouse' in Bloemfontein needs review.", NotificationType.General, "/Admin/Applications", 24);

        // ---------- Activity history ----------
        void Log(ApplicationUser? u, string action, ActivityType type, int hoursAgo) =>
            db.ActivityLogs.Add(new ActivityLog { UserId = u?.Id, Action = action, ActivityType = type, CreatedAt = DateTime.UtcNow.AddHours(-hoursAgo) });

        Log(null, "DemoDataSeeded", ActivityType.System, 0);
        foreach (var u in new[] { elsie, neroshen, banele, sboniso, nthabiseng, muzi }) Log(u, "UserRegistered", ActivityType.Registration, 24 * 40);
        Log(elsie, "HostApplicationSubmitted", ActivityType.HostApplication, 24 * 42);
        Log(lunga, $"RoleChange: {elsie.Email} Guest to Host", ActivityType.RoleChange, 24 * 41);
        Log(lunga, $"RoleChange: {neroshen.Email} Guest to Host", ActivityType.RoleChange, 24 * 41);
        Log(nthabiseng, $"BookingStatusChange: #{b3.Id} Approved to CheckedIn", ActivityType.BookingStatusChange, 20);
        Log(elsie, $"BookingStatusChange: #{b4.Id} Pending to Approved", ActivityType.BookingStatusChange, 70);
        Log(banele, $"CheckInSubmitted: booking #{b4.Id}", ActivityType.CheckIn, 6);
        Log(muzi, "HostApplicationSubmitted", ActivityType.HostApplication, 24);
        Log(null, "FailedLogin: unknown email test@example.com", ActivityType.Security, 3);

        await db.SaveChangesAsync();
        Console.WriteLine("[Staybnb] Demo data seeded.");
    }

    // ---------- Upgrade listings created by the first demo seeder (illustrations) to real photos ----------
    private static readonly Dictionary<string, (string Title, string Type, string City, string Address, decimal Price, string Description, string[] Images)> LegacyUpdates = new()
    {
        ["Cozy Forest Cabin"] = ("Sunny Garden Townhouse", "House", "Durban", "12 Acacia Close, Hillcrest", 1450,
            "A bright single-storey townhouse in a quiet, secure complex. Covered parking at the door, a walled garden and a sunny patio for morning coffee.",
            new[] { "townhouse.jpg", "simplex-entrance.jpg", "patio-view.jpg" }),
        ["Lovely City Apartment"] = ("Modern Brick Duplex", "Apartment", "Umhlanga", "5 Palm Grove, Umhlanga Rocks", 1780,
            "A face-brick duplex with a private covered patio opening onto a leafy garden. Upstairs balcony, secure parking and close to the beach.",
            new[] { "duplex-patio.jpg", "patio-view.jpg", "garden-side.jpg" }),
        ["Beautiful Beachfront Cottage"] = ("Leafy Garden Cottage", "Cottage", "Ballito", "21 Dolphin Crescent, Ballito", 2100,
            "A peaceful brick cottage surrounded by mature fruit trees, with a stepping-stone garden path and a shaded outdoor area.",
            new[] { "garden-side.jpg", "patio-view.jpg" }),
        ["Exceptional Villa in Drakensberg"] = ("Palm Court Villa", "Villa", "Pretoria", "3 Palm Court, Faerie Glen", 2400,
            "A spacious double-storey villa behind a private walled courtyard, shaded by tall palms. Double garage, balcony and a large garden with a covered patio.",
            new[] { "palm-court.jpg", "duplex-patio.jpg", "garden-side.jpg", "patio-view.jpg" }),
        ["Cozy 3 Bedroom House"] = ("Family Home with Double Garage", "House", "Nelspruit", "8 Jacaranda Street, Sonheuwel", 1150,
            "A family-friendly double-storey home with a double garage, paved driveway and a big garden, close to the Kruger gates and Riverside Mall.",
            new[] { "garage-front.jpg", "garden-side.jpg", "patio-view.jpg" }),
        ["Cozy Holiday Cabin"] = ("Quiet Simplex in Secure Estate", "House", "Hazyview", "Unit 14, Aloe Estate, Hazyview", 980,
            "A neat simplex with a carport and walled garden in a gated estate. Perfect for a quiet weekend away.",
            new[] { "simplex-entrance.jpg", "townhouse.jpg" }),
        ["Karoo Farm Guesthouse"] = ("Hillside Garden Guesthouse", "Guesthouse", "Bloemfontein", "17 Hill Street, Westdene", 950,
            "A welcoming guesthouse with a private garden patio, secure parking and easy access to the city centre.",
            new[] { "patio-view.jpg", "garden-side.jpg" })
    };

    private static readonly string[] DemoHostEmails = { "elsie@staybnb.co.za", "neroshen@staybnb.co.za", "muzi@staybnb.co.za" };

    private static async Task RefreshLegacyDemoListingsAsync(ApplicationDbContext db)
    {
        var titles = LegacyUpdates.Keys.ToList();
        var legacy = await db.HostProperties
            .Include(p => p.Images)
            .Include(p => p.CheckInProcess)
            .Where(p => titles.Contains(p.Title) && DemoHostEmails.Contains(p.Host!.Email!))
            .ToListAsync();
        if (!legacy.Any()) return;
        Console.WriteLine($"[Staybnb] Upgrading {legacy.Count} demo listings to photos...");

        foreach (var p in legacy)
        {
            var u = LegacyUpdates[p.Title];
            p.Title = u.Title;
            p.PropertyType = u.Type;
            p.City = u.City;
            p.Address = u.Address;
            p.PricePerNight = u.Price;
            p.Description = u.Description;
            if (p.CheckInProcess != null) p.CheckInProcess.Title = $"Check-in for {u.Title}";

            db.PropertyImages.RemoveRange(p.Images.ToList());
            foreach (var img in u.Images) p.Images.Add(new PropertyImage { ImageUrl = $"/images/demo/{img}" });
        }
        await db.SaveChangesAsync();
    }

    private static async Task<ApplicationUser> CreateUserAsync(UserManager<ApplicationUser> users,
        string first, string last, string email, string role, int daysAgo)
    {
        var user = new ApplicationUser
        {
            UserName = email, Email = email, EmailConfirmed = true,
            FirstName = first, LastName = last, CreatedAt = DateTime.UtcNow.AddDays(-daysAgo)
        };
        var result = await users.CreateAsync(user, DemoPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Demo user {email} failed: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        await users.AddToRoleAsync(user, role);
        return user;
    }

    // A clearly fake ID card image so the host verification flow can be demonstrated
    private static string WriteSampleDocument(IFileStorageService files, string name)
    {
        var fileName = $"demo-id-{Guid.NewGuid():N}.svg";
        var path = files.GetPrivatePath("documents", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 640 400">
              <rect width="640" height="400" rx="24" fill="#e8f1ff" stroke="#1a73e8" stroke-width="6"/>
              <rect x="40" y="90" width="160" height="200" rx="12" fill="#c5d7f2"/>
              <circle cx="120" cy="160" r="40" fill="#8fa9d1"/><rect x="70" y="210" width="100" height="60" rx="30" fill="#8fa9d1"/>
              <text x="40" y="60" font-family="Arial" font-size="28" font-weight="bold" fill="#1a73e8">SAMPLE ID - DEMO ONLY</text>
              <text x="230" y="140" font-family="Arial" font-size="24" fill="#222">{name}</text>
              <text x="230" y="185" font-family="Arial" font-size="18" fill="#555">ID No: 0000000000000</text>
              <text x="230" y="220" font-family="Arial" font-size="18" fill="#555">Staybnb test document</text>
            </svg>
            """);
        return fileName;
    }
}
