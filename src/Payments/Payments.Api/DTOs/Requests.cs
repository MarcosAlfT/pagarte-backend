using System.Text.Json.Serialization;

namespace Payments.Api.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StartCardRegistrationRequest(string CardHolderName, bool IsDefault = false, bool ConsentAccepted = false, string ConsentVersion = "");

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CompleteCardRegistrationRequest(string TemporaryToken);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateCreditCardRequest(string CardHolderName, bool IsDefault);
