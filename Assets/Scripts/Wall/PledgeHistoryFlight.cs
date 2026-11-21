using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public struct PledgeHistoryEntry
{
    public string name;
    public byte[] drawing;
}

/// <summary>
/// Perspective orbit through the archive, followed by an exact pose match into
/// the five-card Figma wall. The same visible cards perform the entire movement.
/// </summary>
public class PledgeHistoryFlight : MonoBehaviour
{
    private const int Capacity = 24;
    private const int LandingCards = WallBoard.MaxVisible - 1;
    private readonly List<PledgeCard> cards = new List<PledgeCard>();
    private readonly Vector3[] positions = new Vector3[Capacity];
    private readonly Vector3[] angles = new Vector3[Capacity];
    private readonly float[] scales = new float[Capacity];
    private readonly float[] depths = new float[Capacity];
    private readonly int[] order = new int[Capacity];
    private readonly byte[][] assigned = new byte[Capacity][];
    private readonly string[] assignedNames = new string[Capacity];
    private readonly bool[] populated = new bool[Capacity];
    private readonly PledgeHistoryEntry[] entries = new PledgeHistoryEntry[Capacity];
    private CanvasGroup group;
    private Image veil;
    private int sampleCursor;

    public bool IsRunning { get; private set; }
    public bool HasDocked { get; private set; }
    public float LastDockError { get; private set; }
    public int VisiblePoolCount => cards.Count;
    public int LastHistoryCount { get; private set; }

    public static PledgeHistoryFlight Create(Transform parent)
    {
        var go = new GameObject("PledgeHistoryFlight", typeof(RectTransform), typeof(CanvasGroup));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var flight = go.AddComponent<PledgeHistoryFlight>();
        flight.group = go.GetComponent<CanvasGroup>();
        flight.group.blocksRaycasts = flight.group.interactable = false;
        var backdrop = new GameObject("HistoryAtmosphere", typeof(RectTransform), typeof(Image));
        backdrop.transform.SetParent(rect, false);
        var br = (RectTransform)backdrop.transform;
        br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one;
        br.offsetMin = br.offsetMax = Vector2.zero;
        flight.veil = backdrop.GetComponent<Image>();
        flight.veil.raycastTarget = false;
        flight.Hide();
        return flight;
    }

