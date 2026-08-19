using System;
using Godot;

namespace Gridworks.Game;

internal enum CommercialAudioCue
{
    ConstructionOrdered,
    ConstructionCompleted,
    Energized,
    ProtectiveStop,
    Warning,
    Result,
    FirstLightMotif,
    FinalRerouteMotif,
}

internal enum CommercialWeatherProfile
{
    Clear,
    Heat,
    Rain,
    Storm,
}

/// <summary>
/// Keeps the commercial scene's live-only cue boundary explicit and owns the generated
/// ambience layers. Save restore and journal replay never emit historical cues.
/// </summary>
internal sealed partial class CommercialAudio : Node
{
    private AudioStreamPlayer _cityPlayer = null!;
    private AudioStreamPlayer _weatherPlayer = null!;
    private AudioStreamPlayer[] _sfxPlayers = null!;
    private CommercialAudioAssetSet _assets = null!;
    private CommercialWeatherProfile _weather = CommercialWeatherProfile.Clear;
    private int _nextSfxPlayer;
    private bool _ready;

#if DEBUG
    public int SfxVoiceCount => _sfxPlayers?.Length ?? 0;
#endif

    public override void _Ready()
    {
        _assets = CommercialAudioLibrary.Build();
        _cityPlayer = new AudioStreamPlayer
        {
            Bus = "Ambient",
            Stream = _assets.CityAmbient,
            VolumeDb = -2.5f,
        };
        _weatherPlayer = new AudioStreamPlayer
        {
            Bus = "Ambient",
            Stream = _assets.Weather[_weather],
            VolumeDb = -1.5f,
        };
        _sfxPlayers =
        [
            new AudioStreamPlayer { Bus = "SFX" },
            new AudioStreamPlayer { Bus = "SFX" },
            new AudioStreamPlayer { Bus = "SFX" },
        ];
        AddChild(_cityPlayer);
        AddChild(_weatherPlayer);
        foreach (AudioStreamPlayer player in _sfxPlayers)
        {
            AddChild(player);
        }
        _cityPlayer.Play();
        _weatherPlayer.Play();
        _ready = true;
    }

    public override void _ExitTree()
    {
        _ready = false;
        _cityPlayer.Stop();
        _weatherPlayer.Stop();
        _cityPlayer.Stream = null;
        _weatherPlayer.Stream = null;
        foreach (AudioStreamPlayer player in _sfxPlayers)
        {
            player.Stop();
            player.Stream = null;
        }
        _assets.Dispose();
    }

    public void ApplyVolumes(int masterPercent, int ambientPercent, int sfxPercent)
    {
        SetBusVolume("Master", masterPercent);
        SetBusVolume("Ambient", ambientPercent);
        SetBusVolume("SFX", sfxPercent);
    }

    public void SetWeather(CommercialWeatherProfile profile)
    {
        if (!_ready || profile == _weather)
        {
            return;
        }
        _weather = profile;
        _weatherPlayer.Stop();
        _weatherPlayer.Stream = _assets.Weather[profile];
        _weatherPlayer.Play();
    }

    public void PlayLive(CommercialAudioCue cue)
    {
        if (!_ready)
        {
            return;
        }
        int selected = _nextSfxPlayer;
        for (int offset = 0; offset < _sfxPlayers.Length; offset++)
        {
            int candidate = (_nextSfxPlayer + offset) % _sfxPlayers.Length;
            if (!_sfxPlayers[candidate].Playing)
            {
                selected = candidate;
                break;
            }
        }
        AudioStreamPlayer player = _sfxPlayers[selected];
        player.Stream = _assets.Cues[cue];
        player.Play();
        _nextSfxPlayer = (selected + 1) % _sfxPlayers.Length;
    }

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
