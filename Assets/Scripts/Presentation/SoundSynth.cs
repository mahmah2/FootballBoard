using System;
using System.Collections.Generic;

namespace FootballBoard.Presentation
{
    public enum Sfx
    {
        Click, Select, Target, Confirm, Error, Step,
        Pass, LongBall, Shot, Goal, Save, Miss,
        Tackle, TackleWon, TackleLost, Clash,
        Whistle, WhistleLong, FinalWhistle, Swoosh, Coin
    }

    public enum MusicTrack { None, Theme, Match }

    /// <summary>
    /// Generates every sound of the game as raw mono samples (no audio files needed). Pure maths with
    /// no Unity calls, so it can run on a worker thread. Each sound has a file name
    /// (<see cref="FileName(Sfx)"/>); a clip with that name in Resources/Audio replaces it.
    /// </summary>
    public static class SoundSynth
    {
        public const int Rate = 44100;

        public static string FileName(Sfx s) => "sfx_" + s.ToString().ToLowerInvariant();
        public static string FileName(MusicTrack m) => "music_" + m.ToString().ToLowerInvariant();

        // ------------------------------------------------------------------ building blocks

        private enum Wave { Sine, Triangle, SoftSquare, SoftSaw, Pulse }

        private static float[] Buffer(float seconds) => new float[(int)(seconds * Rate)];

        private static float Osc(Wave w, double phase)
        {
            var p = phase - Math.Floor(phase);
            switch (w)
            {
                case Wave.Sine: return (float)Math.Sin(2 * Math.PI * p);
                case Wave.Triangle: return (float)(p < 0.5 ? 4 * p - 1 : 3 - 4 * p);
                case Wave.SoftSquare: return (float)Math.Tanh(3 * Math.Sin(2 * Math.PI * p));
                case Wave.Pulse: return (float)Math.Tanh(4 * (Math.Sin(2 * Math.PI * p) + 0.45));
                default: // soft saw: six harmonics, no harsh aliasing
                    double s = 0;
                    for (var h = 1; h <= 6; h++) s += Math.Sin(2 * Math.PI * h * p) / h;
                    return (float)(s * 0.55);
            }
        }

        /// <summary>
        /// Adds a tone gliding from f0 to f1 (exponentially), with a linear attack, an exponential
        /// decay (per second, 0 = none) and a linear release at the end. Optional vibrato (Hz, depth in
        /// semitones). With <paramref name="wrap"/> the tail continues at the start (seamless loops).
        /// </summary>
        private static void Tone(float[] b, float t0, float dur, float f0, float f1, Wave w, float amp,
            float attack = 0.005f, float decay = 0f, float release = 0.03f, float vibRate = 0f, float vibDepth = 0f, bool wrap = false)
        {
            int start = (int)(t0 * Rate), n = (int)(dur * Rate);
            double phase = 0;
            for (var k = 0; k < n; k++)
            {
                var i = start + k;
                if (i >= b.Length) { if (!wrap) break; i %= b.Length; }
                var t = (float)k / Rate;
                var f = f0 * Math.Pow(f1 / f0, (double)k / n);
                if (vibDepth > 0) f *= Math.Pow(2, vibDepth * Math.Sin(2 * Math.PI * vibRate * t) / 12.0);
                phase += f / Rate;
                var env = Math.Min(1f, t / Math.Max(attack, 1e-4f)) * (float)Math.Exp(-decay * t) * Math.Min(1f, (dur - t) / Math.Max(release, 1e-4f));
                b[i] += amp * env * Osc(w, phase);
            }
        }

        private static readonly Random Rng = new Random(7);

        /// <summary>
        /// Adds noise through a band-pass made of two one-pole filters whose cut-offs glide over the
        /// sound (lowCut0..lowCut1 = high-pass, highCut0..highCut1 = low-pass).
        /// </summary>
        private static void Noise(float[] b, float t0, float dur, float amp, float highCut0, float highCut1,
            float lowCut0 = 0f, float lowCut1 = 0f, float attack = 0.002f, float decay = 0f, float release = 0.03f, bool wrap = false)
        {
            int start = (int)(t0 * Rate), n = (int)(dur * Rate);
            float lp = 0, hp = 0;
            for (var k = 0; k < n; k++)
            {
                var i = start + k;
                if (i >= b.Length) { if (!wrap) break; i %= b.Length; }
                var t = (float)k / Rate;
                var x = (float)(Rng.NextDouble() * 2 - 1);
                var hc = highCut0 + (highCut1 - highCut0) * k / n;
                var lc = lowCut0 + (lowCut1 - lowCut0) * k / n;
                lp += (1f - (float)Math.Exp(-2 * Math.PI * hc / Rate)) * (x - lp);
                hp += (1f - (float)Math.Exp(-2 * Math.PI * Math.Max(lc, 1f) / Rate)) * (lp - hp);
                var y = lc > 0 ? lp - hp : lp;
                var env = Math.Min(1f, t / Math.Max(attack, 1e-4f)) * (float)Math.Exp(-decay * t) * Math.Min(1f, (dur - t) / Math.Max(release, 1e-4f));
                b[i] += amp * env * y;
            }
        }

