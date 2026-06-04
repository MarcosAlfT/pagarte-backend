namespace Utilities.Responses
{
	/// <summary>
	/// Represents a standard API response envelope with a data payload.
	/// Provides factory methods for easy creation.
	/// </summary>
	public class ApiResponse<T> : Response<T>
	{
		public static ApiResponse<T> CreateSuccess(T? data, string? message = "Operation succeeded.")
		{
			return new ApiResponse<T>
			{
				Success = true,
				Message = message,
				Data = data
			};
		}

		public static ApiResponse<T> CreateFailure(string? message)
		{
			return new ApiResponse<T>
			{
				Success = false,
				Message = message,
				Data = default
			};
		}
	}

	/// <summary>
	/// Represents a standard API response envelope without a data payload.
	/// Provides factory methods for easy creation.
	/// </summary>
	public class ApiResponse : Response
	{
		public static ApiResponse CreateSuccess(string? message = "Operation succeeded.")
		{
			return new ApiResponse
			{
				Success = true,
				Message = message
			};
		}

		public static ApiResponse CreateFailure(string? message)
		{
			return new ApiResponse
			{
				Success = false,
				Message = message
			};
		}
	}
}
