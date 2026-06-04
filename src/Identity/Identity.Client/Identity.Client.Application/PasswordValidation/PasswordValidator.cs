using Identity.Client.Application.Policies;

namespace Identity.Client.Application.PasswordValidation;

public sealed class PasswordValidator
{
    private readonly IReadOnlyCollection<IPasswordRule> _rules;

    private PasswordValidator(IReadOnlyCollection<IPasswordRule> rules)
    {
        _rules = rules;
    }

    public PasswordValidationResult Validate(string password, string email, PasswordPolicy policy)
    {
        var errors = _rules
            .Select(rule => rule.Validate(password, email, policy))
            .Where(error => !string.IsNullOrWhiteSpace(error))
            .Cast<string>()
            .ToArray();

        return new PasswordValidationResult(errors.Length == 0, errors);
    }

    public sealed class Builder
    {
        private readonly List<IPasswordRule> _rules = [];

        public Builder UseDefaultRules()
        {
            _rules.AddRange([
                new MinimumLengthRule(),
                new MaximumLengthRule(),
                new UppercaseRule(),
                new LowercaseRule(),
                new NumberRule(),
                new SymbolRule(),
                new NotSameAsEmailRule(),
                new CommonPasswordRule()
            ]);

            return this;
        }

        public PasswordValidator Build()
        {
            return new PasswordValidator(_rules);
        }
    }
}
