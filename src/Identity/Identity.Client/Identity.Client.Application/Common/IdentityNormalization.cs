namespace Identity.Client.Application.Common;

public static class IdentityNormalization
{
    public static string NormalizeEmail(string email)
    {
        return email.Trim().ToUpperInvariant();
    }
}
