namespace Staybnb.Models;

public enum BookingStatus { Pending, Approved, CheckedIn, Rejected, Cancelled, Completed }
public enum PaymentStatus { Pending, Paid, Failed, Refunded }
public enum ApplicationStatus { Pending, Approved, Rejected }
public enum ActivityType { Registration, Login, Logout, RoleChange, HostApplication, BookingStatusChange, PropertyChange, CheckIn, Security, System }
public enum NotificationType { General, NewMessage, BookingRequested, BookingApproved, BookingRejected, HostApplicationApproved, HostApplicationRejected, CheckInUpdate }
public enum CheckInStatus { NotStarted, InProgress, Submitted, Verified, Rejected }
public enum DocumentStatus { Pending, Verified, Rejected }

public static class AppRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string Host = "Host";
    public const string Guest = "Guest";
}
