using BcdEditTool.Interfaces.BootManager;
using BcdEditTool.Models.BootManager;
using Microsoft.Extensions.Logging;

namespace BcdEditTool.Services.BootManager;

/// <summary>
/// Facade coordinating storage volume scanning and BCD operations.
/// </summary>
public sealed class BootManagerFacade : IBootManagerFacade
{
    private readonly IBcdCommandService _bcdCommandService;
    private readonly IDriveDetectionService _driveDetectionService;
    private readonly ILogger<BootManagerFacade> _logger;

    public BootManagerFacade(
        IBcdCommandService bcdCommandService,
        IDriveDetectionService driveDetectionService,
        ILogger<BootManagerFacade> logger)
    {
        ArgumentNullException.ThrowIfNull(bcdCommandService);
        ArgumentNullException.ThrowIfNull(driveDetectionService);
        ArgumentNullException.ThrowIfNull(logger);

        _bcdCommandService = bcdCommandService;
        _driveDetectionService = driveDetectionService;
        _logger = logger;
    }

    public async Task<(IEnumerable<BcdEntry> Entries, IEnumerable<DriveVolumeInfo> Drives, BcdBootManagerConfig Config)> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading storage volumes and BCD configuration");

        var drivesTask = _driveDetectionService.DetectAllDrivesAsync(cancellationToken);
        var entriesTask = _bcdCommandService.GetBootLoaderEntriesAsync(cancellationToken);
        var configTask = _bcdCommandService.GetBootManagerConfigAsync(cancellationToken);
        var currentIdTask = _bcdCommandService.GetCurrentBootEntryIdentifierAsync(cancellationToken);

        await Task.WhenAll(drivesTask, entriesTask, configTask, currentIdTask);

        var drives = (await drivesTask).ToList();
        var rawEntries = (await entriesTask).ToList();
        var config = await configTask;
        var currentIdentifier = await currentIdTask;

        _logger.LogInformation("Enriching {EntryCount} BCD entries with {DriveCount} drives. Default GUID: {DefaultGuid}, Current GUID: {CurrentGuid}",
            rawEntries.Count, drives.Count, config.DefaultEntryIdentifier ?? "None", currentIdentifier ?? "Unknown");

        var enriched = new List<BcdEntry>(rawEntries.Count);

        foreach (var entry in rawEntries)
        {
            var isDefault = !string.IsNullOrWhiteSpace(config.DefaultEntryIdentifier) &&
                            string.Equals(entry.Identifier, config.DefaultEntryIdentifier, StringComparison.OrdinalIgnoreCase);

            var isCurrent = !string.IsNullOrWhiteSpace(currentIdentifier) &&
                            string.Equals(entry.Identifier, currentIdentifier, StringComparison.OrdinalIgnoreCase);

            var matchedVolume = _driveDetectionService.ResolveVolume(entry.OsDevice ?? entry.Device, drives);

            var driveLetter = matchedVolume?.DriveLetter;
            var detectedVersion = matchedVolume?.WindowsFriendlyEdition;

            enriched.Add(entry with
            {
                IsDefault = isDefault,
                IsCurrent = isCurrent,
                DriveLetter = driveLetter,
                AssociatedVolume = matchedVolume,
                DetectedWindowsVersion = detectedVersion
            });
        }

        // Order: Current OS first, Default OS next, then by description
        var ordered = enriched
            .OrderByDescending(e => e.IsCurrent)
            .ThenByDescending(e => e.IsDefault)
            .ThenBy(e => e.Description, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return (ordered, drives, config);
    }

    public Task<BcdOperationResult> UpdateDescriptionAsync(string identifier, string newDescription, CancellationToken cancellationToken = default) =>
        _bcdCommandService.UpdateDescriptionAsync(identifier, newDescription, cancellationToken);

    public Task<BcdOperationResult> SetDefaultBootEntryAsync(string identifier, CancellationToken cancellationToken = default) =>
        _bcdCommandService.SetDefaultBootEntryAsync(identifier, cancellationToken);

    public Task<BcdOperationResult> BootIntoEntryImmediatelyAsync(string identifier, CancellationToken cancellationToken = default) =>
        _bcdCommandService.BootIntoEntryImmediatelyAsync(identifier, cancellationToken);

    public Task<BcdOperationResult> SetBootTimeoutAsync(int timeoutSeconds, CancellationToken cancellationToken = default) =>
        _bcdCommandService.SetBootTimeoutAsync(timeoutSeconds, cancellationToken);

    public Task<BcdOperationResult> ExportBcdBackupAsync(string targetFilePath, CancellationToken cancellationToken = default) =>
        _bcdCommandService.ExportBcdBackupAsync(targetFilePath, cancellationToken);
}
