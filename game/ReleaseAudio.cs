using System;
using Godot;

namespace Gridworks.Game;

internal enum ReleaseAudioCue
{
    Breaker,
    Order,
    Complete,
    Energize,
    Outage,
    Warning,
    Result,
}

/// <summary>
/// Generates the release sound set from deterministic PCM. PlayLive is intentionally
/// named to keep save replay and render paths from producing historical sound effects.
/// </summary>
internal sealed partial class ReleaseAudio : Node
{
    private const int SampleRate = 22_050;
    private const double Tau = Math.PI * 2d;

    private AudioStreamPlayer _ambientPlayer = null!;
    private AudioStreamPlayer _weatherPlayer = null!;
    private AudioStreamPlayer _sfxPlayer = null!;
    private AudioStreamPlayer _motifPlayer = null!;
    private AudioStreamWav _ambient = null!;
    private AudioStreamWav _weather = null!;
    private AudioStreamWav _breaker = null!;
    private AudioStreamWav _complete = null!;
    private AudioStreamWav _energize = null!;
    private AudioStreamWav _outage = null!;
    private AudioStreamWav _warning = null!;
    private AudioStreamWav _result = null!;
    private AudioStreamWav _firstLightMotif = null!;
    private AudioStreamWav _lastBypassMotif = null!;
    private bool _ready;

    public override void _Ready()
    {
        _ambient = BuildAmbient();
        _weather = BuildWeatherAmbient();
        _breaker = BuildCue(0.24d, 0x19c4_70a1u, BreakerSample);
        _complete = BuildCue(0.42d, 0x23f1_7629u, CompleteSample);
        _energize = BuildCue(0.66d, 0x4b57_c31du, EnergizeSample);
        _outage = BuildCue(0.52d, 0x7a13_6e09u, OutageSample);
        _warning = BuildCue(0.31d, 0x5d33_a817u, WarningSample);
        _result = BuildCue(0.58d, 0x4f25_19b2u, ResultSample);
        _firstLightMotif = BuildCue(1.08d, 0x187a_2451u, FirstLightMotifSample);
        _lastBypassMotif = BuildCue(1.28d, 0x71c9_3e2du, LastBypassMotifSample);

        _ambientPlayer = new AudioStreamPlayer
        {
            Bus = "Ambient",
            Stream = _ambient,
        };
        _weatherPlayer = new AudioStreamPlayer
        {
            Bus = "Ambient",
            Stream = _weather,
            VolumeDb = -12f,
        };
        _sfxPlayer = new AudioStreamPlayer { Bus = "SFX" };
        _motifPlayer = new AudioStreamPlayer { Bus = "SFX" };
        AddChild(_ambientPlayer);
        AddChild(_weatherPlayer);
        AddChild(_sfxPlayer);
        AddChild(_motifPlayer);
        _ambientPlayer.Play();
        _weatherPlayer.Play();
        _ready = true;
    }

    public override void _ExitTree()
    {
        _ready = false;
        _ambientPlayer.Stop();
        _weatherPlayer.Stop();
        _sfxPlayer.Stop();
        _motifPlayer.Stop();
        _ambientPlayer.Stream = null;
        _weatherPlayer.Stream = null;
        _sfxPlayer.Stream = null;
        _motifPlayer.Stream = null;
        _ambient.Dispose();
        _weather.Dispose();
        _breaker.Dispose();
        _complete.Dispose();
        _energize.Dispose();
        _outage.Dispose();
        _warning.Dispose();
        _result.Dispose();
        _firstLightMotif.Dispose();
        _lastBypassMotif.Dispose();
    }

    public void ApplyVolumes(int masterPercent, int ambientPercent, int sfxPercent)
    {
        SetBusVolume("Master", masterPercent);
        SetBusVolume("Ambient", ambientPercent);
        SetBusVolume("SFX", sfxPercent);
    }

    public void PlayLive(ReleaseAudioCue cue)
    {
        if (!_ready)
        {
            return;
        }
        _sfxPlayer.Stream = cue switch
        {
            ReleaseAudioCue.Breaker or ReleaseAudioCue.Order => _breaker,
            ReleaseAudioCue.Complete => _complete,
            ReleaseAudioCue.Energize => _energize,
            ReleaseAudioCue.Outage => _outage,
            ReleaseAudioCue.Warning => _warning,
            ReleaseAudioCue.Result => _result,
            _ => throw new ArgumentOutOfRangeException(nameof(cue)),
        };
        _sfxPlayer.Play();
    }

    public void SetAtmosphere(int chapterIndex)
    {
        if (!_ready)
        {
            return;
        }
        int index = Math.Clamp(chapterIndex, 0, 7);
        _weatherPlayer.PitchScale = index switch
        {
            4 => 1.18f,
            5 => 0.82f,
            7 => 0.72f,
            _ => 1f,
        };
        _weatherPlayer.VolumeDb = index switch
        {
            4 => -15f,
            5 => -7f,
            7 => -10f,
            _ => -18f,
        };
    }

    public void PlayMotifLive(bool finalChapter)
    {
        if (!_ready)
        {
            return;
        }
        _motifPlayer.Stream = finalChapter ? _lastBypassMotif : _firstLightMotif;
        _motifPlayer.Play();
    }

