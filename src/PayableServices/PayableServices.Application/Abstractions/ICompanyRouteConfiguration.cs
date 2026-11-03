namespace PayableServices.Application.Abstractions;

public sealed record CompanyRouteConnection(string PaymentEndpoint, string RollbackEndpoint,
    string StatusEndpoint, string CredentialReference);
public interface ICompanyRouteConfiguration
{
    CompanyRouteConnection? Resolve(string provider);
}
