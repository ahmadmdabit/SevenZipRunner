# SevenZipRunner

A lightweight .NET library for invoking 7‑Zip (7za/7zz) from managed code, with platform‑aware profiles for compression and extraction.

The library embeds native 7‑Zip executables for Windows and Linux (x86 and x64) and exposes a simple async API. It supports named compression profiles (e.g., `Balanced`, `MaxCompression`, `LogArchiving`) and automatically adjusts parameters based on the runtime environment (OS, architecture, CPU count) to avoid crashes and balance resource usage.

---

## Features

- **Compression & extraction** – wrap directories into `.7z` archives, and extract archives to a target folder.
- **Named profiles** – define compression level, thread count, and process priority per profile; choose a profile per call or use a default.
- **Platform‑aware tuning** – caps compression level to 5 on 32‑bit processes (prevents out‑of‑memory), and resolves `Threads = 0` to the actual processor count.
- **Embedded binaries** – includes 7‑Zip executables for Windows (`7za.exe`, `7za.dll`) and Linux (`7zz`, `7zzs`) for x86 and x64; binaries are copied to the output directory.
- **Async API** – all operations are cancellable via `CancellationToken`.
- **Structured error reporting** – throws `SevenZipException` with exit code, arguments, and standard error.

---

## Class Diagram – Core Types & Relationships

```mermaid
classDiagram
  class SevenZipExecutor {
    -SevenZipOptions options
    -string sevenZipExePath
    +CompressDirectoryAsync()
    +ExtractArchiveAsync()
    -RunCommandAsync()
  }
  class ISevenZipExecutor {
    <<interface>>
    +CompressDirectoryAsync()
    +ExtractArchiveAsync()
  }
  class SevenZipOptions {
    +string ExePath
    +string DefaultProfile
    +IReadOnlyDictionary~string,SevenZipProfile~ Profiles
    +GetAdjustedProfile(profileName) SevenZipProfile
  }
  class SevenZipProfile {
    +int CompressionLevel
    +int Threads
    +ProcessPriorityClass ProcessPriority
  }
  class SevenZipException {
    +int ExitCode
    +string StandardError
    +string Arguments
  }
  ISevenZipExecutor <|.. SevenZipExecutor
  SevenZipExecutor --> SevenZipOptions
  SevenZipOptions --> SevenZipProfile : contains
  SevenZipExecutor ..> SevenZipException : throws
```

---

## Installation

