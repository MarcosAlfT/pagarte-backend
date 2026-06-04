using Identity.Client.Application.Policies;

namespace Identity.Client.Application.PasswordValidation;

public sealed class MinimumLengthRule : IPasswordRule
{
    public string? Validate(string password, string email, PasswordPolicy policy)
    {
        return password.Length < policy.MinimumLength
            ? $"Password must be at least {policy.MinimumLength} characters."
            : null;
    }
}

public sealed class MaximumLengthRule : IPasswordRule
{
    public string? Validate(string password, string email, PasswordPolicy policy)
    {
        return password.Length > policy.MaximumLength
            ? $"Password must be at most {policy.MaximumLength} characters."
            : null;
    }
}

public sealed class UppercaseRule : IPasswordRule
{
    public string? Validate(string password, string email, PasswordPolicy policy)
    {
        return policy.RequireUppercase && !password.Any(char.IsUpper)
            ? "Password must contain an uppercase letter."
            : null;
    }
}

public sealed class LowercaseRule : IPasswordRule
{
    public string? Validate(string password, string email, PasswordPolicy policy)
    {
        return policy.RequireLowercase && !password.Any(char.IsLower)
            ? "Password must contain a lowercase letter."
            : null;
    }
}

public sealed class NumberRule : IPasswordRule
{
    public string? Validate(string password, string email, PasswordPolicy policy)
    {
        return policy.RequireNumber && !password.Any(char.IsDigit)
            ? "Password must contain a number."
            : null;
    }
}

public sealed class SymbolRule : IPasswordRule
{
    public string? Validate(string password, string email, PasswordPolicy policy)
    {
        return policy.RequireSymbol && !password.Any(c => !char.IsLetterOrDigit(c))
            ? "Password must contain a symbol."
            : null;
    }
}

public sealed class NotSameAsEmailRule : IPasswordRule
{
    public string? Validate(string password, string email, PasswordPolicy policy)
    {
        return policy.RejectEmailAsPassword && password.Equals(email, StringComparison.OrdinalIgnoreCase)
            ? "Password cannot be the same as the email."
            : null;
    }
}

public sealed class CommonPasswordRule : IPasswordRule
{
    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "password1",
        "password123",
        "admin123",
        "qwerty123",
        "letmein",
        "welcome1"
    };

    public string? Validate(string password, string email, PasswordPolicy policy)
    {
        return policy.RejectCommonPasswords && CommonPasswords.Contains(password)
            ? "Password is too common."
            : null;
    }
}
