namespace BcdEditTool.Models.BootManager;

/// <summary>
/// Represents a local storage volume detected during startup scan.
/// </summary>
public sealed record DriveVolumeInfo(
    string DriveLetter,
    string VolumeLabel,
    string FileSystem,
    string DosDevicePath,
    long TotalBytes,
    long FreeBytes,
    bool HasWindowsInstallation,
    string? WindowsKernelVersion,
    int? WindowsBuildNumber,
    string? WindowsFriendlyEdition,
    string? SystemRootPath)
{
    /// <summary>
    /// Gets formatted human-readable total capacity string.
    /// </summary>
    public string FormattedTotalSize => FormatBytes(TotalBytes);

    /// <summary>
    /// Gets formatted human-readable free space string.
    /// </summary>
    public string FormattedFreeSize => FormatBytes(FreeBytes);

    /// <summary>
    /// Gets calculated free disk space percentage (0 to 100).
    /// </summary>
    public double FreePercentage => TotalBytes > 0
        ? Math.Round((double)FreeBytes / TotalBytes * 100, 1)
        : 0;

    /// <summary>
    /// Gets calculated used disk space percentage (0 to 100).
    /// </summary>
    public double UsedPercentage => TotalBytes > 0
        ? Math.Round((double)(TotalBytes - FreeBytes) / TotalBytes * 100, 1)
        : 0;

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 GB";
        }

        var gigabytes = bytes / (1024.0 * 1024.0 * 1024.0);
        if (gigabytes >= 1000)
        {
            var terabytes = gigabytes / 1024.0;
            return $"{terabytes:F2} TB";
        }

        return $"{gigabytes:F1} GB";
    }
}
