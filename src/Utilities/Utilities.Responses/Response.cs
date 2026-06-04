namespace Utilities.Responses
{
	/// <summary>
	/// The base for all API responses, indicating success and a message.
	/// </summary>
	public class Response
	{
		public bool Success { get; set; }
		public string? Message { get; set; }
	}

	/// <summary>
	/// A generic base response that can hold a data payload.
	/// </summary>
	/// <typeparam name="T">The type of the data payload.</typeparam>
	public class Response<T> : Response
	{
		public T? Data { get; set; }
	}
}
