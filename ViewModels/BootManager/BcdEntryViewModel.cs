using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using BcdEditTool.Models.BootManager;

namespace BcdEditTool.ViewModels.BootManager;

/// <summary>
/// ViewModel representing an individual BCD boot loader entry with editing and immediate boot capabilities.
/// </summary>
public sealed class BcdEntryViewModel : INotifyPropertyChanged
{
    private readonly Func<BcdEntryViewModel, CancellationToken, Task> _onUpdateDescription;
    private readonly Func<BcdEntryViewModel, CancellationToken, Task> _onSetDefault;
    private readonly Func<BcdEntryViewModel, CancellationToken, Task> _onInstantBoot;

    private string _description;
    private string _editedDescription = string.Empty;
    private bool _isDefault;
    private bool _isCurrent;
    private bool _isEditing;
    private bool _isProcessing;
    private string? _statusNotice;

    public BcdEntryViewModel(
        BcdEntry entry,
        Func<BcdEntryViewModel, CancellationToken, Task> onUpdateDescription,
        Func<BcdEntryViewModel, CancellationToken, Task> onSetDefault,
        Func<BcdEntryViewModel, CancellationToken, Task> onInstantBoot)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(onUpdateDescription);
        ArgumentNullException.ThrowIfNull(onSetDefault);
        ArgumentNullException.ThrowIfNull(onInstantBoot);

        Identifier = entry.Identifier;
        _description = entry.Description;
        Device = entry.Device;
        OsDevice = entry.OsDevice;
        Path = entry.Path;
        SystemRoot = entry.SystemRoot;
        _isDefault = entry.IsDefault;
        _isCurrent = entry.IsCurrent;
        DriveLetter = entry.DriveLetter;
        AssociatedVolume = entry.AssociatedVolume;
        DetectedWindowsVersion = entry.DetectedWindowsVersion;
        PartitionSummary = entry.PartitionSummary;

        _onUpdateDescription = onUpdateDescription;
        _onSetDefault = onSetDefault;
        _onInstantBoot = onInstantBoot;

        StartEditCommand = new RelayCommand(StartEdit);
        CancelEditCommand = new RelayCommand(CancelEdit);
        SaveEditCommand = new AsyncRelayCommand(SaveEditAsync, () => !string.IsNullOrWhiteSpace(EditedDescription));
        SetDefaultCommand = new AsyncRelayCommand(SetDefaultAsync, () => !IsDefault);
        InstantBootCommand = new AsyncRelayCommand(InstantBootAsync);
        CopyGuidCommand = new RelayCommand(CopyGuidToClipboard);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Identifier { get; }
    public string? Device { get; }
    public string? OsDevice { get; }
    public string? Path { get; }
    public string? SystemRoot { get; }
    public string? DriveLetter { get; }
    public DriveVolumeInfo? AssociatedVolume { get; }
    public string? DetectedWindowsVersion { get; }
    public string PartitionSummary { get; }

    public string Description
    {
        get => _description;
        set => SetField(ref _description, value);
    }

    public string EditedDescription
    {
        get => _editedDescription;
        set => SetField(ref _editedDescription, value);
    }

    public bool IsDefault
    {
        get => _isDefault;
        set => SetField(ref _isDefault, value);
    }

    public bool IsCurrent
    {
        get => _isCurrent;
        set => SetField(ref _isCurrent, value);
    }

    public bool IsEditing
    {
        get => _isEditing;
        set => SetField(ref _isEditing, value);
    }

    public bool IsProcessing
    {
        get => _isProcessing;
        set => SetField(ref _isProcessing, value);
    }

    public string? StatusNotice
    {
        get => _statusNotice;
        set => SetField(ref _statusNotice, value);
    }

    public ICommand StartEditCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand SaveEditCommand { get; }
    public ICommand SetDefaultCommand { get; }
    public ICommand InstantBootCommand { get; }
    public ICommand CopyGuidCommand { get; }

    private void StartEdit()
    {
        EditedDescription = Description;
        IsEditing = true;
    }

    private void CancelEdit()
    {
        IsEditing = false;
        EditedDescription = string.Empty;
    }

    private async Task SaveEditAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(EditedDescription))
        {
            return;
        }

        try
        {
            IsProcessing = true;
            await _onUpdateDescription(this, cancellationToken);
            IsEditing = false;
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private async Task SetDefaultAsync(CancellationToken cancellationToken)
    {
        try
        {
            IsProcessing = true;
            await _onSetDefault(this, cancellationToken);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private async Task InstantBootAsync(CancellationToken cancellationToken)
    {
        try
        {
            IsProcessing = true;
            await _onInstantBoot(this, cancellationToken);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private void CopyGuidToClipboard()
    {
        try
        {
            Clipboard.SetText(Identifier);
            StatusNotice = "GUID copied to clipboard!";
        }
        catch
        {
            // Ignore clipboard access issues
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
