using System;
using Godot;

namespace Gridworks.Game;

internal enum ProductAudioCue
{
    Breaker,
    Energize,
    Outage,
}

internal sealed partial class ProductAudio : Node
{
    private const int SampleRate = 22_050;
    private const double Tau = Math.PI * 2d;

    private AudioStreamPlayer _ambientPlayer = null!;
    private AudioStreamPlayer _sfxPlayer = null!;
    private AudioStreamWav _ambient = null!;
    private AudioStreamWav _breaker = null!;
    private AudioStreamWav _energize = null!;
    private AudioStreamWav _outage = null!;

    public override void _Ready()
    {
        _ambient = BuildAmbient();
        _ambientPlayer = new AudioStreamPlayer
        {
            Bus = "Ambient",
            Stream = _ambient,
        };
        _sfxPlayer = new AudioStreamPlayer { Bus = "SFX" };
        _breaker = BuildCue(0.22d, 0x31a7_1c5du, BreakerSample);
        _energize = BuildCue(0.62d, 0x59d2_40bbu, EnergizeSample);
        _outage = BuildCue(0.46d, 0x78e4_9931u, OutageSample);
        AddChild(_ambientPlayer);
        AddChild(_sfxPlayer);
        _ambientPlayer.Play();
    }

    public override void _ExitTree()
    {
        _ambientPlayer.Stop();
        _sfxPlayer.Stop();
        _ambientPlayer.Stream = null;
        _sfxPlayer.Stream = null;
        _ambient.Dispose();
        _breaker.Dispose();
        _energize.Dispose();
        _outage.Dispose();
    }

    public void ApplyVolumes(int masterPercent, int ambientPercent, int sfxPercent)
    {
        SetBusVolume("Master", masterPercent);
        SetBusVolume("Ambient", ambientPercent);
        SetBusVolume("SFX", sfxPercent);
    }

    public void Play(ProductAudioCue cue)
    {
        _sfxPlayer.Stream = cue switch
        {
            ProductAudioCue.Breaker => _breaker,
            ProductAudioCue.Energize => _energize,
            ProductAudioCue.Outage => _outage,
            _ => throw new ArgumentOutOfRangeException(nameof(cue)),
        };
        _sfxPlayer.Play();
    }

    private static AudioStreamWav BuildAmbient()
    {
        const int seconds = 8;
        int sampleCount = checked(SampleRate * seconds);
        var components = new (int Cycles, double Amplitude, double Phase)[5];
        uint state = 0x5f37_59dfu;
        for (int index = 0; index < components.Length; index++)
        {
            state = NextNoise(state);
            int cycles = 3 + (int)(state % 17u);
            state = NextNoise(state);
            double amplitude = 0.004d + ((state & 0xffu) / 255d * 0.008d);
            state = NextNoise(state);
            double phase = (state / (double)uint.MaxValue) * Tau;
            components[index] = (cycles, amplitude, phase);
        }

        return BuildStream(sampleCount, index =>
        {
            double loopPosition = index / (double)sampleCount;
            double sample = 0.025d * Math.Sin(Tau * 60d * (index / (double)SampleRate));
            foreach ((int cycles, double amplitude, double phase) in components)
            {
                sample += amplitude * Math.Sin((Tau * cycles * loopPosition) + phase);
            }
            return sample;
        }, loop: true);
    }

    private static AudioStreamWav BuildCue(
        double durationSeconds,
        uint seed,
        Func<double, double, double, double> sampleFunction)
    {
        int sampleCount = checked((int)Math.Round(durationSeconds * SampleRate));
        uint state = seed;
        return BuildStream(sampleCount, index =>
        {
            state = NextNoise(state);
            double noise = ((state >> 8) / 8_388_607.5d) - 1d;
            double seconds = index / (double)SampleRate;
            double progress = index / (double)Math.Max(1, sampleCount - 1);
            return sampleFunction(seconds, progress, noise);
        }, loop: false);
    }

    private static double BreakerSample(double seconds, double progress, double noise)
    {
        double thump = Math.Sin(Tau * 82d * seconds) * Math.Exp(-20d * seconds) * 0.18d;
        double contact = seconds < 0.035d
            ? noise * (1d - (seconds / 0.035d)) * 0.12d
            : 0d;
        return (thump + contact) * FadeOut(progress);
    }

    private static double EnergizeSample(double seconds, double progress, double noise)
    {
        double frequency = 170d + (220d * progress);
        double rise = Math.Sin(Tau * frequency * seconds) * Math.Sin(Math.PI * progress) * 0.13d;
        double harmonic = Math.Sin(Tau * (frequency * 2.01d) * seconds) *
            Math.Sin(Math.PI * progress) * 0.045d;
        double contact = seconds < 0.025d
            ? noise * (1d - (seconds / 0.025d)) * 0.09d
            : 0d;
        return (rise + harmonic + contact) * FadeOut(progress);
    }

    private static double OutageSample(double seconds, double progress, double noise)
    {
        double frequency = 190d - (130d * progress);
        double fall = Math.Sin(Tau * frequency * seconds) * Math.Exp(-4.5d * seconds) * 0.17d;
        double contact = seconds < 0.055d
            ? noise * (1d - (seconds / 0.055d)) * 0.14d
            : 0d;
        return (fall + contact) * FadeOut(progress);
    }

    private static double FadeOut(double progress) =>
        progress < 0.9d ? 1d : Math.Max(0d, (1d - progress) / 0.1d);

    private static AudioStreamWav BuildStream(
        int sampleCount,
        Func<int, double> sampleFunction,
        bool loop)
    {
        byte[] data = new byte[checked(sampleCount * sizeof(short))];
        for (int index = 0; index < sampleCount; index++)
        {
            double sample = Math.Clamp(sampleFunction(index), -1d, 1d);
            short pcm = checked((short)Math.Round(sample * short.MaxValue));
            data[index * 2] = (byte)(pcm & 0xff);
            data[(index * 2) + 1] = (byte)((pcm >> 8) & 0xff);
        }
        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = SampleRate,
            Stereo = false,
            Data = data,
            LoopMode = loop
                ? AudioStreamWav.LoopModeEnum.Forward
                : AudioStreamWav.LoopModeEnum.Disabled,
            LoopBegin = 0,
            LoopEnd = loop ? sampleCount : 0,
        };
    }

    private static uint NextNoise(uint state) => unchecked((state * 1_664_525u) + 1_013_904_223u);

    private static void SetBusVolume(string busName, int percent)
    {
        if (percent is not (0 or 25 or 50 or 75 or 100))
        {
            throw new ArgumentOutOfRangeException(nameof(percent));
        }
        int bus = AudioServer.GetBusIndex(busName);
        if (bus < 0)
        {
            throw new InvalidOperationException($"Audio bus '{busName}' is missing.");
        }
        AudioServer.SetBusMute(bus, percent == 0);
        AudioServer.SetBusVolumeLinear(bus, percent / 100f);
    }
}
