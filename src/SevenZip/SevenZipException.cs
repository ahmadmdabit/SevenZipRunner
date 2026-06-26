using System.Text;

namespace SevenZip;

public class SevenZipException : Exception
{
    public int ExitCode { get; }
    public string? StandardError { get; }
    public string Arguments { get; }

    public SevenZipException(int exitCode, string? standardError, string arguments)
        : base(BuildErrorMessage(exitCode, standardError, arguments))
    {
        ExitCode = exitCode;
        StandardError = standardError;
        Arguments = arguments;
    }

    private static string BuildErrorMessage(int exitCode, string? stdErr, string args)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"7-Zip process failed with exit code {exitCode}.");
        sb.AppendLine($"Arguments: 7za.exe {args}");
        if (!string.IsNullOrWhiteSpace(stdErr))
        {
            sb.AppendLine("Standard Error:");
            sb.AppendLine(stdErr);
        }
        return sb.ToString();
    }
}