        private static float Midi(float note) => 440f * (float)Math.Pow(2, (note - 69) / 12f);

        private static float[] Finish(float[] b, float peak = 0.9f)
        {
            var max = 1e-6f;
            foreach (var v in b) max = Math.Max(max, Math.Abs(v));
            var g = peak / max;
            for (var i = 0; i < b.Length; i++) b[i] = (float)Math.Tanh(b[i] * g * 1.1f) / (float)Math.Tanh(1.1f);
            return b;
        }

        // ------------------------------------------------------------------ sound effects

        public static float[] Make(Sfx s)
        {
            float[] b;
            switch (s)
            {
                case Sfx.Click:
                    b = Buffer(0.06f);
                    Tone(b, 0, 0.05f, 1400, 1100, Wave.Sine, 0.6f, 0.001f, 40);
                    return Finish(b, 0.45f);

                case Sfx.Select: // two quick rising blips
                    b = Buffer(0.18f);
                    Tone(b, 0, 0.07f, 880, 900, Wave.Sine, 0.6f, 0.003f, 25);
                    Tone(b, 0.055f, 0.11f, 1320, 1340, Wave.Sine, 0.55f, 0.003f, 22);
                    Tone(b, 0.055f, 0.11f, 2640, 2640, Wave.Sine, 0.12f, 0.003f, 35);
                    return Finish(b, 0.55f);

                case Sfx.Target: // a rising "lock-on" sweep with a sparkle
                    b = Buffer(0.4f);
                    Tone(b, 0, 0.2f, 520, 1560, Wave.Triangle, 0.5f, 0.004f, 6, 0.05f);
                    Tone(b, 0.13f, 0.27f, 2093, 2093, Wave.Sine, 0.3f, 0.002f, 12);
                    Tone(b, 0.17f, 0.22f, 3136, 3136, Wave.Sine, 0.15f, 0.002f, 16);
                    return Finish(b, 0.6f);

                case Sfx.Confirm: // bright major chord blip
                    b = Buffer(0.35f);
                    foreach (var note in new[] { 72f, 76f, 79f, 84f })
                        Tone(b, 0, 0.33f, Midi(note), Midi(note), Wave.SoftSquare, 0.18f, 0.004f, 9, 0.08f);
                    return Finish(b, 0.55f);

                case Sfx.Error: // two low buzzes
                    b = Buffer(0.28f);
                    Tone(b, 0, 0.1f, 196, 190, Wave.SoftSquare, 0.4f, 0.003f, 6);
                    Tone(b, 0.13f, 0.13f, 165, 160, Wave.SoftSquare, 0.4f, 0.003f, 6);
                    return Finish(b, 0.3f);

                case Sfx.Step: // soft studs on grass
                    b = Buffer(0.12f);
                    Noise(b, 0, 0.06f, 0.6f, 1800, 900, 0, 0, 0.001f, 40);
                    Tone(b, 0, 0.08f, 210, 120, Wave.Sine, 0.4f, 0.001f, 30);
                    Noise(b, 0.05f, 0.06f, 0.35f, 1600, 800, 0, 0, 0.001f, 45);
                    return Finish(b, 0.35f);

                case Sfx.Pass: // a clean kick
                    b = Buffer(0.25f);
                    Tone(b, 0, 0.16f, 190, 60, Wave.Sine, 0.9f, 0.001f, 16);
                    Noise(b, 0, 0.035f, 0.5f, 5000, 2000, 0, 0, 0.0005f, 60);
                    Noise(b, 0.02f, 0.2f, 0.12f, 2500, 1200, 500, 500, 0.04f, 8);
                    return Finish(b, 0.7f);

                case Sfx.LongBall: // kick and a long rising whoosh
                    b = Buffer(0.9f);
                    Tone(b, 0, 0.16f, 190, 60, Wave.Sine, 0.9f, 0.001f, 16);
                    Noise(b, 0, 0.035f, 0.5f, 5000, 2000, 0, 0, 0.0005f, 60);
                    Noise(b, 0.04f, 0.8f, 0.45f, 700, 3200, 300, 900, 0.25f, 1.5f, 0.35f);
                    return Finish(b, 0.7f);

                case Sfx.Shot: // a hard strike and a fast whoosh
                    b = Buffer(0.6f);
                    Tone(b, 0, 0.24f, 170, 42, Wave.Sine, 1f, 0.001f, 10);
                    Noise(b, 0, 0.05f, 0.8f, 7000, 2500, 0, 0, 0.0005f, 45);
                    Noise(b, 0.03f, 0.5f, 0.5f, 1500, 6000, 600, 1500, 0.08f, 4, 0.2f);
                    return Finish(b, 0.85f);

                case Sfx.Goal: // fanfare over a roaring crowd
                    b = Buffer(3.6f);
                    var fan = new[] { (67f, 0f, 0.16f), (72f, 0.15f, 0.16f), (76f, 0.3f, 0.16f), (79f, 0.45f, 0.38f) };
                    foreach (var (note, at, len) in fan)
                    {
                        Tone(b, at, len, Midi(note), Midi(note), Wave.SoftSaw, 0.35f, 0.01f, 1, 0.04f);
                        Tone(b, at, len, Midi(note + 12), Midi(note + 12), Wave.Sine, 0.08f, 0.01f, 2, 0.04f);
                    }
                    foreach (var note in new[] { 60f, 64f, 67f, 72f, 76f })
                        Tone(b, 0.82f, 1.5f, Midi(note), Midi(note), Wave.SoftSaw, 0.16f, 0.03f, 0.6f, 0.5f, 5.5f, 0.12f);
                    Tone(b, 0.82f, 0.25f, 120, 45, Wave.Sine, 0.6f, 0.001f, 10);           // timpani hit
                    Noise(b, 0.05f, 3.5f, 0.55f, 2600, 1800, 250, 300, 0.45f, 0.35f, 1.3f); // crowd roar
                    Noise(b, 0.3f, 3.0f, 0.2f, 5000, 3000, 1500, 1500, 0.6f, 0.5f, 1.0f);   // cheering hiss
                    return Finish(b, 0.85f);

                case Sfx.Save: // glove thud and the crowd's "ooh"
                    b = Buffer(1.5f);
                    Tone(b, 0, 0.13f, 130, 70, Wave.Sine, 0.9f, 0.001f, 20);
                    Noise(b, 0, 0.05f, 0.6f, 2200, 900, 0, 0, 0.001f, 50);
                    Tone(b, 0.12f, 1.2f, 330, 270, Wave.Triangle, 0.18f, 0.2f, 1.2f, 0.4f, 5, 0.15f);
                    Noise(b, 0.12f, 1.3f, 0.35f, 1100, 700, 350, 300, 0.25f, 1.0f, 0.5f);
                    return Finish(b, 0.7f);

                case Sfx.Miss: // "wah-wah" sad trombone and a groan
                    b = Buffer(2.2f);
                    var sad = new[] { (67f, 0f, 0.36f), (66f, 0.36f, 0.36f), (65f, 0.72f, 0.36f), (64f, 1.08f, 1.0f) };
                    foreach (var (note, at, len) in sad)
                    {
                        var last = at > 1f;
                        Tone(b, at, len, Midi(note), Midi(note - (last ? 0.3f : 0.15f)), Wave.SoftSaw, 0.3f, 0.03f, last ? 0.8f : 0, 0.08f,
                            last ? 6f : 0f, last ? 0.35f : 0f);
                    }
                    Noise(b, 0.2f, 1.8f, 0.25f, 900, 500, 300, 250, 0.3f, 0.8f, 0.6f);
                    return Finish(b, 0.6f);

                case Sfx.Tackle: // body impact
                    b = Buffer(0.4f);
                    Noise(b, 0, 0.15f, 0.9f, 3200, 900, 0, 0, 0.001f, 22);
                    Tone(b, 0, 0.22f, 115, 48, Wave.Sine, 1f, 0.001f, 12);
                    Noise(b, 0.04f, 0.3f, 0.25f, 4000, 2000, 1200, 1200, 0.01f, 10);
                    return Finish(b, 0.8f);

                case Sfx.TackleWon: // rising major stab
                    b = Buffer(0.45f);
                    foreach (var (note, at) in new[] { (67f, 0f), (72f, 0.07f), (76f, 0.14f) })
                        Tone(b, at, 0.3f, Midi(note), Midi(note), Wave.SoftSquare, 0.25f, 0.004f, 7, 0.08f);
                    return Finish(b, 0.55f);

                case Sfx.TackleLost: // falling minor sigh
                    b = Buffer(0.6f);
                    Tone(b, 0, 0.5f, Midi(64), Midi(60), Wave.SoftSquare, 0.3f, 0.01f, 3, 0.15f);
                    Tone(b, 0, 0.5f, Midi(67), Midi(63), Wave.Triangle, 0.15f, 0.01f, 3, 0.15f);
                    return Finish(b, 0.5f);

                case Sfx.Clash: // two players arrive together
                    b = Buffer(0.6f);
                    Noise(b, 0, 0.25f, 0.4f, 600, 3000, 300, 800, 0.15f, 2, 0.05f);
                    Noise(b, 0.22f, 0.15f, 0.9f, 3000, 900, 0, 0, 0.001f, 22);
                    Tone(b, 0.22f, 0.22f, 120, 50, Wave.Sine, 1f, 0.001f, 12);
                    return Finish(b, 0.75f);

                case Sfx.Whistle:
                    return Finish(Whistle(0.35f), 0.5f);

                case Sfx.WhistleLong:
                    return Finish(Whistle(0.85f), 0.5f);

                case Sfx.FinalWhistle:
                    b = Buffer(1.9f);
                    var w1 = Whistle(0.3f); var w3 = Whistle(1.0f);
                    for (var i = 0; i < w1.Length; i++) { b[i] += w1[i]; b[i + (int)(0.42f * Rate)] += w1[i]; }
                    for (var i = 0; i < w3.Length; i++) b[i + (int)(0.84f * Rate)] += w3[i];
                    return Finish(b, 0.5f);

                case Sfx.Swoosh: // cutscene transition
                    b = Buffer(0.45f);
                    Noise(b, 0, 0.42f, 0.7f, 400, 4500, 200, 1200, 0.18f, 2, 0.15f);
                    return Finish(b, 0.5f);

                case Sfx.Coin: // metallic ping
                    b = Buffer(0.9f);
                    Tone(b, 0, 0.85f, 2600, 2600, Wave.Sine, 0.4f, 0.001f, 5);
                    Tone(b, 0, 0.6f, 3907, 3907, Wave.Sine, 0.25f, 0.001f, 7);
                    Tone(b, 0, 0.4f, 5512, 5512, Wave.Sine, 0.15f, 0.001f, 10);
                    return Finish(b, 0.4f);

                default:
                    return Buffer(0.01f);
            }
        }

