using System.Collections.Concurrent;

namespace VoiceServiceLocalApi;

public sealed class GeneratedAudioRegistry
{
    private readonly ConcurrentDictionary<string, string> _files = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    public void Register(string outputDirectory, string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var fileName = Path.GetFileName(filePath);
        var allowedPath = AudioFileAccess.Resolve(outputDirectory, fileName);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(Path.GetFullPath(filePath), allowedPath, comparison))
            throw new ArgumentException("The generated audio file is outside the configured output directory.", nameof(filePath));

        _files[fileName] = allowedPath;
    }

    public bool TryResolve(string outputDirectory, string fileName, out string filePath)
    {
        filePath = "";
        var allowedPath = AudioFileAccess.Resolve(outputDirectory, fileName);
        if (!_files.TryGetValue(fileName, out var registeredPath))
            return false;

        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(registeredPath, allowedPath, comparison))
            return false;

        filePath = allowedPath;
        return true;
    }
}
