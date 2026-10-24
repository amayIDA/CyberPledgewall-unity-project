using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Calm, endless ambient bed for both apps. The loop (soft evolving pads, a low sub, and a sparse
/// echoing pluck over Am - Fmaj7 - C - G) is synthesised on a background thread at start, so there
/// are no audio files to ship. Starts itself in every scene and survives scene loads.
/// </summary>
public class AmbientMusic : MonoBehaviour
{
    /// <summary>Master music volume (0-1). Keep it low so the interface sounds stay on top.</summary>
    public static float Volume = 0.28f;

    private const int Rate = 22050;
    private const float ChordSeconds = 8f;
    private const float FadeInSeconds = 5f;

    private static AmbientMusic instance;

    private AudioSource source;
    private float[] data;
    private volatile bool ready;
    private bool started;
    private float fade;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (instance != null)
            return;

        var go = new GameObject("AmbientMusic");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<AmbientMusic>();
    }

    private void Start()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
        if (FindAnyObjectByType<AudioListener>() == null)
            gameObject.AddComponent<AudioListener>();

        Task.Run(() =>
        {
            try
            {
                data = Synthesize();
                ready = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Ambient music synthesis failed: " + e.Message);
            }
        });
    }

    private void Update()
    {
        if (!ready)
            return;

        if (!started)
        {
            started = true;
            AudioClip clip = AudioClip.Create("ambient_loop", data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            data = null;
            source.clip = clip;
            source.Play();
        }

        fade = Mathf.MoveTowards(fade, 1f, Time.unscaledDeltaTime / FadeInSeconds);
        source.volume = Volume * fade;
    }

    // ---- synthesis (runs off the main thread: no Unity API below this line) ---------------------

    private static readonly float[][] Chords =
    {
        new[] { 220.00f, 261.63f, 329.63f, 493.88f },   // Am add9
        new[] { 174.61f, 220.00f, 261.63f, 329.63f },   // Fmaj7
        new[] { 261.63f, 329.63f, 392.00f, 587.33f },   // C add9
        new[] { 196.00f, 246.94f, 293.66f, 329.63f },   // G6
    };

    private static readonly float[] Roots = { 55.00f, 43.65f, 65.41f, 49.00f };

    // Which chord tone each 1/2-second step plucks (-1 = rest).
    private static readonly int[] Pattern = { 0, -1, 2, 1, -1, 3, 2, -1, 1, 3, -1, 2, 0, -1, 3, -1 };

    private static float[] Synthesize()
    {
        int perChord = Mathf.RoundToInt(ChordSeconds * Rate);
        int n = perChord * Chords.Length;
        var buf = new float[n];
        const double TwoPi = Math.PI * 2.0;

        // Pads + sub bass: each chord fades in over 2.5 s before its bar and out over 2.5 s after,
        // so neighbours overlap and the loop wraps without a seam.
        const float Pre = 2.5f, Post = 2.5f;
        for (int c = 0; c < Chords.Length; c++)
        {
            int start = c * perChord;
            int from = start - Mathf.RoundToInt(Pre * Rate);
            int to = start + perChord + Mathf.RoundToInt(Post * Rate);
            float[] notes = Chords[c];
            for (int i = from; i < to; i++)
            {
                float rel = (i - start) / (float)Rate;
                float env = rel < 0f ? Smooth((rel + Pre) / Pre)
                          : rel > ChordSeconds ? Smooth(1f - (rel - ChordSeconds) / Post)
                          : 1f;
                if (env <= 0f)
                    continue;

                double t = i / (double)Rate;
                float sample = 0f;
                for (int k = 0; k < notes.Length; k++)
                {
                    float f = notes[k];
                    float swell = 0.75f + 0.25f * (float)Math.Sin(TwoPi * (0.07 + 0.013 * k) * t + k * 1.7);
                    sample += (float)(Math.Sin(TwoPi * f * t) + Math.Sin(TwoPi * f * 1.004 * t)) * 0.5f * swell;
                    sample += (float)Math.Sin(TwoPi * f * 2.0 * t) * 0.12f * swell;
                }

                sample *= 0.16f;
                sample += (float)(Math.Sin(TwoPi * Roots[c] * t) + 0.22 * Math.Sin(TwoPi * Roots[c] * 2.0 * t)) * 0.30f;
                buf[Wrap(i, n)] += sample * env;
            }
        }

        // Sparse plucks with a tape-style echo.
        const float Step = 0.5f;
        float[] echoDelay = { 0f, 0.375f, 0.75f, 1.125f };
        float[] echoGain = { 1f, 0.42f, 0.2f, 0.09f };
        int steps = Mathf.RoundToInt(ChordSeconds / Step);
        int pluckLen = Mathf.RoundToInt(2.4f * Rate);
        for (int c = 0; c < Chords.Length; c++)
        {
            for (int s = 0; s < steps; s++)
            {
                int tone = Pattern[s % Pattern.Length];
                if (tone < 0)
                    continue;

                float f = Chords[c][tone] * 2f;
                int origin = c * perChord + Mathf.RoundToInt(s * Step * Rate);
                float accent = 0.75f + 0.25f * (float)Math.Sin(s * 1.9 + c);
                for (int e = 0; e < echoDelay.Length; e++)
                {
                    int offset = origin + Mathf.RoundToInt(echoDelay[e] * Rate);
                    for (int j = 0; j < pluckLen; j++)
                    {
                        float dt = j / (float)Rate;
                        float env = Mathf.Min(1f, dt / 0.012f) * (float)Math.Exp(-dt * 3.4);
                        double ph = TwoPi * f * dt;
                        float v = (float)(Math.Sin(ph) + 0.28 * Math.Sin(ph * 2.0) + 0.08 * Math.Sin(ph * 3.0));
                        buf[Wrap(offset + j, n)] += v * env * echoGain[e] * accent * 0.085f;
                    }
                }
            }
        }

        // Soften the top end, then normalise.
        float lp = 0f;
        float peak = 0.0001f;
        for (int i = 0; i < n; i++)
        {
            lp += 0.55f * (buf[i] - lp);
            buf[i] = lp;
            peak = Mathf.Max(peak, Mathf.Abs(lp));
        }

        float gain = 0.8f / peak;
        for (int i = 0; i < n; i++)
            buf[i] *= gain;

        return buf;
    }

    private static int Wrap(int i, int n)
    {
        int r = i % n;
        return r < 0 ? r + n : r;
    }

    private static float Smooth(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }
}