Install the [SevenZipRunner NuGet package](https://www.nuget.org/packages/SevenZipRunner):

```bash
dotnet add package SevenZipRunner
```

The package targets .NET 6.0, 7.0, 8.0, 9.0, and 10.0.

---

## Usage

### 1. Register with dependency injection (optional)

If you use `Microsoft.Extensions.DependencyInjection`, register the executor with your configuration:

```csharp
using SevenZip;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

// In your startup
services.Configure<SevenZipOptions>(configuration.GetSection("SevenZip"));
services.AddSingleton<ISevenZipExecutor, SevenZipExecutor>();
```

### 2. Direct instantiation

```csharp
var options = new SevenZipOptions
{
    ExePath = "SevenZip",           // default – relative to base directory
    DefaultProfile = "Balanced"
};
var executor = new SevenZipExecutor(options);
```

### 3. Compress a directory

```csharp
await executor.CompressDirectoryAsync(
    sourceDirectory: @"C:\data\logs",
    destinationArchive: @"C:\backups\logs.7z",
    profileName: "LogArchiving",      // optional; falls back to DefaultProfile
    cancellationToken: default
);
```

#### Compression Workflow (Sequence Diagram)

```mermaid
sequenceDiagram
  participant Client
  participant Executor as SevenZipExecutor
  participant Options as SevenZipOptions
  participant Profile as SevenZipProfile
  participant Process as 7z Process

  Client->>Executor: CompressDirectoryAsync(src, dest, profileName)
  Executor->>Options: GetAdjustedProfile(profileName)
  Options->>Options: resolve default if null
  Options->>Profile: get named profile
  Options->>Options: cap compression if 32-bit
  Options->>Options: set threads = CPU count if 0
  Options-->>Executor: adjusted profile
  Executor->>Executor: build 7z arguments (a -t7z -mx=... -mmt...)
  Executor->>Process: start 7za.exe with args
  Process-->>Executor: stdout/stderr
  Executor->>Process: wait for exit
  alt exit code == 0
    Process-->>Executor: success
  else
    Process-->>Executor: non-zero exit
    Executor->>Client: throw SevenZipException
  end
```

### 4. Extract an archive

```csharp
await executor.ExtractArchiveAsync(
    sourceArchive: @"C:\backups\logs.7z",
    destinationDirectory: @"C:\restore\logs",
    profileName: "Balanced",
    cancellationToken: default
);
```

#### Extraction Workflow (Sequence Diagram)

```mermaid
sequenceDiagram
  participant Client
  participant Executor as SevenZipExecutor
  participant Options as SevenZipOptions
  participant Process as 7z Process

  Client->>Executor: ExtractArchiveAsync(src, dest, profileName)
  Executor->>Options: GetAdjustedProfile(profileName)
  Options-->>Executor: adjusted profile (priority only)
  Executor->>Executor: build 7z arguments (x -o...)
  Executor->>Process: start 7za.exe with args
  Process-->>Executor: stdout/stderr
  Executor->>Process: wait for exit
  alt exit code == 0
    Process-->>Executor: success
  else
    Process-->>Executor: non-zero exit
    Executor->>Client: throw SevenZipException
  end
```

---

## Configuration

The library uses the options pattern. All settings are defined in `SevenZipOptions`.

### `SevenZipOptions` properties

| Property         | Type                                           | Default                       | Description                                                                            |
| ---------------- | ---------------------------------------------- | ----------------------------- | -------------------------------------------------------------------------------------- |
| `ExePath`        | `string`                                       | `"SevenZip"`                  | Base path (absolute or relative) where the OS/architecture‑specific subfolders reside. |
| `DefaultProfile` | `string`                                       | `"Balanced"`                  | Name of the profile used when no `profileName` is supplied.                            |
| `Profiles`       | `IReadOnlyDictionary<string, SevenZipProfile>` | Built‑in profiles (see below) | Named compression profiles.                                                            |

### `SevenZipProfile` properties

| Property           | Type                   | Default  | Description                                                                                         |
| ------------------ | ---------------------- | -------- | --------------------------------------------------------------------------------------------------- |
| `CompressionLevel` | `int`                  | `5`      | 1 (fastest) – 9 (ultra). On 32‑bit processes, values >5 are capped to 5.                            |
| `Threads`          | `int`                  | `0`      | Number of threads; `0` means “use all cores” (resolved at runtime to `Environment.ProcessorCount`). |
| `ProcessPriority`  | `ProcessPriorityClass` | `Normal` | Priority class for the 7‑Zip process.                                                               |

#### Platform‑Aware Adjustment Flowchart

```mermaid
flowchart TD
  A["GetAdjustedProfile(profileName)"] --> B{"profileName null?"}
  B -->|Yes| C["Use DefaultProfile"]
  B -->|No| D["Look up profile by name"]
  D --> E{"Profile exists?"}
  E -->|No| F["Throw ArgumentException"]
  E -->|Yes| G["Copy profile to adjustedProfile"]
  G --> H{"Is 32-bit process?"}
  H -->|Yes| I["Cap CompressionLevel to 5"]
  H -->|No| J["Keep original CompressionLevel"]
  I --> K{"Threads <= 0?"}
  J --> K
  K -->|Yes| L["Set Threads = Environment.ProcessorCount"]
  K -->|No| M["Keep original Threads"]
  L --> N["Return adjustedProfile"]
  M --> N
```

### Default profiles

| Name                | Compression | Threads | Priority    |
| ------------------- | ----------- | ------- | ----------- |
| `Balanced`          | 5           | 0       | Normal      |
| `Fastest`           | 1           | 0       | BelowNormal |
| `MaxCompression`    | 9           | 0       | Normal      |
| `LogArchiving`      | 1           | 2       | BelowNormal |
| `LuceneIndexBackup` | 7           | 0       | Normal      |

### Customising profiles

You can override the built‑in profiles or add new ones in your configuration file (e.g., `appsettings.json`):

```json
{
  "SevenZip": {
    "ExePath": "SevenZip",
    "DefaultProfile": "Balanced",
    "Profiles": {
      "Balanced": {
        "CompressionLevel": 5,
        "Threads": 0,
        "ProcessPriority": "Normal"
      },
      "Fastest": {
        "CompressionLevel": 1,
        "Threads": 0,
        "ProcessPriority": "BelowNormal"
      }
    }
  }
}
```

> **Note:** `ProcessPriority` is parsed from the string values defined in `System.Diagnostics.ProcessPriorityClass`.

### Platform‑aware adjustments

- **32‑bit memory constraint**: If the current process is 32‑bit (`!Environment.Is64BitProcess`), the compression level is capped at 5 to avoid exceeding the 2 GB address space.
- **Thread count**: A value of `0` is replaced with `Environment.ProcessorCount` at runtime.

---

## Error handling

All synchronous and asynchronous operations throw a `SevenZipException` if the underlying 7‑Zip process returns a non‑zero exit code.

```csharp
try
{
    await executor.CompressDirectoryAsync(...);
}
catch (SevenZipException ex)
{
    Console.WriteLine(ex.Message);
    Console.WriteLine($"Exit code: {ex.ExitCode}");
    Console.WriteLine($"Standard error: {ex.StandardError}");
    Console.WriteLine($"Arguments: {ex.Arguments}");
}
```

---

## Platform support

- **Windows**: x86 and x64 (uses `7za.exe`).
- **Linux**: x86 and x64 (uses `7zz`).

Other operating systems and architectures are not supported; a `PlatformNotSupportedException` is thrown when constructing the executor.

The required native binaries are included in the package under the `SevenZip` folder and are automatically copied to the output directory during build.

### Executable Resolution Flowchart

```mermaid
flowchart TD
  A["ResolveExecutablePath(basePath)"] --> B{"OS?"}
  B -->|Windows| C["osPart = windows, exeName = 7za.exe"]
  B -->|Linux| D["osPart = linux, exeName = 7zz"]
  B -->|Other| E["Throw PlatformNotSupportedException"]
  C --> F{"Architecture?"}
  D --> F
  F -->|X64| G["archPart = x64"]
  F -->|X86| H["archPart = x86"]
  F -->|Other| I["Throw PlatformNotSupportedException"]
  G --> J{"basePath rooted?"}
  H --> J
  J -->|No| K["Combine with AppContext.BaseDirectory"]
  J -->|Yes| L["Keep basePath"]
  K --> M["finalPath = basePath/osPart/archPart/exeName"]
  L --> M
  M --> N{"File exists?"}
  N -->|Yes| O["Return finalPath"]
  N -->|No| P["Throw FileNotFoundException"]
```

---

## License

This project is licensed under the [MIT License](LICENSE).

---

## Contributing

Issues and pull requests are welcome. Please ensure that changes are covered by tests and follow the existing coding style.

---

## Acknowledgements

This library bundles the [7‑Zip](https://www.7-zip.org/) executables (p7zip for Linux) under their respective licenses. The `7za.exe` and `7zz` files are redistributed as part of the NuGet package.
