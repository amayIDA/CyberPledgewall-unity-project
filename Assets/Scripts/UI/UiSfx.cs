using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sound effects synthesised at runtime (no audio files needed): button clicks, the pledge arrival
/// whoosh / scanner hum / sparkle burst / landing thud, and the visitor-side confirmation chimes.
/// </summary>
public static class UiSfx
{
    private const int Rate = 44100;

    public static float Volume = 0.85f;

    private static AudioSource source;
    private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();

    public static void Click() => Play("click", 0.9f);
    public static void Tick() => Play("tick", 0.35f);
    public static void Whoosh() => Play("whoosh", 0.9f);
    public static void Swoop() => Play("swoop", 0.8f);
    public static void Scan() => Play("scan", 0.55f);
    public static void Burst(float volume = 1f) => Play("burst", volume);
    public static void Land() => Play("land", 1f);
    public static void Success() => Play("success", 0.9f);
    public static void Error() => Play("error", 0.7f);

    private static void Play(string name, float volume)
    {
        if (!Application.isPlaying)
            return;

        EnsureSource();
        if (!Clips.TryGetValue(name, out AudioClip clip) || clip == null)
        {
            clip = Create(name);
            Clips[name] = clip;
        }

        if (clip != null)
            source.PlayOneShot(clip, Mathf.Clamp01(volume * Volume));
    }

    private static void EnsureSource()
    {
        if (source != null)
            return;

        var go = new GameObject("UiSfx");
        UnityEngine.Object.DontDestroyOnLoad(go);
        source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        if (UnityEngine.Object.FindAnyObjectByType<AudioListener>() == null)
            go.AddComponent<AudioListener>();
    }

    // ---- synthesis -----------------------------------------------------------------------------

    private static AudioClip Create(string name)
    {
        switch (name)
        {
            case "click": return Build(name, 0.12f, ClickGen());
            case "tick": return Build(name, 0.05f, (t, n) => Mathf.Sin(2f * Mathf.PI * 3400f * t) * Mathf.Exp(-t * 170f) * 0.5f);
            case "whoosh": return Build(name, 1.15f, WhooshGen(1.15f, 0.02f, 0.42f, 70f, 210f, 0.55f));
            case "swoop": return Build(name, 0.95f, WhooshGen(0.95f, 0.30f, 0.04f, 220f, 90f, 0.4f));
            case "scan": return Build(name, 2.0f, ScanGen());
            case "burst": return Build(name, 1.2f, BurstGen());
            case "land": return Build(name, 0.7f, LandGen());
            case "success": return Build(name, 1.0f, SuccessGen());
            case "error": return Build(name, 0.3f, (t, n) => Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 170f * t)) * Mathf.Exp(-t * 9f) * 0.35f);
        }

        return null;
    }

    private static AudioClip Build(string name, float seconds, Func<float, float, float> sample)
    {
        int count = Mathf.CeilToInt(seconds * Rate);
        var data = new float[count];
        float peak = 0.0001f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)Rate;
            float v = sample(t, i / (float)count);
            data[i] = v;
            peak = Mathf.Max(peak, Mathf.Abs(v));
        }

        // Normalise and fade the last few ms so nothing clicks at the end.
        float gain = 0.9f / peak;
        int fade = Mathf.Min(count, Rate / 200);
        for (int i = 0; i < count; i++)
        {
            float f = i > count - fade ? (count - i) / (float)fade : 1f;
            data[i] *= gain * f;
        }

        AudioClip clip = AudioClip.Create("sfx_" + name, count, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static Func<float, float, float> ClickGen()
    {
        return (t, n) =>
            Mathf.Sin(2f * Mathf.PI * 1500f * t) * Mathf.Exp(-t * 55f) * 0.55f +
            Mathf.Sin(2f * Mathf.PI * 2250f * t) * Mathf.Exp(-t * 90f) * 0.3f;
    }

    /// <summary>Filtered noise swell with a low tone; cutoff glides lowCut -> highCut.</summary>
    private static Func<float, float, float> WhooshGen(float seconds, float cutA, float cutB, float toneA, float toneB, float peak)
    {
        var rng = new System.Random(7);
        float lp = 0f;
        float phase = 0f;
        return (t, n) =>
        {
            float env = n < peak ? Mathf.Pow(n / peak, 2f) : Mathf.Pow((1f - n) / (1f - peak), 1.5f);
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += Mathf.Lerp(cutA, cutB, n) * (noise - lp);
            phase += 2f * Mathf.PI * Mathf.Lerp(toneA, toneB, n) / Rate;
            return (lp * 2.2f + Mathf.Sin(phase) * 0.35f) * env;
        };
    }

    private static Func<float, float, float> ScanGen()
    {
        var rng = new System.Random(11);
        float phase = 0f;
        float phase2 = 0f;
        return (t, n) =>
        {
            float freq = 260f + 1150f * (n * n * 0.4f + n * 0.6f);
            phase += 2f * Mathf.PI * freq / Rate;
            phase2 += 2f * Mathf.PI * freq * 2.01f / Rate;
            float tremolo = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * 13f * t);
            float env = Mathf.Clamp01(n * 12f) * Mathf.Clamp01((1f - n) * 8f);
            float air = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.05f;
            return (Mathf.Sin(phase) * 0.5f + Mathf.Sin(phase2) * 0.18f + air) * tremolo * env;
        };
    }

    private static Func<float, float, float> BurstGen()
    {
        var rng = new System.Random(3);
        float[] notes = { 1318.5f, 1760f, 2093f, 2637f };
        float lp = 0f;
        return (t, n) =>
        {
            float sum = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                float dt = t - i * 0.055f;
                if (dt < 0f)
                    continue;
                sum += (Mathf.Sin(2f * Mathf.PI * notes[i] * dt) + 0.25f * Mathf.Sin(2f * Mathf.PI * notes[i] * 2f * dt))
                       * Mathf.Exp(-dt * 4.2f) * 0.3f;
            }

            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += 0.25f * (noise - lp);
            float crackle = (noise - lp) * Mathf.Exp(-t * 22f) * 0.55f;
            return sum + crackle;
        };
    }

    private static Func<float, float, float> LandGen()
    {
        var rng = new System.Random(5);
        float phase = 0f;
        return (t, n) =>
        {
            float freq = 52f + 95f * Mathf.Exp(-t * 15f);
            phase += 2f * Mathf.PI * freq / Rate;
            float thump = Mathf.Sin(phase) * Mathf.Exp(-t * 7f) * 0.9f;
            float tick = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 180f) * 0.4f;
            return thump + tick;
        };
    }

    private static Func<float, float, float> SuccessGen()
    {
        return (t, n) =>
        {
            float a = Bell(t, 783.99f);
            float b = Bell(t - 0.13f, 1174.66f);
            float c = Bell(t - 0.27f, 1567.98f);
            return (a + b + c) * 0.4f;
        };
    }

    private static float Bell(float t, float freq)
    {
        if (t < 0f)
            return 0f;
        return (Mathf.Sin(2f * Mathf.PI * freq * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * freq * 2.76f * t)) * Mathf.Exp(-t * 4.5f);
    }
}
