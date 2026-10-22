using System;
using System.Collections;
using UnityEngine;

/// <summary>Tiny coroutine tween helpers (unscaled time) so the UI animates without extra packages.</summary>
public static class UITween
{
    public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
    public static float InCubic(float t) => t * t * t;
    public static float InOutCubic(float t) => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

    public static float OutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    public static float OutElastic(float t)
    {
        if (t <= 0f || t >= 1f)
            return Mathf.Clamp01(t);
        const float c4 = 2f * Mathf.PI / 3f;
        return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
    }

    public static IEnumerator Run(float duration, Action<float> apply, Func<float, float> ease = null, float delay = 0f)
    {
        ease = ease ?? OutCubic;
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        float time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            apply(ease(Mathf.Clamp01(time / duration)));
            yield return null;
        }

        apply(ease(1f));
    }

    public static Coroutine Stop(MonoBehaviour owner, Coroutine routine)
    {
        if (routine != null && owner != null)
            owner.StopCoroutine(routine);
        return null;
    }
}