    private static AudioStreamWav BuildAmbient()
    {
        const int seconds = 12;
        int sampleCount = checked(SampleRate * seconds);
        var components = new (int Cycles, double Amplitude, double Phase)[]
        {
            (720, 0.012d, 0d),
            (1440, 0.0025d, 0.4d),
            (731, 0.0035d, 1.8d),
            (2107, 0.0017d, 2.4d),
            (11, 0.004d, 0.9d),
        };
        return BuildStream(sampleCount, index =>
        {
            double loopPosition = index / (double)sampleCount;
            double slowEnvelope = 0.72d +
                (0.28d * Math.Sin((Tau * 3d * loopPosition) + 0.7d));
            double sample = 0d;
            foreach ((int cycles, double amplitude, double phase) in components)
            {
                sample += amplitude * Math.Sin((Tau * cycles * loopPosition) + phase);
            }
            return sample * slowEnvelope;
        }, loop: true);
    }

    private static AudioStreamWav BuildWeatherAmbient()
    {
        const int seconds = 9;
        int sampleCount = checked(SampleRate * seconds);
        uint state = 0x43d1_8a27u;
        return BuildStream(sampleCount, index =>
        {
            state = NextNoise(state);
            double noise = ((state >> 8) / 8_388_607.5d) - 1d;
            double position = index / (double)sampleCount;
            double gust = 0.35d + (0.65d * Math.Pow(
                0.5d + (0.5d * Math.Sin(Tau * 5d * position)),
                2d));
            double river = Math.Sin(Tau * 17d * position) * 0.003d;
            return (noise * 0.009d * gust) + river;
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
        double contact = seconds < 0.032d
            ? noise * (1d - (seconds / 0.032d)) * 0.1d
            : 0d;
        double body = Math.Sin(Tau * 76d * seconds) * Math.Exp(-22d * seconds) * 0.2d;
        double latchTime = seconds - 0.085d;
        double latch = latchTime is >= 0d and < 0.025d
            ? noise * (1d - (latchTime / 0.025d)) * 0.055d
            : 0d;
        return (contact + body + latch) * FadeOut(progress);
    }

    private static double EnergizeSample(double seconds, double progress, double noise)
    {
        double contact = seconds < 0.028d
            ? noise * (1d - (seconds / 0.028d)) * 0.08d
            : 0d;
        double envelope = Math.Sin(Math.PI * progress);
        double frequency = 145d + (185d * progress);
        double rise = Math.Sin(Tau * frequency * seconds) * envelope * 0.12d;
        double harmonic = Math.Sin(Tau * frequency * 2.02d * seconds) * envelope * 0.035d;
        return (contact + rise + harmonic) * FadeOut(progress);
    }

    private static double CompleteSample(double seconds, double progress, double noise)
    {
        double strike = seconds < 0.02d ? noise * 0.06d : 0d;
        double envelope = Math.Sin(Math.PI * progress);
        double tone = Math.Sin(Tau * (122d + (58d * progress)) * seconds) * 0.11d;
        return (strike + (tone * envelope)) * FadeOut(progress);
    }

    private static double OutageSample(double seconds, double progress, double noise)
    {
        double contact = seconds < 0.045d
            ? noise * (1d - (seconds / 0.045d)) * 0.13d
            : 0d;
        double fall = Math.Sin(Tau * (210d - (145d * progress)) * seconds) *
            Math.Exp(-4.8d * seconds) * 0.18d;
        double secondDropTime = seconds - 0.18d;
        double secondDrop = secondDropTime is >= 0d and < 0.05d
            ? noise * (1d - (secondDropTime / 0.05d)) * 0.045d
            : 0d;
        return (contact + fall + secondDrop) * FadeOut(progress);
    }

    private static double WarningSample(double seconds, double progress, double noise)
    {
        double pulse = Math.Sin(Tau * 285d * seconds) * (progress < 0.42d ? 0.12d : 0.06d);
        double gate = progress is < 0.28d or > 0.52d ? 1d : 0.12d;
        return (pulse * gate + (noise * 0.006d)) * FadeOut(progress);
    }

    private static double ResultSample(double seconds, double progress, double noise)
    {
        double envelope = Math.Sin(Math.PI * progress);
        double first = Math.Sin(Tau * 196d * seconds) * 0.08d;
        double second = Math.Sin(Tau * 247d * seconds) * 0.06d;
        return (first + second + (noise * 0.002d)) * envelope;
    }

    private static double FirstLightMotifSample(double seconds, double progress, double noise)
    {
        double frequency = progress < 0.42d ? 165d : progress < 0.72d ? 220d : 262d;
        double envelope = Math.Sin(Math.PI * progress);
        return Math.Sin(Tau * frequency * seconds) * envelope * 0.075d;
    }

    private static double LastBypassMotifSample(double seconds, double progress, double noise)
    {
        double frequency = progress < 0.3d ? 165d : progress < 0.56d ? 220d :
            progress < 0.78d ? 294d : 330d;
        double envelope = Math.Sin(Math.PI * progress);
        double fundamental = Math.Sin(Tau * frequency * seconds) * 0.072d;
        double undertone = Math.Sin(Tau * (frequency / 2d) * seconds) * 0.024d;
        return (fundamental + undertone) * envelope;
    }

    private static double FadeOut(double progress) =>
        progress < 0.88d ? 1d : Math.Max(0d, (1d - progress) / 0.12d);

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
