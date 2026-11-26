using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace FootballBoard.Presentation
{
    /// <summary>
    /// Plays the game's music and sound effects. Everything is synthesized at start-up on a worker
    /// thread (<see cref="SoundSynth"/>); an AudioClip in Resources/Audio or Resources/Music named
    /// like the sound (e.g. "sfx_goal", "music_theme") replaces the synthesized version. The on/off
    /// setting is saved.
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        private const string PrefKey = "SoundOn";       // older builds: on/off only
        private const string VolumeKey = "MasterVolume";
        private const float DefaultVolume = 0.8f;
        private const float MusicVolumeTheme = 0.5f;
        private const float MusicVolumeMatch = 0.28f;

        public static GameAudio Instance { get; private set; }

        /// <summary>Raised with true/false when the sound goes from muted to audible or back.</summary>
        public event Action<bool> SoundChanged;
        /// <summary>Raised with the new master volume (0..1).</summary>
        public event Action<float> VolumeChanged;

        /// <summary>Master volume 0..1; 0 means muted.</summary>
        public float Volume { get; private set; } = DefaultVolume;
        public bool SoundOn => Volume > 0.001f;
        private float _volumeBeforeMute = DefaultVolume;

        private AudioSource _music, _sfx;
        private readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
        private readonly Dictionary<MusicTrack, AudioClip> _tracks = new Dictionary<MusicTrack, AudioClip>();
        private Task<(Dictionary<Sfx, float[]> sfx, Dictionary<MusicTrack, float[]> music)> _synth;
        private MusicTrack _wanted = MusicTrack.None, _playing = MusicTrack.None;
        private float _duckUntil;

        public static GameAudio Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("GameAudio");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<GameAudio>();
            return Instance;
        }

        private void Awake()
        {
            if (FindAnyObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
            _music = gameObject.AddComponent<AudioSource>();
            _music.loop = true;
            _music.playOnAwake = false;
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;

            var legacyOn = PlayerPrefs.GetInt(PrefKey, 1) == 1;
            Volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, legacyOn ? DefaultVolume : 0f));
            if (Volume > 0.001f) _volumeBeforeMute = Volume;
            AudioListener.volume = Volume;

            // Files in Resources/Audio win; only what is missing gets synthesized.
            var needSfx = new List<Sfx>();
            foreach (Sfx s in Enum.GetValues(typeof(Sfx)))
            {
                var clip = LoadOverride(SoundSynth.FileName(s));
                if (clip != null) _clips[s] = clip; else needSfx.Add(s);
            }
            var needMusic = new List<MusicTrack>();
            foreach (var m in new[] { MusicTrack.Theme, MusicTrack.Match })
            {
                var clip = LoadOverride(SoundSynth.FileName(m));
                if (clip != null) _tracks[m] = clip; else needMusic.Add(m);
            }
            _synth = Task.Run(() =>
            {
                var sfx = new Dictionary<Sfx, float[]>();
                foreach (var s in needSfx) sfx[s] = SoundSynth.Make(s);
                var music = new Dictionary<MusicTrack, float[]>();
                foreach (var m in needMusic) music[m] = SoundSynth.Compose(m);
                return (sfx, music);
            });
        }

        private void Update()
        {
            if (_synth != null && _synth.IsCompleted)
            {
                if (_synth.Status == TaskStatus.RanToCompletion)
                {
                    foreach (var pair in _synth.Result.sfx) _clips[pair.Key] = ToClip(SoundSynth.FileName(pair.Key), pair.Value);
                    foreach (var pair in _synth.Result.music) _tracks[pair.Key] = ToClip(SoundSynth.FileName(pair.Key), pair.Value);
                }
                else Debug.LogWarning("[Audio] synthesis failed: " + _synth.Exception?.GetBaseException().Message);
                _synth = null;
            }

            if (_wanted != _playing && _tracks.TryGetValue(_wanted, out var track))
            {
                _music.clip = track;
                _music.Play();
                _playing = _wanted;
            }
            else if (_wanted == MusicTrack.None && _playing != MusicTrack.None)
            {
                _music.Stop();
                _playing = MusicTrack.None;
            }

            var target = _playing == MusicTrack.Theme ? MusicVolumeTheme : MusicVolumeMatch;
            if (Time.unscaledTime < _duckUntil) target *= 0.3f;
            _music.volume = Mathf.MoveTowards(_music.volume, target, Time.unscaledDeltaTime * 1.5f);
        }

        /// <summary>A replacement file for a sound, from Resources/Audio or Resources/Music.</summary>
        private static AudioClip LoadOverride(string name)
        {
            var clip = Resources.Load<AudioClip>("Audio/" + name);
            if (clip == null) clip = Resources.Load<AudioClip>("Music/" + name);
            if (clip != null) Debug.Log($"[Audio] using {name} from file ({clip.length:F0}s)");
            return clip;
        }

        private static AudioClip ToClip(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, SoundSynth.Rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // ------------------------------------------------------------------ API

        public static void Play(Sfx sfx, float volume = 1f)
        {
            var a = Instance;
            if (a == null || !a._clips.TryGetValue(sfx, out var clip)) return;
            a._sfx.PlayOneShot(clip, volume);
            // Let the big moments stand out over the music.
            if (sfx == Sfx.Goal || sfx == Sfx.Miss || sfx == Sfx.Save || sfx == Sfx.FinalWhistle)
                a._duckUntil = Time.unscaledTime + clip.length;
        }

        public static void Music(MusicTrack track)
        {
            if (Instance != null) Instance._wanted = track;
        }

        /// <summary>Sets the master volume (0 = muted) and saves it.</summary>
        public void SetVolume(float volume)
        {
            var wasOn = SoundOn;
            Volume = Mathf.Clamp01(volume);
            if (Volume > 0.001f) _volumeBeforeMute = Volume;
            AudioListener.volume = Volume;
            PlayerPrefs.SetFloat(VolumeKey, Volume);
            PlayerPrefs.SetInt(PrefKey, SoundOn ? 1 : 0);
            PlayerPrefs.Save();
            VolumeChanged?.Invoke(Volume);
            if (wasOn != SoundOn) SoundChanged?.Invoke(SoundOn);
        }

        /// <summary>Mute, or restore the volume used before muting.</summary>
        public void ToggleSound() => SetVolume(SoundOn ? 0f : Mathf.Max(_volumeBeforeMute, 0.2f));
    }
}
