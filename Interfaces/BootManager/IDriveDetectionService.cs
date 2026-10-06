using BcdEditTool.Models.BootManager;

namespace BcdEditTool.Interfaces.BootManager;

/// <summary>
/// Provides storage drive inspection and Windows operating system detection across partitions.
/// </summary>
public interface IDriveDetectionService
{
    /// <summary>
    /// Scans all system storage drives, gathering volume information and identifying any installed Windows instances.
    /// </summary>
    Task<IEnumerable<DriveVolumeInfo>> DetectAllDrivesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to map a raw device path or partition identifier to a recognized drive volume.
    /// </summary>
    DriveVolumeInfo? ResolveVolume(string? deviceSpecifier, IEnumerable<DriveVolumeInfo> knownVolumes);
}
