using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.Extensions.Options;

namespace SevenZipRunner;

public interface ISevenZipExecutor
{
    Task CompressDirectoryAsync(string sourceDirectory, string destinationArchive, string? profileName = null, CancellationToken cancellationToken = default);

    Task ExtractArchiveAsync(string sourceArchive, string destinationDirectory, string? profileName = null, CancellationToken cancellationToken = default);
}

public class SevenZipExecutor : ISevenZipExecutor
{
    private readonly SevenZipOptions options;
    private readonly string sevenZipExePath;

    public SevenZipExecutor(IOptions<SevenZipOptions> options)
    {
        this.options = options.Value;
        sevenZipExePath = ResolveExecutablePath(this.options.ExePath);
    }

    public SevenZipExecutor(SevenZipOptions options)
    {
        this.options = options;
        sevenZipExePath = ResolveExecutablePath(this.options.ExePath);
    }

    private static string ResolveExecutablePath(string basePath)
    {
        // Determine OS-specific folder and executable name
        var (osPart, exeName) = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ("windows", "7za.exe")
                              : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? ("linux", "7zz")
                              : throw new PlatformNotSupportedException("Only Windows and Linux are supported.");

        // Determine architecture-specific folder
        var archPart = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            _ => throw new PlatformNotSupportedException($"The architecture {RuntimeInformation.ProcessArchitecture} is not supported.")
        };

        // Resolve the base path relative to the application's location if it's not rooted
        string resolvedBasePath = basePath;
        if (!Path.IsPathRooted(resolvedBasePath))
        {
            resolvedBasePath = Path.Combine(AppContext.BaseDirectory, resolvedBasePath);
        }

        // Construct the full path
        string finalPath = Path.Combine(resolvedBasePath, osPart, archPart, exeName);

        if (!File.Exists(finalPath))
        {
            throw new FileNotFoundException(
                $"The 7-Zip executable for the current platform ({osPart}/{archPart}) was not found.",
                finalPath);
        }

        return finalPath;
    }

    public Task CompressDirectoryAsync(string sourceDirectory, string destinationArchive, string? profileName = null, CancellationToken cancellationToken = default)
    {
        var profile = options.GetAdjustedProfile(profileName);

        var args = new StringBuilder();
        args.Append($"a \"{destinationArchive}\" \"{sourceDirectory}\"");
        args.Append($" -t7z");
        args.Append($" -mx={profile.CompressionLevel}");
        args.Append($" -mmt{profile.Threads}");
        args.Append($" -y");

        return RunCommandAsync(args.ToString(), profile.ProcessPriority, cancellationToken);
    }

    public Task ExtractArchiveAsync(string sourceArchive, string destinationDirectory, string? profileName = null, CancellationToken cancellationToken = default)
    {
        // Extraction profile might have different priority, etc.
        var profile = options.GetAdjustedProfile(profileName);

        var args = new StringBuilder();
        args.Append($"x \"{sourceArchive}\" -o\"{destinationDirectory}\"");
        args.Append($" -y");

        return RunCommandAsync(args.ToString(), profile.ProcessPriority, cancellationToken);
    }

    private async Task RunCommandAsync(string arguments, ProcessPriorityClass priority, CancellationToken cancellationToken)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = sevenZipExePath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        using var process = new Process { StartInfo = processStartInfo };

        process.Start();

        // Set process priority after starting
        try { process.PriorityClass = priority; }
        catch { /* Ignore errors, e.g., access denied */ }

        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            string standardError = await stdErrTask;
            throw new SevenZipException(process.ExitCode, standardError, arguments);
        }
    }
}
