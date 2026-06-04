namespace Identity.Client.Application.Abstractions;

public interface ITokenGenerator
{
    string GenerateUrlSafeToken();
}
