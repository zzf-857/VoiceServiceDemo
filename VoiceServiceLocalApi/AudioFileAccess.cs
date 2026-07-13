namespace VoiceServiceLocalApi;

public static class AudioFileAccess
{
    public static string Resolve(string outputDirectory, string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (Path.IsPathRooted(fileName) ||
            fileName.Contains(Path.DirectorySeparatorChar) ||
            fileName.Contains(Path.AltDirectorySeparatorChar) ||
            !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
        {
            throw new ArgumentException("Only a safe audio file name is allowed.", nameof(fileName));
        }

        var root = Path.GetFullPath(outputDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var resolved = Path.GetFullPath(Path.Combine(root, fileName));
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!resolved.StartsWith(root, comparison))
            throw new ArgumentException("The audio file must stay inside the configured output directory.", nameof(fileName));

        return resolved;
    }

    public static string GetContentType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".flac" => "audio/flac",
            ".opus" or ".ogg" or ".ogg_opus" => "audio/ogg",
            ".m4a" => "audio/mp4",
            ".aac" => "audio/aac",
            ".ulaw" => "audio/basic",
            _ => "application/octet-stream"
        };
}