    private static float Smooth(float value)
    {
        float t = Mathf.Clamp01(value);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    public IEnumerator Play(PledgeCard template, IList<PledgeHistoryEntry> history, float depth,
        float seconds, Action<float> visibility = null, IList<PledgeCard> destination = null, Action<float> anticipation = null)
    {
        if (template == null || history.Count == 0 || seconds <= 0f) yield break;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        group.alpha = 1f;
        IsRunning = true;
        HasDocked = false;
        LastHistoryCount = history.Count;
        int count = Mathf.Clamp(history.Count, LandingCards, Capacity);
        while (cards.Count < count)
        {
            int i = cards.Count;
            var source = destination != null && i < LandingCards && i + 1 < destination.Count ? destination[i + 1] : template;
            var card = Instantiate(source, transform);
            card.name = "HistoricalPledge_" + i;
            card.CancelRoutine();
            cards.Add(card);
        }
        int olderCount = Mathf.Max(0, history.Count - LandingCards);
        int offset = olderCount > 0 ? sampleCursor % olderCount : 0;
        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].gameObject.SetActive(i < count);
            if (i >= count) continue;
            int index;
            if (i < LandingCards) index = history.Count - 1 - i;
            else index = (offset + (i - LandingCards) * olderCount /
                Mathf.Max(1, count - LandingCards)) % olderCount;
            bool hasEntry = index >= 0;
            entries[i] = hasEntry ? history[index] : default;
            var entry = entries[i];
            var card = cards[i];
            if (!populated[i] || assigned[i] != entry.drawing || assignedNames[i] != entry.name)
            {
                if (hasEntry) card.Populate(entry.name, entry.drawing); else card.SetEmpty();
                populated[i] = true;
                assigned[i] = entry.drawing;
                assignedNames[i] = entry.name;
            }
            card.Rect.sizeDelta = WallBoard.DesignSize(1);
            card.ClearReveal(); card.SetInkBloom(1f);
            float angle = i * 2.399963f + 0.35f;
            float radius = 1.15f + (i % 3) * 0.23f;
            // The five landing cards stay ahead of the camera. Other cards span
            // the whole volume, producing near passes and strong relative motion.
            float z = i < LandingCards ? 3.3f + i * 0.42f : 0.9f + (i % 7) * 0.78f;
            positions[i] = new Vector3(Mathf.Sin(angle) * radius * depth,
                Mathf.Cos(angle) * (0.57f + (i % 2) * 0.17f) * depth, z * depth);
            angles[i] = new Vector3(Mathf.Sin(angle) * 8f, -Mathf.Sin(angle) * 18f, Mathf.Cos(angle) * 4f);
            scales[i] = 0.94f + (i % 3) * 0.15f;
            order[i] = i;
            if (destination != null && i < LandingCards && i + 1 < destination.Count)
            {
                destination[i + 1].CancelRoutine();
                if (hasEntry) destination[i + 1].Populate(entry.name, entry.drawing); else destination[i + 1].SetEmpty();
            }
        }
        sampleCursor++;
        visibility?.Invoke(1f);
        float duration = history.Count < 3 ? Mathf.Min(seconds, 3.8f) : seconds;
        UiSfx.Whoosh();
        yield return UITween.Run(duration, t =>
        {
            float enter = Smooth(t / 0.17f);
            float orbit = Smooth(t / 0.73f);
            float dock = Smooth(Mathf.InverseLerp(0.65f, 1f, t));
            // A broad 68-degree orbit with a dolly-in and a small vertical rise,
            // rather than only sending cards forward through a straight tunnel.
            float heading = Mathf.Lerp(38f, -30f, orbit);
            float radius = Mathf.Lerp(4.8f, 2.15f, orbit) * depth;
            Vector3 center = new Vector3(0, 0, depth * 3.6f);
            Vector3 eye = center + Quaternion.Euler(0, heading, 0) * new Vector3(0, 0, -radius);
            eye.y = Mathf.Sin(orbit * Mathf.PI) * depth * 0.21f;
            Quaternion cameraRotation = Quaternion.LookRotation(center - eye, Vector3.up) *
                Quaternion.Euler(0, 0, Mathf.Sin(orbit * Mathf.PI * 2f) * -1.6f);
            Quaternion inverse = Quaternion.Inverse(cameraRotation);
            anticipation?.Invoke(dock);
            veil.color = new Color(0.012f, 0.038f, 0.12f, 0.82f * enter * (1f - dock));
            for (int i = 0; i < count; i++)
            {
                Vector3 at = positions[i];
                at.y += Mathf.Sin(Time.unscaledTime * 0.55f + i) * depth * 0.008f * (1f - dock);
                Vector3 relative = inverse * (at - eye);
                Vector3 projected = new Vector3(relative.x, relative.y - 20f, relative.z - depth);
                Quaternion rotation = inverse * Quaternion.Euler(angles[i]);
                float distance = relative.z / depth;
                float alpha = Smooth(Mathf.InverseLerp(0.30f, 0.8f, distance)) *
                    (1f - Smooth(Mathf.InverseLerp(8f, 11f, distance)));
                float scale = scales[i];
                if (i < LandingCards)
                {
                    Vector2 initial = WallBoard.DesignPosition(i);
                    Vector3 start = new Vector3(initial.x, initial.y, 0f);
                    Vector2 layout = WallBoard.DesignPosition(i + 1);
                    Vector3 home = new Vector3(layout.x, layout.y, 0f);
                    // Leave the exact wall pose, orbit, then land in that same pose.
                    // Both blends have zero endpoint velocity and overlap the orbit.
                    projected = Vector3.LerpUnclamped(start, projected, enter);
                    rotation = Quaternion.Slerp(Quaternion.identity, rotation, enter);
                    scale = Mathf.Lerp(i == 0 ? WallBoard.DesignSize(0).x / WallBoard.DesignSize(1).x : 1f, scale, enter);
                    projected = Vector3.LerpUnclamped(projected, home, dock);
                    rotation = Quaternion.Slerp(rotation, Quaternion.identity, dock);
                    scale = Mathf.Lerp(scale, 1f, dock);
                    alpha = Mathf.Lerp(Mathf.Lerp(cards[i].RestingAlpha, alpha, enter), cards[i].RestingAlpha, dock);
                    if (t >= 1f)
                    {
                        projected = home; rotation = Quaternion.identity; scale = 1f; alpha = cards[i].RestingAlpha;
                    }
                }
                else
                {
                    float exit = Smooth(Mathf.InverseLerp(0.64f, 0.93f, t));
                    projected.x += Mathf.Sign(projected.x + 0.01f) * exit * depth * 1.2f;
                    projected.z += exit * depth * 0.8f;
                    alpha *= enter * (1f - exit);
                }
                cards[i].SetPose3D(projected, rotation.eulerAngles, scale, alpha);
                depths[i] = projected.z;
                cards[i].gameObject.SetActive(alpha > 0.001f);
                order[i] = i;
            }
            for (int i = 1; i < count; i++)
            {
                int key = order[i], j = i - 1;
                while (j >= 0 && depths[order[j]] < depths[key]) { order[j + 1] = order[j]; j--; }
                order[j + 1] = key;
            }
            for (int i = 0; i < count; i++) cards[order[i]].Rect.SetSiblingIndex(i + 1);
        }, x => x);

        LastDockError = 0f;
        for (int i = 0; i < LandingCards; i++)
        {
            Vector2 layout = WallBoard.DesignPosition(i + 1);
            LastDockError = Mathf.Max(LastDockError, Vector3.Distance(cards[i].Rect.anchoredPosition3D,
                new Vector3(layout.x, layout.y, 0)));
            if (destination != null && i + 1 < destination.Count)
            {
                destination[i + 1].Rect.sizeDelta = WallBoard.DesignSize(i + 1);
                destination[i + 1].SetPose(layout, Vector3.zero, 1f, destination[i + 1].RestingAlpha);
            }
        }
        if (destination != null && destination.Count > 0)
        {
            // The newest historical pledge has moved to satellite 1. Reserve the
            // center for light and the incoming pledge, with no departing occupant.
            destination[0].SetEmpty();
            destination[0].Rect.sizeDelta = WallBoard.DesignSize(0);
            destination[0].SetPose(WallBoard.DesignPosition(0), Vector3.zero, 1f, 0f);
        }
        HasDocked = true;
        Hide();
        // The destination now exactly matches the last rendered gallery frame.
        if (destination == null) visibility?.Invoke(0f);
    }

    public void Hide()
    {
        IsRunning = false;
        if (group != null) group.alpha = 0;
        gameObject.SetActive(false);
    }
}
