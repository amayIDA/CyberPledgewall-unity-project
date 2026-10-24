using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Neon grid starts at 6 slots and grows to 12 as pledges arrive. New submissions play the
/// center-stage spin, then return to the grid. After two idle minutes the wall flips a few
/// cards to drawings that have been off-screen the longest.
/// </summary>
public class WallBoard : MonoBehaviour
{
    public const int MaxVisible = 12;
    private const int MinVisible = 6;
    private const int SparkCount = 120;

    [SerializeField] private PledgeCard[] cards;
    [SerializeField] private PledgeCard heroCard;
    [SerializeField] private Vector2 heroCardSize = new Vector2(920f, 530f);
    [SerializeField] private float idleSeconds = 120f;
    [SerializeField] private float gridGap = 56f;

    [Header("Arrival animation (tweak live in Play mode)")]
    [SerializeField] private float flyInSeconds = 1.2f;
    [SerializeField] private float scanSeconds = 2.0f;
    [SerializeField] private float holdSeconds = 1.0f;
    [SerializeField] private float swoopSeconds = 1.05f;

    [Header("Preview")]
    [Tooltip("Press this key in the editor / dev builds to replay the arrival animation with a made-up pledge.")]
    [SerializeField] private Key previewKey = Key.Digit1;

    private struct Item
    {
        public string name;
        public byte[] drawing;
        public int lastShownStamp;
        public bool isPreview;
    }

    private struct Spark
    {
        public RectTransform rt;
        public Image img;
        public Vector3 pos;
        public Vector3 vel;
        public float life;
        public float total;
        public float spin;
        public float angle;
        public float size;
        public Color color;
    }

    private Spark[] sparks;
    private RectTransform sparkRoot;
    private int sparkCursor;
    private bool pendingFeature;
    private float depthPx = 1000f;

    private readonly List<Item> items = new List<Item>();
    private readonly List<PledgeCard> pool = new List<PledgeCard>();
    private readonly int[] slotItem = new int[MaxVisible];
    private Coroutine featureRoutine;
    private Coroutine idleRoutine;
    private bool started;
    private int showStamp;
    private float lastActivity = -999f;

    private int SlotCount => Mathf.Clamp(Mathf.Max(items.Count, MinVisible), MinVisible, MaxVisible);

    private void Awake()
    {
        for (int i = 0; i < slotItem.Length; i++)
            slotItem[i] = -1;
        EnsurePool();
    }

    private void Start()
    {
        started = true;
        lastActivity = Time.unscaledTime;
        ShowGrid(-1, false);
    }

    private void Update()
    {
        TickSparks(Time.unscaledDeltaTime);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (started && Keyboard.current != null && Keyboard.current[previewKey].wasPressedThisFrame)
            PlayPreview();
#endif

        if (!started || featureRoutine != null || items.Count == 0)
            return;

        if (Time.unscaledTime - lastActivity < idleSeconds)
            return;

        lastActivity = Time.unscaledTime;
        if (idleRoutine != null)
            StopCoroutine(idleRoutine);
        idleRoutine = StartCoroutine(IdleFlip());
    }

    public void Add(string visitorName, byte[] drawing)
    {
        items.Add(new Item { name = visitorName, drawing = drawing, lastShownStamp = 0 });
        NoteActivity();
        if (!started)
            return;

        if (idleRoutine != null)
        {
            StopCoroutine(idleRoutine);
            idleRoutine = null;
        }

        pendingFeature = true;
        if (featureRoutine == null)
            featureRoutine = StartCoroutine(FeatureNewestThenGrid());
    }

    public void AddSilently(string visitorName, byte[] drawing)
    {
        items.Add(new Item { name = visitorName, drawing = drawing, lastShownStamp = 0 });
    }

    public void Refresh()
    {
        if (started)
            ShowGrid(-1, false);
    }

    public void Clear()
    {
        pendingFeature = false;
        items.Clear();
        for (int i = 0; i < slotItem.Length; i++)
            slotItem[i] = -1;
        if (featureRoutine != null)
        {
            StopCoroutine(featureRoutine);
            featureRoutine = null;
        }

        if (idleRoutine != null)
        {
            StopCoroutine(idleRoutine);
            idleRoutine = null;
        }

        HideHero();
        if (started)
            ShowGrid(-1, false);
    }

