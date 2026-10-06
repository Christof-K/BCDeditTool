using System.ComponentModel.DataAnnotations;

namespace BcdEditTool.Configuration.BootManager;

/// <summary>
/// Strongly typed configuration options for the BCD boot manager feature.
/// </summary>
public sealed class BootManagerOptions
{
    /// <summary>
    /// Configuration section key used in application configuration files.
    /// </summary>
    public static readonly string SectionName = "BootManager";

    /// <summary>
    /// Path or executable name for bcdedit.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string BcdExecutable { get; set; } = "bcdedit.exe";

    /// <summary>
    /// Path or executable name for shutdown.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string ShutdownExecutable { get; set; } = "shutdown.exe";

    /// <summary>
    /// Default timeout in seconds for boot menu selection.
    /// </summary>
    [Range(0, 300)]
    public int DefaultTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Flag indicating whether boot entries and disk volumes should be automatically refreshed upon application startup.
    /// </summary>
    public bool AutoRefreshOnStartup { get; set; } = true;
}
