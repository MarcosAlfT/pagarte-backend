using Identity.Client.Application.Policies;

namespace Identity.Client.Application.PasswordValidation;

public interface IPasswordRule
{
    string? Validate(string password, string email, PasswordPolicy policy);
}
