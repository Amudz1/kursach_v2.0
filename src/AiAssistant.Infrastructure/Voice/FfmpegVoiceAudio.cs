using System.Buffers;
using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using AiAssistant.Application.Common;
using AiAssistant.Application.Voice;

namespace AiAssistant.Infrastructure.Voice;

public sealed class FfmpegVoiceAudio(VoiceLimits limits) : IVoiceAudio
{
    private const int Rate = 16000;
    private const int BytesPerSecond = Rate * 2;
    private const string Formats = "matroska,webm,ogg,mov,mp4";

    public async Task<Result<PreparedVoice>> PrepareAsync(Stream input, CancellationToken cancellationToken)
    {
        // A private, seekable file also supports MP4 recordings with metadata at the end.
        var directory = Directory.CreateTempSubdirectory("budz-voice-");
        var path = Path.Combine(directory.FullName, "audio");
        try
        {
            await using (var destination = File.Create(path))
                await CopyBounded(input, destination, limits.MaxBytes, cancellationToken);
            var header = new byte[4096];
            await using (var source = File.OpenRead(path))
                await source.ReadAtLeastAsync(header, header.Length, false, cancellationToken);
            var probe = await Run("ffprobe", ["-v", "error", "-protocol_whitelist", "file", "-format_whitelist", Formats,
                "-show_entries", "format=format_name,duration:stream=codec_name,codec_type,channels,duration", "-of", "json", path],
                16 * 1024, cancellationToken);
            if (!ReadFormat(probe, header, out var duration)) return FormatError();
            if (duration > limits.MaxSeconds + 0.05) return DurationError();

            var stopAt = (limits.MaxSeconds + 0.1).ToString("0.0", CultureInfo.InvariantCulture);
            var pcm = await Run("ffmpeg", ["-nostdin", "-v", "error", "-xerror", "-threads", "1",
                "-protocol_whitelist", "file", "-format_whitelist", Formats, "-i", path, "-map", "0:a:0",
                "-t", stopAt, "-ac", "1", "-ar", "16000", "-c:a", "pcm_s16le", "-f", "s16le", "pipe:1"],
                BytesPerSecond * (limits.MaxSeconds + 1), cancellationToken);
            if (pcm is null) return FormatError();
            if (pcm.Length > BytesPerSecond * limits.MaxSeconds + 1600) return DurationError();
            if (!HasSignal(pcm))
                return Result<PreparedVoice>.Fail(ErrorKind.Validation, "voice_silence", "В записи не слышно речи.");
            return Result<PreparedVoice>.Ok(new(ToWave(pcm)));
        }
        catch (InvalidDataException) { return FormatError(); }
        catch (JsonException) { return FormatError(); }
        catch (Win32Exception)
        {
            return Result<PreparedVoice>.Fail(ErrorKind.Unavailable, "voice_unavailable", "Не удалось запустить обработку аудио.");
        }
        finally
        {
            File.Delete(path);
            directory.Delete();
        }
    }

    private static bool ReadFormat(byte[]? probe, byte[] header, out double duration)
    {
        duration = 0;
        if (probe is null) return false;
        using var report = JsonDocument.Parse(probe);
        var root = report.RootElement;
        if (!root.TryGetProperty("streams", out var streams) || streams.GetArrayLength() != 1 ||
            !root.TryGetProperty("format", out var container)) return false;
        var stream = streams[0];
        if (Field(stream, "codec_type") != "audio" || !stream.TryGetProperty("channels", out var channels) ||
            !channels.TryGetInt32(out var count) || count is < 1 or > 2) return false;
        duration = Math.Max(Duration(container), Duration(stream));
        var format = Field(container, "format_name");
        var codec = Field(stream, "codec_name");
        return (format, codec) switch
        {
            ("matroska,webm", "opus") => IsWebM(header),
            ("ogg", "opus") => header.AsSpan().StartsWith("OggS"u8),
            ("mov,mp4,m4a,3gp,3g2,mj2", "aac") => header.AsSpan(4).StartsWith("ftyp"u8),
            _ => false,
        };
    }

    private static string? Field(JsonElement item, string name) => item.TryGetProperty(name, out var field) &&
        field.ValueKind == JsonValueKind.String ? field.GetString() : null;

    private static double Duration(JsonElement item) =>
        double.TryParse(Field(item, "duration"), CultureInfo.InvariantCulture, out var seconds) ? seconds : 0;

    private static bool IsWebM(ReadOnlySpan<byte> header)
    {
        // EBML DocType for WebM starts with element 0x4282 and its four-byte payload.
        return header.StartsWith(new byte[] { 0x1a, 0x45, 0xdf, 0xa3 }) &&
            header[..minHeaderBytes].IndexOf(new byte[] { 0x42, 0x82, 0x84, (byte)'w', (byte)'e', (byte)'b', (byte)'m' }) >= 0;
    }
    private const int minHeaderBytes = 1024;

    private static bool HasSignal(ReadOnlySpan<byte> pcm)
    {
        for (var i = 0; i + 1 < pcm.Length; i += 2)
            if (Math.Abs((int)BinaryPrimitives.ReadInt16LittleEndian(pcm[i..])) >= 32) return true;
        return false;
    }

    private static byte[] ToWave(byte[] pcm)
    {
        using var output = new MemoryStream(pcm.Length + 44);
        using var writer = new BinaryWriter(output);
        writer.Write("RIFF"u8); writer.Write(pcm.Length + 36); writer.Write("WAVEfmt "u8);
        writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(Rate);
        writer.Write(BytesPerSecond); writer.Write((short)2); writer.Write((short)16);
        writer.Write("data"u8); writer.Write(pcm.Length); writer.Write(pcm);
        return output.ToArray();
    }

    private static async Task<byte[]?> Run(string executable, string[] arguments, int maximumBytes, CancellationToken ct)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        try
        {
            var errorDrain = process.StandardError.BaseStream.CopyToAsync(Stream.Null, ct);
            using var output = new MemoryStream();
            await CopyBounded(process.StandardOutput.BaseStream, output, maximumBytes, ct);
            await process.WaitForExitAsync(ct);
            await errorDrain;
            return process.ExitCode == 0 ? output.ToArray() : null;
        }
        finally
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { /* Exited between check and kill. */ }
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }
    }

    private static async Task CopyBounded(Stream input, Stream output, int maximum, CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(32 * 1024);
        try
        {
            int size, total = 0;
            while ((size = await input.ReadAsync(buffer.AsMemory(), ct)) != 0)
            {
                total += size;
                if (total > maximum) throw new InvalidDataException();
                await output.WriteAsync(buffer.AsMemory(0, size), ct);
            }
        }
        finally { ArrayPool<byte>.Shared.Return(buffer, clearArray: true); }
    }

    private static Result<PreparedVoice> FormatError() => Result<PreparedVoice>.Fail(ErrorKind.Validation,
        "voice_format", "Поддерживаются записи WebM/Opus, Ogg/Opus и MP4/AAC.");
    private static Result<PreparedVoice> DurationError() => Result<PreparedVoice>.Fail(ErrorKind.Validation,
        "voice_duration", "Запись слишком длинная. Запишите более короткий фрагмент.");
}
