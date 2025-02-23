using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Finserve3.Models; // Import your models and DbContext
using Microsoft.EntityFrameworkCore; // For UseSqlServer extension

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();  // Use controllers with views

// Register your ApplicationDbContext with SQL Server provider
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
);

var app = builder.Build();
var logger = app.Logger;

// --- Debug: Confirm Configuration Loading ---
logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);
Console.WriteLine("=== STARTING DATABASE CONNECTION TEST ===");

// Retrieve the connection string from environment variable or configuration
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
    Console.WriteLine("Attempting SQL Server connection...");

    try
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(); // Async for better performance
        logger.LogInformation("✅ MSSQL CONNECTED SUCCESSFULLY!");
        Console.WriteLine("✅ SUCCESS: SQL Server connected!");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ SQL Server connection failed");
        Console.WriteLine($"❌ ERROR: {ex.Message}");
    }
}

Console.WriteLine("=== CONNECTION TEST COMPLETED ===");

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// Map the default controller route.
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.Run();
