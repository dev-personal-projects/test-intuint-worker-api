namespace test_intuint_invoicing_api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health", HealthCheck)
            .WithName("HealthCheck")
            .WithTags("Health");
    }

    private static IResult HealthCheck()
    {
        // Return health status and current timestamp
        return Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
}

