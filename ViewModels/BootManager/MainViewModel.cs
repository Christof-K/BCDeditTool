using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using System.Windows.Input;
using BcdEditTool.Configuration.BootManager;
using BcdEditTool.Interfaces.BootManager;
using BcdEditTool.Models.BootManager;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BcdEditTool.ViewModels.BootManager;

/// <summary>
/// Root ViewModel orchestrating the BCD boot manager user interface.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IBootManagerFacade _bootManagerFacade;
    private readonly IOptions<BootManagerOptions> _options;
    private readonly ILogger<MainViewModel> _logger;

    private bool _isLoading;
    private bool _isElevated;
    private bool _isError;
    private string _statusMessage = "Ready";
    private DateTimeOffset? _statusTimestamp;
    private int _bootTimeoutSeconds = 30;
    private string? _defaultEntryGuid;

    public MainViewModel(
        IBootManagerFacade bootManagerFacade,
        IOptions<BootManagerOptions> options,
        ILogger<MainViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(bootManagerFacade);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _bootManagerFacade = bootManagerFacade;
        _options = options;
        _logger = logger;

        Entries = [];
        Drives = [];

        _isElevated = CheckElevationStatus();

        RefreshCommand = new AsyncRelayCommand(RefreshAllAsync);
        SaveTimeoutCommand = new AsyncRelayCommand(SaveTimeoutAsync);
        RestartElevatedCommand = new RelayCommand(RestartElevated);

        _bootTimeoutSeconds = _options.Value.DefaultTimeoutSeconds;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<BcdEntryViewModel> Entries { get; }
    public ObservableCollection<DriveVolumeInfo> Drives { get; }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetField(ref _isLoading, value);
    }

    public bool IsElevated
    {
        get => _isElevated;
        set => SetField(ref _isElevated, value);
    }

    public bool IsError
    {
        get => _isError;
        set => SetField(ref _isError, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public DateTimeOffset? StatusTimestamp
    {
        get => _statusTimestamp;
        set => SetField(ref _statusTimestamp, value);
    }

    public int BootTimeoutSeconds
    {
        get => _bootTimeoutSeconds;
        set => SetField(ref _bootTimeoutSeconds, value);
    }

    public string? DefaultEntryGuid
    {
        get => _defaultEntryGuid;
        set => SetField(ref _defaultEntryGuid, value);
    }

    public int TotalWindowsInstallationsCount => Drives.Count(d => d.HasWindowsInstallation);

    public bool HasEntries => Entries.Count > 0;

    public ICommand RefreshCommand { get; }
    public ICommand SaveTimeoutCommand { get; }
    public ICommand RestartElevatedCommand { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_options.Value.AutoRefreshOnStartup)
        {
            await RefreshAllAsync(cancellationToken);
        }
    }

    public async Task RefreshAllAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        SetStatus("Scanning all storage drives and BCD boot entries...", false);

        try
        {
            var (loadedEntries, loadedDrives, config) = await _bootManagerFacade.LoadAllAsync(cancellationToken);

            Drives.Clear();
            foreach (var drive in loadedDrives)
            {
                Drives.Add(drive);
            }

            Entries.Clear();
            foreach (var entry in loadedEntries)
            {
                var entryVm = new BcdEntryViewModel(
                    entry,
                    OnUpdateDescriptionAsync,
                    OnSetDefaultAsync,
                    OnInstantBootAsync);

                Entries.Add(entryVm);
            }

            BootTimeoutSeconds = config.TimeoutSeconds;
            DefaultEntryGuid = config.DefaultEntryIdentifier;

            OnPropertyChanged(nameof(TotalWindowsInstallationsCount));
            OnPropertyChanged(nameof(HasEntries));

            if (Entries.Count == 0 && !IsElevated)
            {
                SetStatus("No BCD entries could be loaded. Application must run with administrator privileges.", true);
            }
            else
            {
                SetStatus($"Successfully loaded {Entries.Count} boot entries and {Drives.Count} disk volumes.", false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh boot manager information");
            SetStatus($"Error loading BCD store: {ex.Message}", true);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task OnUpdateDescriptionAsync(BcdEntryViewModel entryVm, CancellationToken cancellationToken)
    {
        SetStatus($"Updating display name for {entryVm.Identifier}...", false);

        var result = await _bootManagerFacade.UpdateDescriptionAsync(entryVm.Identifier, entryVm.EditedDescription, cancellationToken);
        if (result.IsSuccess)
        {
            entryVm.Description = entryVm.EditedDescription;
            SetStatus($"Display name updated to '{entryVm.Description}'.", false);
        }
        else
        {
            SetStatus($"Failed to update display name: {result.StandardError}", true);
        }
    }

    private async Task OnSetDefaultAsync(BcdEntryViewModel entryVm, CancellationToken cancellationToken)
    {
        SetStatus($"Setting {entryVm.Identifier} as default OS...", false);

        var result = await _bootManagerFacade.SetDefaultBootEntryAsync(entryVm.Identifier, cancellationToken);
        if (result.IsSuccess)
        {
            DefaultEntryGuid = entryVm.Identifier;

            foreach (var item in Entries)
            {
                item.IsDefault = string.Equals(item.Identifier, entryVm.Identifier, StringComparison.OrdinalIgnoreCase);
            }

            SetStatus($"Default operating system set to '{entryVm.Description}' ({entryVm.Identifier}).", false);
        }
        else
        {
            SetStatus($"Failed to set default OS: {result.StandardError}", true);
        }
    }

    private async Task OnInstantBootAsync(BcdEntryViewModel entryVm, CancellationToken cancellationToken)
    {
        SetStatus($"⚡ Initiating instant reboot into '{entryVm.Description}' ({entryVm.Identifier})...", false);

        var result = await _bootManagerFacade.BootIntoEntryImmediatelyAsync(entryVm.Identifier, cancellationToken);
        if (result.IsSuccess)
        {
            SetStatus($"System reboot initiated for '{entryVm.Description}' ({entryVm.Identifier}).", false);
        }
        else
        {
            SetStatus($"Failed to trigger instant reboot: {result.StandardError}", true);
        }
    }

    private async Task SaveTimeoutAsync(CancellationToken cancellationToken)
    {
        SetStatus($"Setting boot timeout to {BootTimeoutSeconds}s...", false);

        var result = await _bootManagerFacade.SetBootTimeoutAsync(BootTimeoutSeconds, cancellationToken);
        if (result.IsSuccess)
        {
            SetStatus($"Boot timeout configured to {BootTimeoutSeconds} seconds.", false);
        }
        else
        {
            SetStatus($"Failed to set boot timeout: {result.StandardError}", true);
        }
    }

    public async Task ExportBackupAsync(string targetFilePath, CancellationToken cancellationToken = default)
    {
        SetStatus($"Exporting BCD backup to '{targetFilePath}'...", false);

        var result = await _bootManagerFacade.ExportBcdBackupAsync(targetFilePath, cancellationToken);
        if (result.IsSuccess)
        {
            SetStatus($"BCD store successfully backed up to '{targetFilePath}'.", false);
        }
        else
        {
            SetStatus($"Failed to export BCD backup: {result.StandardError}", true);
        }
    }

    private void RestartElevated()
    {
        try
        {
            var processModule = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(processModule))
            {
                SetStatus("Unable to determine application executable path for elevation.", true);
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = processModule,
                UseShellExecute = true,
                Verb = "runas"
            };

            Process.Start(startInfo);
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to relaunch elevated");
            SetStatus($"Could not elevate application: {ex.Message}", true);
        }
    }

    private static bool CheckElevationStatus()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsError = isError;
        StatusTimestamp = DateTimeOffset.Now;
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
