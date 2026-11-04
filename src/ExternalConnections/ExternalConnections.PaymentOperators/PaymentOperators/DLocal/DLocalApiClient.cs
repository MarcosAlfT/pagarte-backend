using System.Net;
using System.Text;
using System.Text.Json;

namespace ExternalConnections.PaymentOperators.PaymentOperators.DLocal;

public sealed class DLocalApiClient
{
	private readonly HttpClient http;
	private readonly DLocalConnection connection;
	public DLocalApiClient(IHttpClientFactory factory, DLocalConnection connection)
	{
		if (connection.BaseUri.Scheme != Uri.UriSchemeHttps) throw new InvalidOperationException("dLocal requires HTTPS.");
		this.connection = connection;
		http = factory.CreateClient("dlocal");
		http.BaseAddress = connection.BaseUri;
		http.Timeout = TimeSpan.FromSeconds(30);
		http.MaxResponseContentBufferSize = 32768;
	}

	public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? payload, string? key, CancellationToken ct)
	{
		if (!path.StartsWith('/') || path.StartsWith("//", StringComparison.Ordinal) || path.Contains('\\'))
			throw new InvalidOperationException("Use a relative dLocal API resource path.");
		if (key is not null && (key.Length is < 1 or > 42 || key.Any(c => c is < '!' or > '~')))
			throw new InvalidOperationException("dLocal idempotency keys must contain 1 to 42 printable characters.");
		var bytes = payload is null ? [] : JsonSerializer.SerializeToUtf8Bytes(payload);
		using var request = new HttpRequestMessage(method, path);
		if (payload is not null) request.Content = new ByteArrayContent(bytes);
		if (request.Content is not null) request.Content.Headers.ContentType = new("application/json");
		var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);
		request.Headers.Add("X-Login", connection.XLogin);
		request.Headers.Add("X-Trans-Key", connection.XTransKey);
		request.Headers.Add("X-Date", timestamp);
		request.Headers.Add("X-Version", "2.1");
		request.Headers.UserAgent.ParseAdd("Pagarte/1.0");
		request.Headers.TryAddWithoutValidation("Authorization", DLocalSignature.Create(connection.XLogin, connection.SecretKey, timestamp, bytes));
		if (key is not null) request.Headers.Add("X-Idempotency-Key", key);
		// No automatic retries: workflow-owned intent/recovery decides whether another call is safe.
		return await http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
	}

	public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response, CancellationToken ct)
	{
		const int limit = 32768;
		if (response.Content.Headers.ContentLength > limit) throw new JsonException("Oversized provider response.");
		await using var stream = await response.Content.ReadAsStreamAsync(ct);
		using var body = new MemoryStream();
		var buffer = new byte[4096]; int read;
		while ((read = await stream.ReadAsync(buffer, ct)) > 0)
		{
			if (body.Length + read > limit) throw new JsonException("Oversized provider response.");
			body.Write(buffer, 0, read);
		}
		using var document = JsonDocument.Parse(body.ToArray(), new JsonDocumentOptions { MaxDepth = 16 });
		return document.RootElement.Clone();
	}
}
