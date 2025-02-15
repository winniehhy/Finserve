using MySql.Data.MySqlClient;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

var app = builder.Build();

var logger = app.Logger;

// --- Debug: Confirm Configuration Loading ---
logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);
Console.WriteLine("=== STARTING DATABASE CONNECTION TEST ===");

// Retrieve the connection string
var connectionString = Environment.GetEnvironmentVariable("DefaultConnection")
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

logger.LogInformation("Connection string: {cs}", connectionString);

if (string.IsNullOrEmpty(connectionString))
{
    logger.LogError("Connection string is missing!");
    Console.WriteLine("ERROR: No connection string");
}
else
{
    Console.WriteLine("Attempting MySQL connection...");

    try
    {
        using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(); // Use async version
        logger.LogInformation("MySQL CONNECTED SUCCESSFULLY!");
        Console.WriteLine("SUCCESS: MySQL connected!");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "MySQL connection failed");
        Console.WriteLine($"ERROR: {ex.Message}");
    }
}

Console.WriteLine("=== CONNECTION TEST COMPLETED ===");

// Rest of your middleware setup
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);


app.Run();