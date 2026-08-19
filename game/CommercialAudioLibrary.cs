using System;
using System.Collections.Generic;
using Godot;

namespace Gridworks.Game;

/// <summary>
/// Builds the commercial runtime sound set from deterministic PCM16 waveforms.
/// No recorded or third-party audio is read at runtime.
/// </summary>
internal static class CommercialAudioLibrary
{
    private const int SampleRate = 22_050;
    private const double Tau = Math.PI * 2d;

    public static CommercialAudioAssetSet Build() => new(
        BuildCityAmbient(),
        new Dictionary<CommercialWeatherProfile, AudioStreamWav>
        {
            [CommercialWeatherProfile.Clear] = BuildClearWeather(),
            [CommercialWeatherProfile.Heat] = BuildHeatWeather(),
            [CommercialWeatherProfile.Rain] = BuildRainWeather(),
            [CommercialWeatherProfile.Storm] = BuildStormWeather(),
        },
        new Dictionary<CommercialAudioCue, AudioStreamWav>
        {
            [CommercialAudioCue.ConstructionOrdered] = BuildCue(
                0.24d,
                0x19c4_70a1u,
                ConstructionOrderedSample),
            [CommercialAudioCue.ConstructionCompleted] = BuildCue(
                0.42d,
                0x32b5_81e2u,
                ConstructionCompletedSample),
            [CommercialAudioCue.Energized] = BuildCue(
                0.66d,
                0x4b57_c31du,
                EnergizedSample),
            [CommercialAudioCue.ProtectiveStop] = BuildCue(
                0.52d,
                0x7a13_6e09u,
                ProtectiveStopSample),
            [CommercialAudioCue.Warning] = BuildCue(
                0.72d,
                0x83d9_2f17u,
                WarningSample),
            [CommercialAudioCue.Result] = BuildCue(
                0.86d,
                0x9e24_6ac3u,
                ResultSample),
            [CommercialAudioCue.FirstLightMotif] = BuildCue(
                1.6d,
                0xa130_47d2u,
                FirstLightMotifSample),
            [CommercialAudioCue.FinalRerouteMotif] = BuildCue(
                2.1d,
                0xb842_15f0u,
                FinalRerouteMotifSample),
        });

    private static AudioStreamWav BuildCityAmbient()
    {
        const int seconds = 12;
        int sampleCount = checked(SampleRate * seconds);
        return BuildStream(sampleCount, index =>
        {
            double loop = index / (double)sampleCount;
            double envelope = 0.76d + (0.24d * Math.Sin((Tau * 3d * loop) + 0.7d));
            double gridHum = 0.010d * Math.Sin(Tau * 720d * loop);
            double transformer = 0.0024d * Math.Sin((Tau * 1440d * loop) + 0.4d);
            double distantPlant = 0.0032d * Math.Sin((Tau * 731d * loop) + 1.8d);
            double cityAir = 0.0018d * Math.Sin((Tau * 2107d * loop) + 2.4d);
            return (gridHum + transformer + distantPlant + cityAir) * envelope;
        }, loop: true);
    }

    private static AudioStreamWav BuildClearWeather() => BuildWeatherLoop(
        (5, 0.0017d, 0.3d),
        (13, 0.0011d, 2.1d),
        (29, 0.0007d, 1.2d));

    private static AudioStreamWav BuildHeatWeather() => BuildWeatherLoop(
        (47, 0.0032d, 0.8d),
        (94, 0.0014d, 1.7d),
        (151, 0.0008d, 2.9d));

    private static AudioStreamWav BuildRainWeather() => BuildWeatherLoop(
        (233, 0.0035d, 0.1d),
        (347, 0.0025d, 1.5d),
        (521, 0.0017d, 2.7d),
        (809, 0.0011d, 0.9d));

    private static AudioStreamWav BuildStormWeather() => BuildWeatherLoop(
        (7, 0.0060d, 2.2d),
        (19, 0.0036d, 0.6d),
        (263, 0.0038d, 1.1d),
        (401, 0.0027d, 2.6d),
        (677, 0.0013d, 0.2d));

