namespace Identity.Client.Application.Abstractions;

public interface ITokenHasher
{
    string Hash(string token);
}
