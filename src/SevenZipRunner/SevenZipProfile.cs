using System.Diagnostics;

namespace SevenZipRunner;

/// <summary>
/// Represents a single, named compression profile.
/// </summary>
public class SevenZipProfile
{
    /// <summary>
    /// The compression level (1=fastest, 5=normal, 9=ultra).
    /// </summary>
    public int CompressionLevel { get; set; } = 5;

    /// <summary>
    /// The number of threads to use. 0 means use all available cores.
    /// </summary>
    public int Threads { get; set; } = 0;

    /// <summary>
    /// The priority for the 7za.exe process.
    /// </summary>
    public ProcessPriorityClass ProcessPriority { get; set; } = ProcessPriorityClass.Normal;
}
