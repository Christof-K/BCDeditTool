using BcdEditTool.Models.BootManager;

namespace BcdEditTool.Interfaces.BootManager;

/// <summary>
/// Provides administrative interactions with Windows BCD (Boot Configuration Data) store.
/// </summary>
public interface IBcdCommandService
{
    /// <summary>
    /// Retrieves all Windows Boot Loader entries using their unique GUID identifiers.
    /// </summary>
    Task<IEnumerable<BcdEntry>> GetBootLoaderEntriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves Windows Boot Manager global configuration including default entry GUID and timeout.
    /// </summary>
    Task<BcdBootManagerConfig> GetBootManagerConfigAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the unique GUID identifier for the currently running Windows operating system.
    /// </summary>
    Task<string?> GetCurrentBootEntryIdentifierAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the display name (description) for a specific BCD entry using its unique GUID identifier.
    /// </summary>
    Task<BcdOperationResult> UpdateDescriptionAsync(string identifier, string newDescription, CancellationToken cancellationToken = default);

    /// <summary>
    /// Configures the specified BCD entry as default using its unique GUID identifier.
    /// Never passes the literal string 'default'.
    /// </summary>
    Task<BcdOperationResult> SetDefaultBootEntryAsync(string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the one-time bootsequence to the specified unique GUID identifier and immediately triggers a system restart.
    /// </summary>
    Task<BcdOperationResult> BootIntoEntryImmediatelyAsync(string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the global boot selection timeout in seconds.
    /// </summary>
    Task<BcdOperationResult> SetBootTimeoutAsync(int timeoutSeconds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports a full backup copy of the current BCD store to the specified target file path.
    /// </summary>
    Task<BcdOperationResult> ExportBcdBackupAsync(string targetFilePath, CancellationToken cancellationToken = default);
}
