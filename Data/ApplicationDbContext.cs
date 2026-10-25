using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Staybnb.Models;

namespace Staybnb.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<HostProperty> HostProperties => Set<HostProperty>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Amenity> Amenities => Set<Amenity>();
    public DbSet<PropertyImage> PropertyImages => Set<PropertyImage>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<HostApplication> HostApplications => Set<HostApplication>();
    public DbSet<CheckInProcess> CheckInProcesses => Set<CheckInProcess>();
    public DbSet<GuestCheckIn> GuestCheckIns => Set<GuestCheckIn>();
    public DbSet<GuestDocument> GuestDocuments => Set<GuestDocument>();

    // Store enums as readable text in SQL Server
    protected override void ConfigureConventions(ModelConfigurationBuilder c)
    {
        c.Properties<BookingStatus>().HaveConversion<string>();
        c.Properties<PaymentStatus>().HaveConversion<string>();
        c.Properties<ApplicationStatus>().HaveConversion<string>();
        c.Properties<ActivityType>().HaveConversion<string>();
        c.Properties<NotificationType>().HaveConversion<string>();
        c.Properties<CheckInStatus>().HaveConversion<string>();
        c.Properties<DocumentStatus>().HaveConversion<string>();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // One-to-Many: ApplicationUser -> HostProperty
        b.Entity<HostProperty>()
            .HasOne(p => p.Host).WithMany(u => u.Properties)
            .HasForeignKey(p => p.HostId).OnDelete(DeleteBehavior.Cascade);

        // Many-to-Many: HostProperty <-> Amenity
        b.Entity<HostProperty>()
            .HasMany(p => p.Amenities).WithMany(a => a.Properties)
            .UsingEntity(j => j.ToTable("PropertyAmenities"));

        // One-to-Many: HostProperty -> PropertyImage
        b.Entity<PropertyImage>()
            .HasOne(i => i.Property).WithMany(p => p.Images)
            .HasForeignKey(i => i.PropertyId).OnDelete(DeleteBehavior.Cascade);

        // One-to-One: HostProperty -> CheckInProcess
        b.Entity<HostProperty>()
            .HasOne(p => p.CheckInProcess).WithOne(c => c.Property)
            .HasForeignKey<CheckInProcess>(c => c.PropertyId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Booking>(e =>
        {
            // One-to-Many: HostProperty -> Booking
            e.HasOne(x => x.Property).WithMany(p => p.Bookings)
             .HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Cascade);
            // One-to-Many: ApplicationUser -> Booking (Restrict avoids multiple cascade paths)
            e.HasOne(x => x.Guest).WithMany(u => u.Bookings)
             .HasForeignKey(x => x.GuestId).OnDelete(DeleteBehavior.Restrict);
            // One-to-One: Booking -> Payment
            e.HasOne(x => x.Payment).WithOne(p => p.Booking)
             .HasForeignKey<Payment>(p => p.BookingId).OnDelete(DeleteBehavior.Cascade);
            // One-to-One: Booking -> GuestCheckIn
            e.HasOne(x => x.GuestCheckIn).WithOne(g => g.Booking)
             .HasForeignKey<GuestCheckIn>(g => g.BookingId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<GuestCheckIn>()
            .HasOne(g => g.CheckInProcess).WithMany(c => c.GuestCheckIns)
            .HasForeignKey(g => g.CheckInProcessId).OnDelete(DeleteBehavior.Restrict);

        // One-to-Many: GuestCheckIn -> GuestDocument
        b.Entity<GuestDocument>()
            .HasOne(d => d.GuestCheckIn).WithMany(g => g.Documents)
            .HasForeignKey(d => d.GuestCheckInId).OnDelete(DeleteBehavior.Cascade);

        // One-to-Many: HostProperty -> Review and ApplicationUser -> Review
        b.Entity<Review>(e =>
        {
            e.HasOne(r => r.Property).WithMany(p => p.Reviews)
             .HasForeignKey(r => r.PropertyId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Reviewer).WithMany(u => u.Reviews)
             .HasForeignKey(r => r.ReviewerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<WishlistItem>(e =>
        {
            e.HasOne(w => w.User).WithMany(u => u.WishlistItems)
             .HasForeignKey(w => w.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(w => w.Property).WithMany()
             .HasForeignKey(w => w.PropertyId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(w => new { w.UserId, w.PropertyId }).IsUnique();
        });

        b.Entity<Message>(e =>
        {
            e.HasOne(m => m.Sender).WithMany()
             .HasForeignKey(m => m.SenderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.Receiver).WithMany()
             .HasForeignKey(m => m.ReceiverId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ActivityLog>()
            .HasOne(a => a.User).WithMany()
            .HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<Notification>()
            .HasOne(n => n.User).WithMany()
            .HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<HostApplication>(e =>
        {
            e.HasOne(h => h.ApplicationUser).WithMany()
             .HasForeignKey(h => h.ApplicationUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(h => h.Property).WithMany()
             .HasForeignKey(h => h.PropertyId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
