using System.Security.Cryptography;
using System.Text;
using Identity.Client.Application.Abstractions;

namespace Identity.Client.Infrastructure.Security;

public sealed class Sha256TokenHasher : ITokenHasher
{
    public string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
