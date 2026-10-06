using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using BcdEditTool.Interfaces.BootManager;
using BcdEditTool.Models.BootManager;
using Microsoft.Extensions.Logging;

namespace BcdEditTool.Services.BootManager;

/// <summary>
/// Service responsible for scanning storage volumes, resolving DOS device paths, and detecting Windows installations.
/// </summary>
public sealed partial class DriveDetectionService : IDriveDetectionService
{
    private readonly ILogger<DriveDetectionService> _logger;

    public DriveDetectionService(ILogger<DriveDetectionService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    [DllImport("kernel32.dll", EntryPoint = "QueryDosDeviceW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint QueryDosDevice(string lpDeviceName, StringBuilder lpTargetPath, uint ucchMax);

    [GeneratedRegex(@"partition=\s*([a-zA-Z]):", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PartitionLetterRegex();

    [GeneratedRegex(@"partition=\s*(\\[^,\s]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PartitionDosDeviceRegex();

    public Task<IEnumerable<DriveVolumeInfo>> DetectAllDrivesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var detectedVolumes = new List<DriveVolumeInfo>();

        try
        {
            var systemDrives = DriveInfo.GetDrives();
            _logger.LogInformation("Scanning system drives. Found {DriveCount} physical/logical drives", systemDrives.Length);

            foreach (var drive in systemDrives)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!drive.IsReady)
                {
                    continue;
                }

                var driveLetter = drive.Name.TrimEnd('\\');
                var dosDevice = GetDosDevicePath(driveLetter);
                var label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "Local Disk" : drive.VolumeLabel;
                var (hasWindows, kernelVersion, buildNumber, friendlyEdition, systemRoot) = InspectWindowsInstallation(drive.RootDirectory.FullName);

                var volume = new DriveVolumeInfo(
                    DriveLetter: driveLetter,
                    VolumeLabel: label,
                    FileSystem: drive.DriveFormat,
                    DosDevicePath: dosDevice,
                    TotalBytes: drive.TotalSize,
                    FreeBytes: drive.AvailableFreeSpace,
                    HasWindowsInstallation: hasWindows,
                    WindowsKernelVersion: kernelVersion,
                    WindowsBuildNumber: buildNumber,
                    WindowsFriendlyEdition: friendlyEdition,
                    SystemRootPath: systemRoot);

                detectedVolumes.Add(volume);
                _logger.LogDebug("Detected drive {DriveLetter} ({VolumeLabel}) - HasWindows: {HasWindows}", driveLetter, label, hasWindows);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate system storage drives");
        }

        return Task.FromResult<IEnumerable<DriveVolumeInfo>>(detectedVolumes);
    }

    public DriveVolumeInfo? ResolveVolume(string? deviceSpecifier, IEnumerable<DriveVolumeInfo> knownVolumes)
    {
        if (string.IsNullOrWhiteSpace(deviceSpecifier))
        {
            return null;
        }

        var letterMatch = PartitionLetterRegex().Match(deviceSpecifier);
        if (letterMatch.Success)
        {
            var letter = $"{letterMatch.Groups[1].Value}:";
            return knownVolumes.FirstOrDefault(v => string.Equals(v.DriveLetter, letter, StringComparison.OrdinalIgnoreCase));
        }

        var dosMatch = PartitionDosDeviceRegex().Match(deviceSpecifier);
        if (dosMatch.Success)
        {
            var dosPath = dosMatch.Groups[1].Value;
            return knownVolumes.FirstOrDefault(v => string.Equals(v.DosDevicePath, dosPath, StringComparison.OrdinalIgnoreCase));
        }

        return knownVolumes.FirstOrDefault(v =>
            deviceSpecifier.Contains(v.DriveLetter, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(v.DosDevicePath) && deviceSpecifier.Contains(v.DosDevicePath, StringComparison.OrdinalIgnoreCase)));
    }

    private static string GetDosDevicePath(string driveLetter)
    {
        var buffer = new StringBuilder(1024);
        var result = QueryDosDevice(driveLetter, buffer, (uint)buffer.Capacity);
        if (result == 0)
        {
            return string.Empty;
        }

        var path = buffer.ToString().TrimEnd('\0');
        var nullIndex = path.IndexOf('\0', StringComparison.Ordinal);
        return nullIndex >= 0 ? path[..nullIndex] : path;
    }

    private (bool HasWindows, string? KernelVersion, int? BuildNumber, string? FriendlyEdition, string? SystemRoot) InspectWindowsInstallation(string rootDirectory)
    {
        try
        {
            var windowsDir = Path.Combine(rootDirectory, "Windows");
            if (!Directory.Exists(windowsDir))
            {
                return (false, null, null, null, null);
            }

            var ntoskrnlPath = Path.Combine(windowsDir, "System32", "ntoskrnl.exe");
            if (!File.Exists(ntoskrnlPath))
            {
                return (true, null, null, "Windows", windowsDir);
            }

            var versionInfo = FileVersionInfo.GetVersionInfo(ntoskrnlPath);
            var build = versionInfo.ProductBuildPart;
            var friendly = DetermineFriendlyWindowsName(build);
            var fullVersion = $"{versionInfo.ProductMajorPart}.{versionInfo.ProductMinorPart}.{versionInfo.ProductBuildPart}.{versionInfo.ProductPrivatePart}";

            return (true, fullVersion, build, friendly, windowsDir);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not inspect Windows installation at {RootDirectory}", rootDirectory);
            return (false, null, null, null, null);
        }
    }

    private static string DetermineFriendlyWindowsName(int buildNumber) => buildNumber switch
    {
        >= 26100 => "Windows 11 (24H2)",
        >= 22631 => "Windows 11 (23H2)",
        >= 22621 => "Windows 11 (22H2)",
        >= 22000 => "Windows 11 (21H2)",
        >= 19045 => "Windows 10 (22H2)",
        >= 19044 => "Windows 10 (21H2)",
        >= 19043 => "Windows 10 (21H1)",
        >= 19042 => "Windows 10 (20H2)",
        >= 19041 => "Windows 10 (2004)",
        >= 18363 => "Windows 10 (1909)",
        >= 17763 => "Windows 10 / Server 2019",
        >= 14393 => "Windows 10 / Server 2016",
        >= 9600 => "Windows 8.1 / Server 2012 R2",
        >= 7601 => "Windows 7 (SP1)",
        _ => $"Windows NT (Build {buildNumber})"
    };
}
