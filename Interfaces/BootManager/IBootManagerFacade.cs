using BcdEditTool.Models.BootManager;

namespace BcdEditTool.Interfaces.BootManager;

/// <summary>
/// High-level facade coordinating BCD commands, disk detection, and enriched entry mapping.
/// </summary>
public interface IBootManagerFacade
{
    /// <summary>
    /// Loads all drives, BCD entries, and boot manager configuration into a synchronized snapshot.
    /// </summary>
    Task<(IEnumerable<BcdEntry> Entries, IEnumerable<DriveVolumeInfo> Drives, BcdBootManagerConfig Config)> LoadAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the display name for a specific boot entry.
    /// </summary>
    Task<BcdOperationResult> UpdateDescriptionAsync(string identifier, string newDescription, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a specific boot entry as default by its unique GUID identifier.
    /// </summary>
    Task<BcdOperationResult> SetDefaultBootEntryAsync(string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Instantly boots into the selected operating system by its unique GUID without user confirmation.
    /// </summary>
    Task<BcdOperationResult> BootIntoEntryImmediatelyAsync(string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the boot selection timeout in seconds.
    /// </summary>
    Task<BcdOperationResult> SetBootTimeoutAsync(int timeoutSeconds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports the BCD store to the specified file path.
    /// </summary>
    Task<BcdOperationResult> ExportBcdBackupAsync(string targetFilePath, CancellationToken cancellationToken = default);
}
