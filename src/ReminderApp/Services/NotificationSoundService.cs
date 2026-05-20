using System.IO;
using System.Media;

namespace ReminderApp.Services;

/// <summary>
/// Генерирует при необходимости <c>data/sounds/notification.wav</c> и воспроизводит без системных звуков Windows.
/// </summary>
public sealed class NotificationSoundService
{
    private const int SampleRate = 22050;
    private const short Channels = 1;
    private const short BitsPerSample = 16;
    private readonly string _wavPath = PortablePaths.NotificationWavPath;

    public NotificationSoundService()
    {
    }

    public void EnsureNotificationWavExists()
    {
        if (File.Exists(_wavPath))
        {
            return;
        }

        try
        {
            WriteGeneratedWav(_wavPath);
        }
        catch
        {
            // Файл не обязателен для работы приложения
        }
    }

    public void PlayNotificationSound()
    {
        EnsureNotificationWavExists();
        if (!File.Exists(_wavPath))
        {
            return;
        }

        try
        {
            using var player = new SoundPlayer(_wavPath);
            player.Play();
        }
        catch
        {
            // Не падаем из-за звука
        }
    }

    private static void WriteGeneratedWav(string path)
    {
        // Два коротких тона ~0.2 с каждый, общая длительность ~0.45 с, мягкая огибающая
        const double dur1 = 0.2;
        const double dur2 = 0.2;
        const double gap = 0.05;
        var samples1 = (int)(SampleRate * dur1);
        var samplesGap = (int)(SampleRate * gap);
        var samples2 = (int)(SampleRate * dur2);
        var totalSamples = samples1 + samplesGap + samples2;
        var samples = new short[totalSamples];

        FillTone(samples, 0, samples1, 880.0, samples1);
        FillTone(samples, samples1 + samplesGap, samples2, 1175.0, samples2);

        var dataBytes = new byte[totalSamples * 2];
        Buffer.BlockCopy(samples, 0, dataBytes, 0, dataBytes.Length);

        using (var stream = File.Create(path))
        {
            WriteWavPcm16Mono(stream, dataBytes, SampleRate);
        }
    }

    private static void FillTone(short[] buffer, int offset, int length, double frequencyHz, int fadeSamples)
    {
        var attackLen = Math.Max(1, fadeSamples / 6);
        var releaseLen = Math.Max(1, fadeSamples / 5);
        for (var i = 0; i < length; i++)
        {
            var t = i / (double)SampleRate;
            var sample = Math.Sin(2 * Math.PI * frequencyHz * t);
            var fadeIn = Math.Min(1.0, i / (double)attackLen);
            var fadeOut = Math.Min(1.0, (length - 1 - i) / (double)releaseLen);
            var env = Math.Min(fadeIn, fadeOut);
            env = 0.5 * (1 - Math.Cos(Math.PI * Math.Clamp(env, 0, 1)));
            var v = sample * env * 0.22;
            buffer[offset + i] = (short)Math.Clamp(v * short.MaxValue, short.MinValue, short.MaxValue);
        }
    }

    private static void WriteWavPcm16Mono(Stream stream, byte[] pcmData, int sampleRate)
    {
        var dataLen = pcmData.Length;
        var fileLen = 36 + dataLen;

        using var bw = new BinaryWriter(stream);
        bw.Write("RIFF"u8.ToArray());
        bw.Write(fileLen);
        bw.Write("WAVE"u8.ToArray());
        bw.Write("fmt "u8.ToArray());
        bw.Write(16);
        bw.Write((short)1); // PCM
        bw.Write(Channels);
        bw.Write(sampleRate);
        bw.Write(sampleRate * Channels * BitsPerSample / 8);
        bw.Write((short)(Channels * BitsPerSample / 8));
        bw.Write(BitsPerSample);
        bw.Write("data"u8.ToArray());
        bw.Write(dataLen);
        bw.Write(pcmData);
    }
}
