using VoiceServiceLocalApi;

namespace VoiceServiceLocalApi.Tests;

public sealed class AudioFileAccessTests
{
    [Fact]
    public void Audio_path_accepts_a_safe_file_name_inside_output_directory()
    {
        var output = Path.Combine(Path.GetTempPath(), "voiceops-api-output", Guid.NewGuid().ToString("N"));
        var resolved = AudioFileAccess.Resolve(output, "voice.mp3");

        Assert.Equal(Path.Combine(output, "voice.mp3"), resolved);
    }

    [Theory]
    [InlineData("../secret.mp3")]
    [InlineData("..\\secret.mp3")]
    [InlineData("sub/voice.mp3")]
    [InlineData("sub\\voice.mp3")]
    [InlineData("C:\\secret.mp3")]
    public void Audio_path_rejects_parent_absolute_and_nested_paths(string fileName)
    {
        var output = Path.Combine(Path.GetTempPath(), "voiceops-api-output", Guid.NewGuid().ToString("N"));

        Assert.Throws<ArgumentException>(() => AudioFileAccess.Resolve(output, fileName));
    }

    [Theory]
    [InlineData("result.mp3", "audio/mpeg")]
    [InlineData("result.wav", "audio/wav")]
    [InlineData("result.flac", "audio/flac")]
    [InlineData("result.opus", "audio/ogg")]
    [InlineData("result.pcm", "application/octet-stream")]
    [InlineData("result.unknown", "application/octet-stream")]
    public void Content_type_matches_extension(string file, string expected)
    {
        Assert.Equal(expected, AudioFileAccess.GetContentType(file));
    }
}