    private static AudioStreamWav BuildWeatherLoop(
        params (int Cycles, double Amplitude, double Phase)[] components)
    {
        const int seconds = 8;
        int sampleCount = checked(SampleRate * seconds);
        return BuildStream(sampleCount, index =>
        {
            double loop = index / (double)sampleCount;
            double breathe = 0.72d +
                (0.28d * Math.Sin((Tau * 2d * loop) + 0.45d));
            double sample = 0d;
            foreach ((int cycles, double amplitude, double phase) in components)
            {
                sample += amplitude * Math.Sin((Tau * cycles * loop) + phase);
            }
            return sample * breathe;
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

    private static double ConstructionOrderedSample(
        double seconds,
        double progress,
        double noise)
    {
        double contact = seconds < 0.032d
            ? noise * (1d - (seconds / 0.032d)) * 0.10d
            : 0d;
        double body = Math.Sin(Tau * 76d * seconds) * Math.Exp(-22d * seconds) * 0.20d;
        double latchSeconds = seconds - 0.085d;
        double latch = latchSeconds is >= 0d and < 0.025d
            ? noise * (1d - (latchSeconds / 0.025d)) * 0.055d
            : 0d;
        return (contact + body + latch) * FadeOut(progress);
    }

    private static double ConstructionCompletedSample(
        double seconds,
        double progress,
        double noise)
    {
        double first = Bell(seconds, 0d, 196d, 0.12d, 20d);
        double second = Bell(seconds, 0.13d, 247d, 0.10d, 18d);
        double third = Bell(seconds, 0.26d, 294d, 0.08d, 16d);
        double relay = seconds < 0.02d ? noise * 0.025d : 0d;
        return (first + second + third + relay) * FadeOut(progress);
    }

    private static double EnergizedSample(double seconds, double progress, double noise)
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

    private static double ProtectiveStopSample(
        double seconds,
        double progress,
        double noise)
    {
        double contact = seconds < 0.045d
            ? noise * (1d - (seconds / 0.045d)) * 0.13d
            : 0d;
        double fall = Math.Sin(Tau * (210d - (145d * progress)) * seconds) *
            Math.Exp(-4.8d * seconds) * 0.18d;
        double secondDropSeconds = seconds - 0.18d;
        double secondDrop = secondDropSeconds is >= 0d and < 0.05d
            ? noise * (1d - (secondDropSeconds / 0.05d)) * 0.045d
            : 0d;
        return (contact + fall + secondDrop) * FadeOut(progress);
    }

    private static double WarningSample(double seconds, double progress, double noise)
    {
        double first = Bell(seconds, 0d, 392d, 0.10d, 10d);
        double second = Bell(seconds, 0.34d, 392d, 0.10d, 10d);
        return (first + second + (noise * 0.002d)) * FadeOut(progress);
    }

    private static double ResultSample(double seconds, double progress, double noise)
    {
        double envelope = Math.Sin(Math.PI * Math.Min(1d, progress * 1.35d)) *
            Math.Exp(-1.5d * progress);
        double chord = Math.Sin(Tau * 196d * seconds) * 0.07d +
            Math.Sin(Tau * 247d * seconds) * 0.05d +
            Math.Sin(Tau * 294d * seconds) * 0.04d;
        return (chord * envelope) + (noise * 0.001d * FadeOut(progress));
    }

    private static double FirstLightMotifSample(
        double seconds,
        double progress,
        double noise)
    {
        double first = Bell(seconds, 0d, 196d, 0.10d, 4.6d);
        double second = Bell(seconds, 0.38d, 247d, 0.09d, 4.2d);
        double third = Bell(seconds, 0.78d, 294d, 0.085d, 3.8d);
        double fourth = Bell(seconds, 1.15d, 392d, 0.075d, 3.5d);
        return (first + second + third + fourth + (noise * 0.0007d)) * FadeOut(progress);
    }

    private static double FinalRerouteMotifSample(
        double seconds,
        double progress,
        double noise)
    {
        double first = Bell(seconds, 0d, 147d, 0.09d, 3.8d);
        double second = Bell(seconds, 0.32d, 196d, 0.085d, 3.6d);
        double third = Bell(seconds, 0.72d, 220d, 0.08d, 3.4d);
        double fourth = Bell(seconds, 1.12d, 294d, 0.075d, 3.1d);
        double fifth = Bell(seconds, 1.52d, 392d, 0.065d, 2.8d);
        return (first + second + third + fourth + fifth + (noise * 0.0006d)) *
            FadeOut(progress);
    }

    private static double Bell(
        double seconds,
        double start,
        double frequency,
        double amplitude,
        double decay)
    {
        double local = seconds - start;
        return local < 0d
            ? 0d
            : Math.Sin(Tau * frequency * local) * Math.Exp(-decay * local) * amplitude;
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

    private static uint NextNoise(uint state) =>
        unchecked((state * 1_664_525u) + 1_013_904_223u);
}

internal sealed class CommercialAudioAssetSet : IDisposable
{
    public CommercialAudioAssetSet(
        AudioStreamWav cityAmbient,
        IReadOnlyDictionary<CommercialWeatherProfile, AudioStreamWav> weather,
        IReadOnlyDictionary<CommercialAudioCue, AudioStreamWav> cues)
    {
        CityAmbient = cityAmbient;
        Weather = weather;
        Cues = cues;
    }

    public AudioStreamWav CityAmbient { get; }

    public IReadOnlyDictionary<CommercialWeatherProfile, AudioStreamWav> Weather { get; }

    public IReadOnlyDictionary<CommercialAudioCue, AudioStreamWav> Cues { get; }

    public void Dispose()
    {
        CityAmbient.Dispose();
        foreach (AudioStreamWav stream in Weather.Values)
        {
            stream.Dispose();
        }
        foreach (AudioStreamWav stream in Cues.Values)
        {
            stream.Dispose();
        }
    }
}
