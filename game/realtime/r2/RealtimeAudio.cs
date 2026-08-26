using System;
using Godot;

namespace Gridworks.Game.Realtime.R2;

internal readonly record struct RealtimeAudioQualificationFacts(
    int AmbientStarts,
    int LiveCues,
    bool AmbientReady,
    bool SfxQuiet);

/// <summary>
/// Owns the current R2 generated PCM streams and engine playback only. Live cue
/// meaning belongs to RealtimeSession, while volume and mute belong to Main's
/// product-settings projection.
/// </summary>
internal sealed partial class RealtimeAudio : Node
{
    private const int SampleRate = 22_050;
    private const double Tau = Math.PI * 2d;

    private AudioStreamPlayer _ambientPlayer = null!;
    private AudioStreamPlayer _sfxPlayer = null!;
    private AudioStreamWav _ambient = null!;
    private AudioStreamWav _breaker = null!;
    private AudioStreamWav _energize = null!;
    private AudioStreamWav _outage = null!;
    private bool _ready;
    private bool _ambientStarted;
    private int _ambientStartCount;
    private int _liveCuePlayCount;

#if DEBUG
    private RealtimeLiveAudioCue? _lastLiveCue;

    internal int AmbientStartCountForSmoke => _ambientStartCount;

    internal int LiveCuePlayCountForSmoke => _liveCuePlayCount;

    internal RealtimeLiveAudioCue? LastLiveCueForSmoke => _lastLiveCue;

    internal AudioStreamPlayer AmbientPlayerForSmoke => _ambientPlayer;

    internal AudioStreamPlayer SfxPlayerForSmoke => _sfxPlayer;

    internal AudioStreamWav StreamForSmoke(RealtimeLiveAudioCue cue) => Stream(cue);
#endif

    public override void _Ready()
    {
        _ambient = BuildAmbient();
        _breaker = BuildCue(0.24d, 0x19c4_70a1u, BreakerSample);
        _energize = BuildCue(0.66d, 0x4b57_c31du, EnergizeSample);
        _outage = BuildCue(0.52d, 0x7a13_6e09u, OutageSample);

        _ambientPlayer = new AudioStreamPlayer
        {
            Bus = "Ambient",
            Stream = _ambient,
        };
        _sfxPlayer = new AudioStreamPlayer { Bus = "SFX" };
        AddChild(_ambientPlayer);
        AddChild(_sfxPlayer);
        _ready = true;
    }

    public override void _ExitTree()
    {
        if (!_ready)
        {
            return;
        }

        _ready = false;
        _ambientStarted = false;
        _ambientPlayer.Stop();
        _sfxPlayer.Stop();
        _ambientPlayer.Stream = null;
        _sfxPlayer.Stream = null;
        _ambient.Dispose();
        _breaker.Dispose();
        _energize.Dispose();
        _outage.Dispose();
    }

    internal void StartAmbient()
    {
        if (!_ready)
        {
            throw new InvalidOperationException(
                "Realtime audio must be ready before ambient playback starts.");
        }
        if (_ambientStarted)
        {
            return;
        }

        if (!IsHeadless())
        {
            _ambientPlayer.Play();
        }
        _ambientStarted = true;
        _ambientStartCount++;
    }

    internal void PlayLive(RealtimeLiveAudioCue cue)
    {
        if (!_ready)
        {
            return;
        }

        _sfxPlayer.Stream = Stream(cue);
        if (!IsHeadless())
        {
            _sfxPlayer.Play();
        }
        _liveCuePlayCount++;
#if DEBUG
        _lastLiveCue = cue;
#endif
    }

    internal RealtimeAudioQualificationFacts CaptureQualificationFacts()
    {
        bool ambientReady = _ready &&
            _ambientStarted &&
            _ambientPlayer.Stream is AudioStreamWav stream &&
            ReferenceEquals(stream, _ambient) &&
            string.Equals(_ambientPlayer.Bus, "Ambient", StringComparison.Ordinal) &&
            stream.Format == AudioStreamWav.FormatEnum.Format16Bits &&
            stream.MixRate == SampleRate &&
            !stream.Stereo &&
            stream.LoopMode == AudioStreamWav.LoopModeEnum.Forward &&
            stream.LoopBegin == 0 &&
            stream.LoopEnd > 0 &&
            stream.Data.Length > 0;
        bool sfxQuiet = _ready &&
            string.Equals(_sfxPlayer.Bus, "SFX", StringComparison.Ordinal) &&
            _sfxPlayer.Stream is null &&
            !_sfxPlayer.Playing;
        return new RealtimeAudioQualificationFacts(
            _ambientStartCount,
            _liveCuePlayCount,
            ambientReady,
            sfxQuiet);
    }

    private AudioStreamWav Stream(RealtimeLiveAudioCue cue) => cue switch
    {
        RealtimeLiveAudioCue.Breaker => _breaker,
        RealtimeLiveAudioCue.Energize => _energize,
        RealtimeLiveAudioCue.Outage => _outage,
        _ => throw new ArgumentOutOfRangeException(nameof(cue)),
    };

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
        double harmonic = Math.Sin(Tau * frequency * 2.02d * seconds) *
            envelope * 0.035d;
        return (contact + rise + harmonic) * FadeOut(progress);
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

    private static uint NextNoise(uint state) =>
        unchecked((state * 1_664_525u) + 1_013_904_223u);

    private static bool IsHeadless() => string.Equals(
        DisplayServer.GetName(),
        "headless",
        StringComparison.OrdinalIgnoreCase);
}
