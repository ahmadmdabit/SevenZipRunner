using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SevenZipRunner;

// ................................................................
// 1. Setup Host with Dependency Injection and Configuration
// ................................................................
using var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Bind "SevenZip" section from appsettings.json
        services.Configure<SevenZipOptions>(context.Configuration.GetSection("SevenZip"));
        // Register the executor as singleton
        services.AddSingleton<ISevenZipExecutor, SevenZipExecutor>();
    })
    .Build();

// Get the executor from DI
var executor = host.Services.GetRequiredService<ISevenZipExecutor>();

// ................................................................
// 2. Direct instantiation (alternative to DI)
//    Uncomment to test without DI
// ................................................................
/*
var options = new SevenZipOptions
{
    ExePath = "SevenZip",
    DefaultProfile = "Balanced"
    // Profiles are built-in, but you can override
};
var directExecutor = new SevenZipExecutor(options);
*/

// ................................................................
// 3. Usage Examples
// ................................................................
Console.WriteLine("SevenZipRunner Console Demo");
Console.WriteLine("...........................");

// ................................................................
// 3a. Compress a directory with a specific profile
// ................................................................
try
{
    Console.WriteLine("\nCompressing with 'LogArchiving' profile...");
    await executor.CompressDirectoryAsync(
        sourceDirectory: @"C:\temp\logs",
        destinationArchive: @"C:\temp\logs-backup.7z",
        profileName: "LogArchiving",
        cancellationToken: default
    );
    Console.WriteLine("Compression succeeded.");
}
catch (SevenZipException ex)
{
    Console.WriteLine($"Compression failed: {ex.Message}");
    Console.WriteLine($"Exit code: {ex.ExitCode}");
    Console.WriteLine($"Standard error: {ex.StandardError}");
    Console.WriteLine($"Arguments: {ex.Arguments}");
}
catch (Exception ex)
{
    Console.WriteLine($"Unexpected error: {ex.Message}");
}

// ................................................................
// 3b. Compress using the default profile (no profileName specified)
// ................................................................
try
{
    Console.WriteLine("\nCompressing with default profile...");
    await executor.CompressDirectoryAsync(
        sourceDirectory: @"C:\temp\data",
        destinationArchive: @"C:\temp\data.7z"
    );
    Console.WriteLine("Compression with default succeeded.");
}
catch (SevenZipException ex)
{
    Console.WriteLine($"Compression failed: {ex.Message}");
}

// ................................................................
// 3c. Extract an archive with a specific profile
// ................................................................
try
{
    Console.WriteLine("\nExtracting with 'Balanced' profile...");
    await executor.ExtractArchiveAsync(
        sourceArchive: @"C:\temp\logs-backup.7z",
        destinationDirectory: @"C:\temp\restored-logs",
        profileName: "Balanced"
    );
    Console.WriteLine("Extraction succeeded.");
}
catch (SevenZipException ex)
{
    Console.WriteLine($"Extraction failed: {ex.Message}");
}

// ................................................................
// 3d. Extract using the default profile
// ................................................................
try
{
    Console.WriteLine("\nExtracting with default profile...");
    await executor.ExtractArchiveAsync(
        sourceArchive: @"C:\temp\data.7z",
        destinationDirectory: @"C:\temp\restored-data"
    );
    Console.WriteLine("Extraction with default succeeded.");
}
catch (SevenZipException ex)
{
    Console.WriteLine($"Extraction failed: {ex.Message}");
}

Console.WriteLine("\nDemo completed.");