namespace test_intuint_invoicing_api.Models;

// Standard API response wrapper for consistent response format
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }
    public string? Message { get; set; }

    // Create a successful response with data
    public static ApiResponse<T> Ok(T data) => new() { Success = true, Data = data };
    
    // Create a failed response with error message
    public static ApiResponse<T> Fail(string error) => new() { Success = false, Error = error };
}