    private void NoteActivity()
    {
        lastActivity = Time.unscaledTime;
    }

    // ---- preview (press the preview key) -----------------------------------------------------

    private static readonly string[] PreviewNames =
        { "Alex Morgan", "Priya Nair", "Sam Okafor", "Mei Tanaka", "Jordan Lee", "Zara Khan" };

    /// <summary>Replays the arrival animation with a generated scribble. Nothing is saved to disk.</summary>
    [ContextMenu("Play Preview Arrival")]
    public void PlayPreview()
    {
        if (!started)
            return;

        // Restart cleanly if a previous preview is still running, and don't let previews pile up.
        if (featureRoutine != null)
        {
            StopCoroutine(featureRoutine);
            featureRoutine = null;
            HideHero();
        }

        pendingFeature = false;
        items.RemoveAll(i => i.isPreview);

        items.Add(new Item
        {
            name = PreviewNames[Random.Range(0, PreviewNames.Length)],
            drawing = MakePreviewDrawing(),
            lastShownStamp = 0,
            isPreview = true
        });

        NoteActivity();
        pendingFeature = true;
        featureRoutine = StartCoroutine(FeatureNewestThenGrid());
    }

    private static byte[] MakePreviewDrawing()
    {
        const int w = DrawingCanvas.TextureWidth;
        const int h = DrawingCanvas.TextureHeight;
        var pixels = new Color32[w * h];

        // A looping, signature-like stroke with a random rhythm each time.
        float freq = Random.Range(2.2f, 4.5f);
        float amp = Random.Range(70f, 130f);
        float loops = Random.Range(0.8f, 1.8f);
        float phase = Random.value * 6f;
        for (int i = 0; i < 2600; i++)
        {
            float u = i / 2599f;
            float x = 70f + u * (w - 140f) + Mathf.Sin(u * freq * Mathf.PI * 2f + phase) * 36f * loops;
            float y = h * 0.5f + Mathf.Sin(u * freq * Mathf.PI * 2f + phase + 1.2f) * amp * (0.45f + 0.55f * Mathf.Sin(u * Mathf.PI));
            float radius = Mathf.Lerp(2.2f, 6.5f, Mathf.Sin(u * Mathf.PI));
            int x0 = Mathf.Max(0, Mathf.FloorToInt(x - radius - 1f)), x1 = Mathf.Min(w - 1, Mathf.CeilToInt(x + radius + 1f));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(y - radius - 1f)), y1 = Mathf.Min(h - 1, Mathf.CeilToInt(y + radius + 1f));
            for (int py = y0; py <= y1; py++)
            {
                for (int px = x0; px <= x1; px++)
                {
                    float d = Mathf.Sqrt((px + 0.5f - x) * (px + 0.5f - x) + (py + 0.5f - y) * (py + 0.5f - y));
                    if (d > radius)
                        continue;
                    byte a = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255f);
                    int idx = py * w + px;
                    if (a > pixels[idx].a)
                        pixels[idx] = new Color32(10, 10, 20, a);
                }
            }
        }

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels32(pixels);
        tex.Apply();
        byte[] png = tex.EncodeToPNG();
        Destroy(tex);
        return png;
    }

    // ---- arrival choreography ----------------------------------------------------------------

    private void RefreshDepth()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return;
        canvas = canvas.rootCanvas;

        // Real perspective needs a camera canvas; overlay canvases render rotations flat.
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            Camera cam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
            if (cam != null && !cam.orthographic)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = Mathf.Clamp(100f, cam.nearClipPlane + 1f, Mathf.Max(cam.nearClipPlane + 2f, cam.farClipPlane - 1f));
                Canvas.ForceUpdateCanvases();
            }
        }

        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            float scale = canvas.transform.lossyScale.x;
            if (scale > 0.00001f)
                depthPx = canvas.planeDistance / scale;
        }
    }

    private void EnsureSparks()
    {
        if (sparks != null)
            return;

        Transform parent = transform.parent != null ? transform.parent : transform;
        var rootGo = new GameObject("Sparks", typeof(RectTransform));
        sparkRoot = (RectTransform)rootGo.transform;
        sparkRoot.SetParent(parent, false);
        sparkRoot.anchorMin = sparkRoot.anchorMax = sparkRoot.pivot = new Vector2(0.5f, 0.5f);
        sparkRoot.anchoredPosition3D = Vector3.zero;
        sparkRoot.sizeDelta = Vector2.zero;

        sparks = new Spark[SparkCount];
        for (int i = 0; i < SparkCount; i++)
        {
            var go = new GameObject("Spark", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(sparkRoot, false);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            RectTransform r = img.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            go.SetActive(false);
            sparks[i] = new Spark { rt = r, img = img };
        }
    }

    private void Emit(Vector3 pos, Vector3 vel, float life, Color color, float size)
    {
        if (sparks == null)
            return;

        for (int n = 0; n < sparks.Length; n++)
        {
            sparkCursor = (sparkCursor + 1) % sparks.Length;
            if (sparks[sparkCursor].life > 0f)
                continue;

            ref Spark s = ref sparks[sparkCursor];
            s.pos = pos;
            s.vel = vel;
            s.life = s.total = life;
            s.spin = Random.Range(-420f, 420f);
            s.angle = Random.Range(0f, 360f);
            s.size = size;
            s.color = color;
            s.rt.gameObject.SetActive(true);
            s.rt.sizeDelta = new Vector2(size, size);
            return;
        }
    }

    private static readonly Color[] SparkColors =
    {
        new Color(0.55f, 0.92f, 1f), new Color(1f, 1f, 1f), new Color(1f, 0.82f, 0.35f), new Color(0.45f, 0.7f, 1f)
    };

    private void Burst(Vector3 origin, int count, float speed)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = Random.value * Mathf.PI * 2f;
            float s = speed * Random.Range(0.35f, 1f);
            var vel = new Vector3(Mathf.Cos(angle) * s, Mathf.Sin(angle) * s * 0.85f, Random.Range(-0.3f, 0.3f) * s);
            Emit(origin, vel, Random.Range(0.7f, 1.5f), SparkColors[Random.Range(0, SparkColors.Length)], Random.Range(5f, 13f));
        }
    }

    private void TickSparks(float dt)
    {
        if (sparks == null)
            return;

        for (int i = 0; i < sparks.Length; i++)
        {
            ref Spark s = ref sparks[i];
            if (s.life <= 0f)
                continue;

            s.life -= dt;
            if (s.life <= 0f)
            {
                s.rt.gameObject.SetActive(false);
                continue;
            }

            s.vel *= Mathf.Max(0f, 1f - 2.4f * dt);
            s.vel.y -= 220f * dt;
            s.pos += s.vel * dt;
            s.angle += s.spin * dt;

            float k = s.life / s.total;
            s.rt.anchoredPosition3D = s.pos;
            s.rt.localRotation = Quaternion.Euler(0f, 0f, s.angle);
            s.rt.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, k);
            Color c = s.color;
            c.a = Mathf.Clamp01(k * 1.6f);
            s.img.color = c;
        }
    }

    private Vector3 Sway(float amount)
    {
        float t = Time.unscaledTime;
        return new Vector3(Mathf.Sin(t * 1.3f) * 3f, Mathf.Sin(t * 0.9f) * 7f, Mathf.Sin(t * 0.7f) * 1.4f) * amount;
    }

    private IEnumerator FeatureNewestThenGrid()
    {
        while (pendingFeature)
        {
            pendingFeature = false;
            if (items.Count == 0)
                break;

            // Several arrivals at once: the newest gets the stage, the rest land in the grid.
            yield return PlayArrival(items[items.Count - 1]);
        }

        NoteActivity();
        featureRoutine = null;
    }

    private IEnumerator PlayArrival(Item item)
    {
        EnsurePool();
        EnsureSparks();
        RefreshDepth();

        int slots = SlotCount;
        GetLayout(slots, out Vector2 cardSize, out _, out _);
        Vector2 slotPos = GridPosition(0, slots);
        PledgeCard hero = GetHero();
        hero.CancelRoutine();
        HideHero();

        // 1. The grid recedes into the background and re-flows for the new slot count.
        yield return DimGrid(slots, cardSize);

        // 2. Re-seat content under the dim; slot 0 stays empty until the hero lands in it.
        FillSlotsWithNewest(slots);
        for (int i = 0; i < slots; i++)
        {
            int idx = slotItem[i];
            if (idx >= 0)
                pool[i].Populate(items[idx].name, items[idx].drawing);
            else
                pool[i].SetEmpty();
        }

        pool[0].SetPose3D(new Vector3(slotPos.x, slotPos.y, depthPx * 0.06f), Vector3.zero, 0.94f, 0f);

        // 3. Hero flies in from deep space, swinging round to face the viewer.
        hero.gameObject.SetActive(true);
        hero.Rect.SetAsLastSibling();
        hero.Rect.sizeDelta = heroCardSize;
        hero.Populate(item.name, item.drawing);
        hero.PrepareReveal();
        sparkRoot.SetAsLastSibling();

        var from = new Vector3(-820f, -420f, depthPx * 0.9f);
        var rest = new Vector3(0f, 30f, -depthPx * 0.1f);
        var eulerFrom = new Vector3(-30f, 78f, 20f);

        UiSfx.Whoosh();
        yield return UITween.Run(flyInSeconds, t =>
        {
            float e = UITween.OutCubic(t);
            Vector3 pos = Vector3.LerpUnclamped(from, rest, e);
            pos.y += Mathf.Sin(t * Mathf.PI) * 170f;
            Vector3 euler = Vector3.LerpUnclamped(eulerFrom, Vector3.zero, UITween.OutBack(t));
            hero.SetPose3D(pos, euler, 1f, Mathf.Clamp01(t * 3.2f));
            hero.Flash();
        }, x => x);
        Burst(rest, 14, 380f);
        UiSfx.Burst(0.45f);

        // 4. The handwriting is "scanned" onto the card by a light bar, shedding sparks.
        UiSfx.Scan();
        float sparkClock = 0f;
        yield return UITween.Run(scanSeconds, t =>
        {
            float p = Mathf.Lerp(t, t * t * (3f - 2f * t), 0.65f);
            hero.SetInkProgress(p);
            hero.SetPose3D(rest + new Vector3(0f, Mathf.Sin(Time.unscaledTime * 1.7f) * 6f, Mathf.Sin(Time.unscaledTime * 1.1f) * depthPx * 0.01f),
                Sway(1f), 1f, 1f);

            sparkClock += Time.unscaledDeltaTime;
            while (sparkClock > 0.022f && p > 0.01f && p < 0.99f)
            {
                sparkClock -= 0.022f;
                Vector3 at = sparkRoot.InverseTransformPoint(hero.ScanWorldPoint);
                Emit(at, new Vector3(Random.Range(10f, 110f), Random.Range(-260f, 260f), Random.Range(-60f, 60f)),
                    Random.Range(0.4f, 0.9f), SparkColors[Random.Range(0, 3)], Random.Range(3f, 7f));
            }

            if (p >= 0.99f)
                sparkClock = 0f;
        }, x => x);
        hero.SetInkProgress(1f);

        // 5. Name types in, then the card pulses with a burst.
        int nameLength = string.IsNullOrEmpty(item.name) ? 0 : item.name.Length;
        int lastShown = 0;
        yield return UITween.Run(0.5f, t =>
        {
            hero.SetNameProgress(t);
            int shown = Mathf.CeilToInt(nameLength * t);
            if (shown != lastShown)
            {
                lastShown = shown;
                UiSfx.Tick();
            }

            hero.SetPose3D(rest, Sway(1f), 1f, 1f);
        }, x => x);
        hero.SetNameProgress(1f);
        hero.Flash();
        Burst(hero.Rect.anchoredPosition3D, 46, 620f);
        UiSfx.Burst();

        yield return UITween.Run(holdSeconds, t =>
        {
            float punch = 1f + 0.075f * Mathf.Sin(Mathf.Min(t * 3.2f, 1f) * Mathf.PI) * Mathf.Exp(-1.5f * t);
            hero.SetPose3D(rest + new Vector3(0f, Mathf.Sin(Time.unscaledTime * 1.7f) * 6f, 0f), Sway(1.15f), punch, 1f);
        }, x => x);

        // 6. Swoop into its place on the wall.
        Vector3 startPos = hero.Rect.anchoredPosition3D;
        bool restored = false;
        UiSfx.Swoop();
        yield return UITween.Run(swoopSeconds, t =>
        {
            float e = UITween.InOutCubic(t);
            float arc = Mathf.Sin(t * Mathf.PI);
            Vector3 pos = Vector3.Lerp(startPos, new Vector3(slotPos.x, slotPos.y, 0f), e);
            pos.y += arc * 120f;
            pos.z = Mathf.Lerp(startPos.z, 0f, e) - arc * depthPx * 0.09f;
            Vector3 euler = new Vector3(-arc * 10f, -arc * 20f, arc * 6f) + Sway(1f - e);
            hero.Rect.sizeDelta = Vector2.Lerp(heroCardSize, cardSize, e);
            hero.SetPose3D(pos, euler, 1f, 1f);

            if (!restored && t > 0.5f)
            {
                restored = true;
                RestoreGrid(slots, cardSize);
            }
        }, x => x);

        // 7. Land: swap hero for the real grid card, ripple, settle.
        pool[0].ShowLanded(slotPos, cardSize);
        HideHero();
        UiSfx.Land();
        Burst(new Vector3(slotPos.x, slotPos.y, 0f), 22, 320f);

        yield return new WaitForSecondsRealtime(0.9f);
    }

    private IEnumerator DimGrid(int slots, Vector2 cardSize)
    {
        var startPos = new Vector3[MaxVisible];
        var startSize = new Vector2[MaxVisible];
        var startAlpha = new float[MaxVisible];
        var startScale = new float[MaxVisible];

        for (int i = 0; i < MaxVisible; i++)
        {
            PledgeCard card = pool[i];
            card.CancelRoutine();
            RectTransform r = card.Rect;
            Vector2 target = GridPosition(i, slots);
            bool visible = card.gameObject.activeSelf && card.Alpha > 0.01f;
            startPos[i] = visible ? r.anchoredPosition3D : new Vector3(target.x, target.y, 0f);
            startSize[i] = visible ? r.sizeDelta : cardSize;
            startAlpha[i] = visible ? card.Alpha : 0f;
            startScale[i] = visible ? r.localScale.x : 0.9f;
        }

        yield return UITween.Run(0.55f, t =>
        {
            for (int i = 0; i < MaxVisible; i++)
            {
                bool active = i < slots;
                Vector2 target = GridPosition(i, slots);
                PledgeCard card = pool[i];
                card.Rect.sizeDelta = Vector2.Lerp(startSize[i], cardSize, t);
                card.SetPose3D(
                    Vector3.Lerp(startPos[i], new Vector3(target.x, target.y, depthPx * 0.06f), t),
                    Vector3.zero,
                    Mathf.Lerp(startScale[i], 0.94f, t),
                    Mathf.Lerp(startAlpha[i], active ? 0.2f : 0f, t));
            }
        }, UITween.InOutCubic);
    }

    private void RestoreGrid(int slots, Vector2 cardSize)
    {
        GetLayout(slots, out _, out int cols, out _);
        for (int i = 1; i < slots; i++)
        {
            int distance = (i % cols) + (i / cols);
            pool[i].RestoreFromDim(GridPosition(i, slots), cardSize, 0.07f * distance);
        }
    }

    private IEnumerator IdleFlip()
    {
        int slots = SlotCount;
        if (items.Count == 0)
            yield break;

        int flips = Mathf.Clamp(slots / 3, 2, 4);
        var order = new List<int>(slots);
        for (int i = 0; i < slots; i++)
            order.Add(i);
        Shuffle(order);

        int done = 0;
        for (int n = 0; n < order.Count && done < flips; n++)
        {
            int slot = order[n];
            int next = NextHiddenItem(slotItem[slot]);
            if (next < 0)
                continue;

            AssignSlot(slot, next);
            Item item = items[next];
            pool[slot].FlipTo(item.name, item.drawing, done * 0.12f);
            done++;
        }

        yield return new WaitForSecondsRealtime(1.1f);
        idleRoutine = null;
    }

    private int NextHiddenItem(int current)
    {
        int best = -1;
        int bestStamp = int.MaxValue;
        for (int i = 0; i < items.Count; i++)
        {
            if (IsOnScreen(i) && i != current)
                continue;
            if (i == current)
                continue;

            int stamp = items[i].lastShownStamp;
            if (stamp < bestStamp)
            {
                bestStamp = stamp;
                best = i;
            }
        }

        if (best < 0 && items.Count > 1)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (i == current)
                    continue;
                if (items[i].lastShownStamp < bestStamp)
                {
                    bestStamp = items[i].lastShownStamp;
                    best = i;
                }
            }
        }

        return best;
    }

    private bool IsOnScreen(int itemIndex)
    {
        int slots = SlotCount;
        for (int i = 0; i < slots; i++)
        {
            if (slotItem[i] == itemIndex)
                return true;
        }

        return false;
    }

    private void AssignSlot(int slot, int itemIndex)
    {
        slotItem[slot] = itemIndex;
        if (itemIndex < 0)
            return;

        Item item = items[itemIndex];
        item.lastShownStamp = ++showStamp;
        items[itemIndex] = item;
    }


    private void HideHero()
    {
        PledgeCard hero = GetHero();
        if (hero != null)
            hero.SetPose(new Vector2(0f, 900f), Vector3.zero, 0.01f, 0f);
    }

    private void ShowGrid(int highlightSlot, bool forcePopulate)
    {
        EnsurePool();
        HideHero();

        int slots = SlotCount;
        GetLayout(slots, out Vector2 size, out _, out _);
        FillSlotsWithNewest(slots);

        for (int i = 0; i < MaxVisible; i++)
        {
            PledgeCard card = pool[i];
            if (i >= slots)
            {
                card.SetPose(new Vector2(0f, 900f), Vector3.zero, 0.01f, 0f);
                continue;
            }

            Vector2 slot = GridPosition(i, slots);
            card.ReleaseToGrid(slot, size);
            int itemIndex = slotItem[i];
            if (itemIndex >= 0)
            {
                Item item = items[itemIndex];
                if (forcePopulate || !card.HasPledge)
                    card.Populate(item.name, item.drawing);
                card.Reveal(0.05f * i + (i == highlightSlot ? 0.12f : 0f), i == highlightSlot);
            }
            else
            {
                card.SetEmpty();
                card.Reveal(0.04f * i, false);
            }
        }
    }

    private void FillSlotsWithNewest(int slots)
    {
        int filled = Mathf.Min(slots, items.Count);
        for (int i = 0; i < filled; i++)
        {
            int itemIndex = items.Count - 1 - i;
            AssignSlot(i, itemIndex);
        }

        for (int i = filled; i < MaxVisible; i++)
            slotItem[i] = -1;
    }

    private void GetLayout(int slots, out Vector2 cardSize, out int cols, out int rows)
    {
        if (slots <= 6)
        {
            cols = 3;
            rows = 2;
        }
        else if (slots <= 8)
        {
            cols = 4;
            rows = 2;
        }
        else if (slots <= 9)
        {
            cols = 3;
            rows = 3;
        }
        else
        {
            cols = 4;
            rows = 3;
        }

        const float usableW = 1680f;
        const float usableH = 860f;
        float cellW = usableW / cols;
        float cellH = usableH / rows;
        cardSize = new Vector2(cellW - gridGap, cellH - gridGap);
    }

    private Vector2 GridPosition(int index, int slots)
    {
        GetLayout(slots, out _, out int cols, out int rows);
        const float usableW = 1680f;
        const float usableH = 860f;
        float cellW = usableW / cols;
        float cellH = usableH / rows;
        int col = index % cols;
        int row = index / cols;
        float x = (col - (cols - 1) * 0.5f) * cellW;
        float y = ((rows - 1) * 0.5f - row) * cellH;
        return new Vector2(x, y);
    }

    private PledgeCard GetHero()
    {
        EnsurePool();
        if (heroCard == null && pool.Count > MaxVisible)
            heroCard = pool[MaxVisible];
        return heroCard;
    }

    private void EnsurePool()
    {
        if (pool.Count >= MaxVisible + 1)
            return;

        pool.Clear();
        Transform parent = transform.parent != null ? transform.parent : transform;

        if (cards != null)
        {
            foreach (PledgeCard card in cards)
            {
                if (card != null && !pool.Contains(card))
                    pool.Add(card);
            }
        }

        PledgeCard template = pool.Count > 0 ? pool[0] : heroCard;
        if (template == null)
            return;

        while (pool.Count < MaxVisible + 1)
        {
            var clone = Instantiate(template, parent);
            clone.name = pool.Count == MaxVisible ? "HeroCard" : "PledgeCard_" + pool.Count;
            clone.gameObject.SetActive(true);
            pool.Add(clone);
        }

        heroCard = pool[MaxVisible];
        for (int i = 0; i < pool.Count; i++)
        {
            RectTransform rt = pool[i].Rect;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        cards = pool.GetRange(0, MaxVisible).ToArray();
    }

    private static void Shuffle(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }
}
