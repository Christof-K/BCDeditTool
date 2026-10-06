namespace BcdEditTool.Models.BootManager;

/// <summary>
/// Represents global Windows Boot Manager configuration properties.
/// </summary>
public sealed record BcdBootManagerConfig(
    string Identifier,
    string? DefaultEntryIdentifier,
    int TimeoutSeconds,
    IReadOnlyList<string> DisplayOrderGuids)
{
    /// <summary>
    /// Returns an empty default configuration.
    /// </summary>
    public static BcdBootManagerConfig Empty => new(string.Empty, null, 30, []);
}
