namespace BcdEditTool.Models.BootManager;

/// <summary>
/// Represents a Windows Boot Loader entry in the BCD store.
/// </summary>
public sealed record BcdEntry(
    string Identifier,
    string Description,
    string? Device,
    string? OsDevice,
    string? Path,
    string? SystemRoot,
    bool IsDefault,
    bool IsCurrent,
    string? DriveLetter,
    DriveVolumeInfo? AssociatedVolume,
    string? DetectedWindowsVersion)
{
    /// <summary>
    /// Gets a value indicating whether this entry has an associated local drive.
    /// </summary>
    public bool HasAssociatedVolume => AssociatedVolume is not null;

    /// <summary>
    /// Gets a formatted display summary for storage partition and drive location.
    /// </summary>
    public string PartitionSummary => DriveLetter is not null
        ? $"{DriveLetter} ({(AssociatedVolume?.VolumeLabel.Length > 0 ? AssociatedVolume.VolumeLabel : "Local Disk")})"
        : Device ?? "Unknown Partition";
}