        private static float[] Whistle(float dur)
        {
            var b = Buffer(dur + 0.02f);
            // Two close pitches beat against each other; a fast flutter gives the pea rattle.
            int n = (int)(dur * Rate);
            double p1 = 0, p2 = 0;
            for (var k = 0; k < n; k++)
            {
                var t = (float)k / Rate;
                p1 += 2850.0 / Rate; p2 += 2905.0 / Rate;
                var flutter = 0.65f + 0.35f * (float)Math.Sin(2 * Math.PI * 31 * t);
                var env = Math.Min(1f, t / 0.015f) * Math.Min(1f, (dur - t) / 0.04f);
                b[k] = env * flutter * (float)(0.5 * Math.Sin(2 * Math.PI * p1) + 0.4 * Math.Sin(2 * Math.PI * p2));
            }
            return b;
        }

        // ------------------------------------------------------------------ music

        private const float Bpm = 118f;
        private static readonly float Beat = 60f / Bpm;

        // One chord per half bar (2 beats), 8 bars: C G Am F | C G Am F-G. Root as MIDI note, minor flag.
        private static readonly (int root, bool minor)[] Chords =
        {
            (48, false), (48, false), (43, false), (43, false), (45, true), (45, true), (41, false), (41, false),
            (48, false), (48, false), (43, false), (43, false), (45, true), (45, true), (41, false), (43, false),
        };

