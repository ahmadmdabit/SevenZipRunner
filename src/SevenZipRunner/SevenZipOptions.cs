using System.Diagnostics;

namespace SevenZipRunner;

/// <summary>
/// Main options class, holds all profiles and the platform-aware logic.
/// </summary>
public class SevenZipOptions
{
    /// <summary>
    /// The base path to the directory containing the platform-specific 7-Zip executables.
    /// For example, "SevenZip", which would contain "windows/x64/7za.exe".
    /// </summary>
    public string ExePath { get; set; } = "SevenZip";

    /// <summary>
    /// The name of the profile to use if none is specified in a method call.
    /// </summary>
    public string DefaultProfile { get; set; } = "Balanced";

    /// <summary>
    /// A dictionary of available compression profiles.
    /// </summary>
    public IReadOnlyDictionary<string, SevenZipProfile> Profiles { get; set; } = new Dictionary<string, SevenZipProfile>()
    {
        ["Balanced"] = new()
        {
            CompressionLevel = 5,
            Threads = 0,
            ProcessPriority = ProcessPriorityClass.Normal,
        },
        ["Fastest"] = new()
        {
            CompressionLevel = 1,
            Threads = 0,
            ProcessPriority = ProcessPriorityClass.BelowNormal,
        },
        ["MaxCompression"] = new()
        {
            CompressionLevel = 9,
            Threads = 0,
            ProcessPriority = ProcessPriorityClass.Normal,
        },
        ["LogArchiving"] = new()
        {
            CompressionLevel = 1,
            Threads = 2, // Limit to 2 threads to not impact a server
            ProcessPriority = ProcessPriorityClass.BelowNormal,
        },
        ["LuceneIndexBackup"] = new()
        {
            CompressionLevel = 7,
            Threads = 0,
            ProcessPriority = ProcessPriorityClass.Normal,
        },
    };

    /// <summary>
    /// Gets a profile and adjusts it for the current platform.
    /// This is the core of the platform-aware logic.
    /// </summary>
    public SevenZipProfile GetAdjustedProfile(string? profileName)
    {
        profileName ??= DefaultProfile;
        if (!Profiles.TryGetValue(profileName, out var profile))
        {
            throw new ArgumentException($"SevenZip profile '{profileName}' not found in configuration.");
        }

        // Create a copy to avoid modifying the source configuration
        var adjustedProfile = new SevenZipProfile
        {
            CompressionLevel = profile.CompressionLevel,
            Threads = profile.Threads,
            ProcessPriority = profile.ProcessPriority
        };

        // --- PLATFORM-AWARE LOGIC ---

        // 1. Memory Constraint: Cap compression level on 32-bit systems to avoid crashes.
        //    Level 7/9 can easily exceed the 2GB address space.
        if (!Environment.Is64BitProcess && adjustedProfile.CompressionLevel > 5)
        {
            adjustedProfile.CompressionLevel = 5; // Cap at 'normal'
        }

        // 2. CPU Constraint: Resolve thread count. 0 means use all cores.
        if (adjustedProfile.Threads <= 0)
        {
            adjustedProfile.Threads = Environment.ProcessorCount;
        }

        return adjustedProfile;
    }
}

/*
{
  "SevenZip": {
    "ExePath": "SevenZip", // Assumes it's in the output dir or PATH
    "DefaultProfile": "Balanced",
    "Profiles": {
      "Balanced": {
        "CompressionLevel": 5,
        "Threads": 0 // Use all available cores
      },
      "Fastest": {
        "CompressionLevel": 1,
        "Threads": 0,
        "ProcessPriority": "BelowNormal"
      },
      "MaxCompression": {
        "CompressionLevel": 9,
        "Threads": 0
      },
      "LogArchiving": {
        "CompressionLevel": 1,
        "Threads": 2, // Limit to 2 threads to not impact a server
        "ProcessPriority": "BelowNormal"
      },
      "LuceneIndexBackup": {
        "CompressionLevel": 7,
        "Threads": 0
      }
    }
  }
}
*/
