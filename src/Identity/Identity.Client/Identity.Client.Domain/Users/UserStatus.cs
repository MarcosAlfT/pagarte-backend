namespace Identity.Client.Domain.Users;

public enum UserStatus
{
    PendingEmailConfirmation = 0,
    Active = 1,
    Locked = 2,
    Suspended = 3,
    Deleted = 4
}
