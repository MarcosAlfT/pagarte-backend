namespace Identity.Client.Application.PasswordValidation;

public sealed record PasswordValidationResult(bool IsValid, IReadOnlyCollection<string> Errors);
