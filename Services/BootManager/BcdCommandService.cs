using System.Diagnostics;
using System.Text.RegularExpressions;
using BcdEditTool.Configuration.BootManager;
using BcdEditTool.Interfaces.BootManager;
using BcdEditTool.Models.BootManager;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BcdEditTool.Services.BootManager;

/// <summary>
/// Service executing administrative bcdedit.exe and system commands.
/// </summary>
public sealed partial class BcdCommandService : IBcdCommandService
{
    private static readonly Regex GuidRegex = CreateGuidRegex();
    private static readonly Regex KeyValueRegex = CreateKeyValueRegex();

    private readonly IOptions<BootManagerOptions> _options;
    private readonly ILogger<BcdCommandService> _logger;

    public BcdCommandService(
        IOptions<BootManagerOptions> options,
        ILogger<BcdCommandService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _logger = logger;
    }

    [GeneratedRegex(@"^\{[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\}$", RegexOptions.Compiled)]
    private static partial Regex CreateGuidRegex();

    [GeneratedRegex(@"^\s*([a-zA-Z0-9_\-]+)\s{2,}(.+)$", RegexOptions.Compiled)]
    private static partial Regex CreateKeyValueRegex();

    public async Task<IEnumerable<BcdEntry>> GetBootLoaderEntriesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Querying all Windows Boot Loader entries via bcdedit /enum osloader /v");
        var result = await RunProcessAsync(_options.Value.BcdExecutable, cancellationToken, "/enum", "osloader", "/v");

        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to enumerate BCD boot loaders: {Error}", result.StandardError);
            return [];
        }

        var entries = ParseOsLoaderEntries(result.StandardOutput);
        return entries;
    }

    public async Task<BcdBootManagerConfig> GetBootManagerConfigAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Querying Windows Boot Manager configuration via bcdedit /enum {{bootmgr}} /v");
        var result = await RunProcessAsync(_options.Value.BcdExecutable, cancellationToken, "/enum", "{bootmgr}", "/v");

        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to enumerate Boot Manager config: {Error}", result.StandardError);
            return BcdBootManagerConfig.Empty;
        }

        return ParseBootManagerConfig(result.StandardOutput);
    }

    public async Task<string?> GetCurrentBootEntryIdentifierAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Querying current OS identifier via bcdedit /enum {{current}} /v");
        var result = await RunProcessAsync(_options.Value.BcdExecutable, cancellationToken, "/enum", "{current}", "/v");

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Could not determine current boot entry identifier: {Error}", result.StandardError);
            return null;
        }

        return ExtractIdentifier(result.StandardOutput);
    }

    public async Task<BcdOperationResult> UpdateDescriptionAsync(string identifier, string newDescription, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(newDescription);
        ValidateStrictGuidIdentifier(identifier);

        _logger.LogInformation("Updating description for entry {Identifier} to {NewDescription}", identifier, newDescription);

        var arguments = new[] { "/set", identifier, "description", newDescription };
        var result = await RunProcessAsync(_options.Value.BcdExecutable, cancellationToken, arguments);
        if (result.IsSuccess)
        {
            _logger.LogInformation("Successfully updated display name for entry {Identifier}", identifier);
            return BcdOperationResult.Success(FormatCommand(_options.Value.BcdExecutable, arguments), result.StandardOutput, $"Description successfully updated to '{newDescription}'");
        }

        _logger.LogError("Failed to update description for entry {Identifier}: {Error}", identifier, result.StandardError);
        return result;
    }

    public async Task<BcdOperationResult> SetDefaultBootEntryAsync(string identifier, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        ValidateStrictGuidIdentifier(identifier);

        _logger.LogInformation("Setting default boot entry to {Identifier}", identifier);
        var arguments = new[] { "/default", identifier };
        var result = await RunProcessAsync(_options.Value.BcdExecutable, cancellationToken, arguments);
        if (result.IsSuccess)
        {
            _logger.LogInformation("Successfully configured entry {Identifier} as default OS", identifier);
            return BcdOperationResult.Success(FormatCommand(_options.Value.BcdExecutable, arguments), result.StandardOutput, $"Boot entry '{identifier}' set as system default.");
        }

        _logger.LogError("Failed to set default boot entry to {Identifier}: {Error}", identifier, result.StandardError);
        return result;
    }

    public async Task<BcdOperationResult> BootIntoEntryImmediatelyAsync(string identifier, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        ValidateStrictGuidIdentifier(identifier);

        _logger.LogInformation("Configuring one-time boot sequence to {Identifier}", identifier);
        var bootSequenceArguments = new[] { "/bootsequence", identifier };
        var seqResult = await RunProcessAsync(_options.Value.BcdExecutable, cancellationToken, bootSequenceArguments);
        if (!seqResult.IsSuccess)
        {
            _logger.LogError("Failed to set bootsequence for {Identifier}: {Error}", identifier, seqResult.StandardError);
            return seqResult;
        }

        _logger.LogInformation("Triggering immediate restart into {Identifier} via {ShutdownExe}", identifier, _options.Value.ShutdownExecutable);
        var shutdownArguments = new[] { "/r", "/t", "0", "/f" };
        var shutdownResult = await RunProcessAsync(_options.Value.ShutdownExecutable, cancellationToken, shutdownArguments);

        if (!shutdownResult.IsSuccess)
        {
            _logger.LogError("Failed to execute shutdown: {Error}", shutdownResult.StandardError);
            return shutdownResult;
        }

        return BcdOperationResult.Success(
            $"{FormatCommand(_options.Value.BcdExecutable, bootSequenceArguments)} && {FormatCommand(_options.Value.ShutdownExecutable, shutdownArguments)}",
            shutdownResult.StandardOutput,
            $"Reboot initiated into operating system {identifier}");
    }

    public async Task<BcdOperationResult> SetBootTimeoutAsync(int timeoutSeconds, CancellationToken cancellationToken = default)
    {
        if (timeoutSeconds < 0 || timeoutSeconds > 300)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), "Timeout must be between 0 and 300 seconds.");
        }

        _logger.LogInformation("Configuring boot timeout to {Timeout} seconds", timeoutSeconds);
        var arguments = new[] { "/timeout", timeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) };
        var result = await RunProcessAsync(_options.Value.BcdExecutable, cancellationToken, arguments);
        if (result.IsSuccess)
        {
            return BcdOperationResult.Success(FormatCommand(_options.Value.BcdExecutable, arguments), result.StandardOutput, $"Boot timeout set to {timeoutSeconds}s.");
        }

        return result;
    }

    public async Task<BcdOperationResult> ExportBcdBackupAsync(string targetFilePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFilePath);

        _logger.LogInformation("Exporting BCD backup to {TargetPath}", targetFilePath);
        var arguments = new[] { "/export", targetFilePath };
        var result = await RunProcessAsync(_options.Value.BcdExecutable, cancellationToken, arguments);
        if (result.IsSuccess)
        {
            return BcdOperationResult.Success(FormatCommand(_options.Value.BcdExecutable, arguments), result.StandardOutput, $"BCD successfully backed up to '{targetFilePath}'.");
        }

        return result;
    }

    private static void ValidateStrictGuidIdentifier(string identifier)
    {
        if (string.Equals(identifier, "default", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(identifier, "{default}", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The literal identifier 'default' is explicitly disallowed. Always use the unique GUID identifier.");
        }

        if (!GuidRegex.IsMatch(identifier))
        {
            throw new ArgumentException($"Identifier '{identifier}' is not a valid GUID formatted as {{xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx}}.", nameof(identifier));
        }
    }

    private static async Task<BcdOperationResult> RunProcessAsync(string executable, CancellationToken cancellationToken, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.Default,
            StandardErrorEncoding = System.Text.Encoding.Default
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var command = FormatCommand(executable, arguments);

        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (InvalidOperationException) when (process.HasExited)
                {
                }

                await process.WaitForExitAsync(CancellationToken.None);
                await Task.WhenAll(stdoutTask, stderrTask);
                throw;
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode == 0)
            {
                return BcdOperationResult.Success(command, stdout, "Command completed successfully.");
            }

            var combinedError = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            return BcdOperationResult.Failure(command, combinedError, process.ExitCode, $"Process exited with code {process.ExitCode}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return BcdOperationResult.Failure(command, ex.Message, -1, $"Failed to launch command: {ex.Message}");
        }
    }

    private static string FormatCommand(string executable, IEnumerable<string> arguments)
    {
        var formattedArguments = arguments.Select(argument =>
            "\"" + argument.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"");
        return $"{executable} {string.Join(" ", formattedArguments)}";
    }

    private static IEnumerable<BcdEntry> ParseOsLoaderEntries(string rawText)
    {
        var entries = new List<BcdEntry>();
        var blocks = SplitIntoSections(rawText);

        foreach (var block in blocks)
        {
            var properties = ParseProperties(block);
            var identifier = GetValue(properties, "identifier", "bezeichner");
            if (string.IsNullOrWhiteSpace(identifier))
            {
                continue;
            }

            var description = GetValue(properties, "description", "anzeigename") ?? "Windows Installation";
            var device = GetValue(properties, "device");
            var osDevice = GetValue(properties, "osdevice");
            var path = GetValue(properties, "path");
            var systemRoot = GetValue(properties, "systemroot");

            entries.Add(new BcdEntry(
                Identifier: identifier,
                Description: description,
                Device: device,
                OsDevice: osDevice,
                Path: path,
                SystemRoot: systemRoot,
                IsDefault: false,
                IsCurrent: false,
                DriveLetter: null,
                AssociatedVolume: null,
                DetectedWindowsVersion: null));
        }

        return entries;
    }

    private static BcdBootManagerConfig ParseBootManagerConfig(string rawText)
    {
        var properties = ParseProperties(rawText);
        var identifier = GetValue(properties, "identifier", "bezeichner") ?? "{bootmgr}";
        var defaultGuid = GetValue(properties, "default");
        var timeoutText = GetValue(properties, "timeout");

        var timeout = int.TryParse(timeoutText, out var parsedTimeout) ? parsedTimeout : 30;

        var displayOrders = properties
            .Where(p => string.Equals(p.Key, "displayorder", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Value)
            .ToList();

        return new BcdBootManagerConfig(identifier, defaultGuid, timeout, displayOrders);
    }

    private static string? ExtractIdentifier(string rawText)
    {
        var properties = ParseProperties(rawText);
        return GetValue(properties, "identifier", "bezeichner");
    }

    private static IEnumerable<string> SplitIntoSections(string rawText)
    {
        var lines = rawText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var currentSection = new List<string>();

        foreach (var line in lines)
        {
            if (line.Trim().StartsWith("---", StringComparison.Ordinal))
            {
                if (currentSection.Count > 1)
                {
                    // Section separator found
                    var headerLine = currentSection[^1];
                    currentSection.RemoveAt(currentSection.Count - 1);
                    if (currentSection.Count > 0)
                    {
                        yield return string.Join(Environment.NewLine, currentSection);
                    }

                    currentSection.Clear();
                    currentSection.Add(headerLine);
                }

                continue;
            }

            currentSection.Add(line);
        }

        if (currentSection.Count > 0)
        {
            yield return string.Join(Environment.NewLine, currentSection);
        }
    }

    private static List<KeyValuePair<string, string>> ParseProperties(string text)
    {
        var result = new List<KeyValuePair<string, string>>();
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        string? currentKey = null;

        foreach (var line in lines)
        {
            var match = KeyValueRegex.Match(line);
            if (match.Success)
            {
                currentKey = match.Groups[1].Value.Trim();
                var value = match.Groups[2].Value.Trim();
                result.Add(new KeyValuePair<string, string>(currentKey, value));
            }
            else if (currentKey is not null && line.StartsWith("    ", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(line))
            {
                // Multi-line continuation such as displayorder
                result.Add(new KeyValuePair<string, string>(currentKey, line.Trim()));
            }
        }

        return result;
    }

    private static string? GetValue(IEnumerable<KeyValuePair<string, string>> pairs, params string[] possibleKeys)
    {
        foreach (var key in possibleKeys)
        {
            var match = pairs.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(match.Value))
            {
                return match.Value;
            }
        }

        return null;
    }
}
