# Staybnb: Airbnb Clone (ASP.NET Core MVC, EF Core, SQL Server)

A managed property rental marketplace covering the full user lifecycle:
Guests explore and book, apply to become Hosts, and Administrators keep the marketplace safe.

## Running the project

1. Requirements: .NET SDK 10 (or 8+) and SQL Server LocalDB (installed with Visual Studio).
2. Open a terminal in this folder and run:
   ```
   dotnet run
   ```
3. Open the `http://localhost:...` address shown in the terminal.

On startup the app automatically:
- applies all EF Core migrations (creates the `StaybnbDb` database),
- seeds the four roles and the SuperAdmin (`RoleSeeder`),
- seeds amenities and a demo marketplace (`DemoDataSeeder`), so every feature can be tested straight away.

No SQL scripts need to be run manually. `Database/Staybnb_Schema.sql` is included for reference only.

To use a different SQL Server, change `ConnectionStrings:DefaultConnection` in `appsettings.json`,
for example `Server=.\SQLEXPRESS;Database=StaybnbDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true`.

To start from an empty database: `dotnet ef database drop`, then `dotnet run`.

## Demo accounts

| Role | Email | Password |
|---|---|---|
| SuperAdmin | superadmin@staybnb.co.za | SuperAdmin@123 |
| Admin | lunga@staybnb.co.za | Demo@123 |
| Host | elsie@staybnb.co.za | Demo@123 |
| Host | neroshen@staybnb.co.za | Demo@123 |
| Guest (pending host application) | muzi@staybnb.co.za | Demo@123 |
| Guest | banele@staybnb.co.za | Demo@123 |
| Guest | sboniso@staybnb.co.za | Demo@123 |
| Guest | nthabiseng@staybnb.co.za | Demo@123 |

The demo data contains bookings in every status (Pending, Approved, CheckedIn, Completed, Rejected, Cancelled),
a check-in waiting for document verification, reviews, messages, notifications and activity logs.

## How the requirements are met

| Rubric area | Where |
|---|---|
| Auth and role logic | `AccountController` (new users get Guest), `RoleService` (adds Host, removes Guest), `RoleSeeder` (SuperAdmin), `[Authorize(Roles = ...)]` on every controller |
| Admin dashboard | `AdminController`: dashboard stats, host request review, user management (SuperAdmin-only promotion), bookings, properties, activity logs |
| Host dashboard | `HostController` (stats, listings, IsActive toggle, edit, check-in setup) and `HostBookingsController` (approve or reject, verify documents, check in) |
| Application process | `HostApplicationController`: property details plus at least one image required, validated in the browser and on the server |
| Booking and check-in | `BookingService` (TotalPrice = PricePerNight x Nights + CleaningFee + ServiceFee), `GuestController` (pay, multi-step check-in with ID upload). Status flow: Pending, Approved, CheckedIn, Completed |
| Messaging and notifications | `MessagesController` (database inbox with read status), `NotificationsController` (bell, filters, read and unread), `HeaderIconsViewComponent` |
| Data and architecture | 15 entities in `Models`, relationships configured in `ApplicationDbContext`, EF Core migrations, services injected through interfaces, `ActivityLogger` on logins, role changes and booking status changes |

## Security notes

- Guest ID documents are stored outside `wwwroot` (`App_Data/uploads/documents`) and served only to the guest, their host or an admin (`DocumentsController`).
- Every form uses anti-forgery tokens; uploads are checked for type and size.
- Five failed logins lock the account for 5 minutes; admins can lock accounts; roles refresh on every request.

## Test checklist

1. Register a new user and confirm the Guest badge. Visiting `/Host/Dashboard` shows Access denied.
2. Become a Host: submitting without an image is blocked; submit with images.
3. Log in as the SuperAdmin, approve the application (or Muzi's demo application). The user now shows Host and has lost Guest.
4. As Admin (not SuperAdmin) the "Make Admin" button is hidden. As SuperAdmin, promote a Guest to Admin.
5. As a Guest, filter the gallery by city or type, book dates and check the price breakdown.
6. As the Host, approve the request. As the Guest, pay and complete the check-in steps with an ID upload.
7. As the Host, verify the document and mark the guest Checked In, then Completed.
8. Send a message from a listing's "Contact host". Check the envelope and bell counts and mark notifications read.
9. Review Activity Logs filtered by Security, RoleChange and BookingStatusChange.