        // Lead melody: (MIDI note, start beat, length in beats).
        private static readonly (int note, float at, float len)[] Melody =
        {
            (76, 0, 1), (79, 1, 1), (84, 2, 1.5f), (83, 3.5f, 0.5f),
            (79, 4, 1), (74, 5, 0.5f), (76, 5.5f, 0.5f), (79, 6, 2),
            (76, 8, 1), (72, 9, 1), (81, 10, 1.5f), (79, 11.5f, 0.5f),
            (77, 12, 1), (76, 13, 0.5f), (74, 13.5f, 0.5f), (72, 14, 2),
            (76, 16, 0.5f), (79, 16.5f, 0.5f), (84, 17, 1), (86, 18, 1), (84, 19, 1),
            (83, 20, 1.5f), (79, 21.5f, 0.5f), (81, 22, 1), (83, 23, 1),
            (84, 24, 1), (81, 25, 1), (76, 26, 1), (81, 27, 1),
            (79, 28, 1), (77, 29, 1), (74, 30, 1), (71, 31, 1),
        };

        /// <summary>
        /// An 8-bar loop at 118 BPM in C major. Theme (menu): drums, bass, pad, arpeggio and lead.
        /// Match: the same harmony, lighter drums, a slower arpeggio and no lead, to sit under play.
        /// </summary>
        public static float[] Compose(MusicTrack track)
        {
            var full = track == MusicTrack.Theme;
            var beats = Chords.Length * 2;
            var b = new float[(int)(beats * Beat * Rate)];

            for (var h = 0; h < Chords.Length; h++)
            {
                var (root, minor) = Chords[h];
                var tones = new[] { 0, minor ? 3 : 4, 7 };
                var t = h * 2 * Beat;

                // Pad: soft sustained triad.
                foreach (var iv in tones)
                    Tone(b, t, 2 * Beat + 0.05f, Midi(root + 12 + iv), Midi(root + 12 + iv), Wave.SoftSaw, full ? 0.05f : 0.045f, 0.08f, 0, 0.12f, 4.5f, 0.06f, true);

                // Bass: eighth notes, root - root - octave - fifth pattern.
                var bassPattern = new[] { 0, 0, 12, 7 };
                for (var e = 0; e < 4; e++)
                {
                    var note = root - 12 + bassPattern[e];
                    Tone(b, t + e * Beat / 2, Beat / 2 * 0.9f, Midi(note), Midi(note), Wave.Triangle, 0.3f, 0.004f, 3, 0.04f, 0, 0, true);
                    Tone(b, t + e * Beat / 2, Beat / 2 * 0.9f, Midi(note + 12), Midi(note + 12), Wave.Sine, 0.08f, 0.004f, 4, 0.04f, 0, 0, true);
                }

                // Arpeggio: chord tones two octaves up, 16ths (theme) or 8ths (match).
                var steps = full ? 8 : 4;
                var arpOrder = new[] { 0, 1, 2, 1, 0, 2, 1, 2 };
                for (var a = 0; a < steps; a++)
                {
                    var note = root + 24 + tones[arpOrder[a % arpOrder.Length]];
                    var len = 2 * Beat / steps;
                    Tone(b, t + a * len, len * 0.95f, Midi(note), Midi(note), Wave.Triangle, full ? 0.075f : 0.06f, 0.003f, 9, 0.02f, 0, 0, true);
                }
            }

            if (full)
                foreach (var (note, at, len) in Melody)
                    Tone(b, at * Beat, len * Beat * 0.92f, Midi(note), Midi(note), Wave.Pulse, 0.12f, 0.01f, 0.8f, 0.05f, 5.5f, len >= 1.5f ? 0.15f : 0f, true);

            // Drums.
            for (var beat = 0; beat < beats; beat++)
            {
                var t = beat * Beat;
                var inBar = beat % 4;
                var bar = beat / 4;
                if (inBar == 0 || inBar == 2 || (full && inBar == 3 && bar % 2 == 1))
                {
                    var at = inBar == 3 ? t + Beat / 2 : t;
                    Tone(b, at, 0.16f, 150, 45, Wave.Sine, full ? 0.75f : 0.5f, 0.001f, 14, 0.03f, 0, 0, true);
                }
                if (full && (inBar == 1 || inBar == 3))
                {
                    Noise(b, t, 0.13f, 0.32f, 6000, 4000, 900, 900, 0.001f, 22, 0.03f, true);
                    Tone(b, t, 0.08f, 200, 170, Wave.Triangle, 0.18f, 0.001f, 25, 0.02f, 0, 0, true);
                }
                if (!full && inBar == 2)
                    Noise(b, t, 0.05f, 0.12f, 5000, 3000, 2000, 2000, 0.001f, 40, 0.02f, true); // soft rim
                var hats = full ? 2 : 1;
                for (var hh = 0; hh < hats; hh++)
                    Noise(b, t + hh * Beat / 2, 0.035f, hh == 0 ? 0.1f : 0.07f, 12000, 12000, 6000, 6000, 0.0005f, 70, 0.01f, true);
            }
            return Finish(b, 0.8f);
        }
    }
}
