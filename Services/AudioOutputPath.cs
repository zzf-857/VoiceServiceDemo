using System.IO;
using System.Security.Cryptography;

namespace VoiceServiceDemo.Services;

public static class AudioOutputPath
{
    private const int MaxReservationAttempts = 32;

    public static string Reserve(
        string directory,
        string vendorId,
        string extension,
        DateTimeOffset? timestamp = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(vendorId);
        ArgumentNullException.ThrowIfNull(extension);

        Directory.CreateDirectory(directory);

        var sanitizedVendorId = new string(vendorId
            .Select(character => char.IsLetterOrDigit(character) || character is '_' or '-' ? character : '_')
            .ToArray());
        var normalizedExtension = "." + extension.TrimStart('.').ToLowerInvariant();
        var filenameTimestamp = timestamp ?? DateTimeOffset.Now;
        var randomBytes = new byte[4];

        for (var attempt = 0; attempt < MaxReservationAttempts; attempt++)
        {
            RandomNumberGenerator.Fill(randomBytes);
            var suffix = Convert.ToHexString(randomBytes).ToLowerInvariant();
            var filename = $"{sanitizedVendorId}_{filenameTimestamp:yyyyMMdd_HHmmss_fff}_{suffix}{normalizedExtension}";
            var path = Path.Combine(directory, filename);

            try
            {
                using var reservation = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                return path;
            }
            catch (IOException) when (File.Exists(path))
            {
                // Retry only when another writer reserved the same random filename first.
            }
        }

        throw new IOException($"Unable to reserve a unique audio output path after {MaxReservationAttempts} attempts in '{directory}'.");
    }
}
