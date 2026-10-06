namespace BcdEditTool.Models.BootManager;

/// <summary>
/// Encapsulates the execution result of an administrative system command.
/// </summary>
public sealed record BcdOperationResult(
    bool IsSuccess,
    string Command,
    string StandardOutput,
    string StandardError,
    int ExitCode,
    string Message)
{
    /// <summary>
    /// Creates a successful operation result instance.
    /// </summary>
    public static BcdOperationResult Success(string command, string standardOutput, string message) =>
        new(true, command, standardOutput, string.Empty, 0, message);

    /// <summary>
    /// Creates a failed operation result instance.
    /// </summary>
    public static BcdOperationResult Failure(string command, string errorOutput, int exitCode, string message) =>
        new(false, command, string.Empty, errorOutput, exitCode, message);
}
