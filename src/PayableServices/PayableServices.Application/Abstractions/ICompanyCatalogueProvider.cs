using PayableServices.Application.Models;

namespace PayableServices.Application.Abstractions;

public interface ICompanyCatalogueProvider
{
    Task<CompanyCatalogueSnapshot?> GetSnapshotAsync(string source, CancellationToken cancellationToken);
}
