using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Five-card Figma constellation with the newest pledge at the center. New submissions
/// unfurl through a magnetic glass aperture; older pledges rotate through the satellites
/// every five idle seconds.
/// </summary>
public class WallBoard : MonoBehaviour
{
    public const int MaxVisible = 5;
    private const int MinVisible = 5;
    private const int SparkCount = 120;

    [SerializeField] private PledgeCard[] cards;
    [SerializeField] private PledgeCard heroCard;
    [SerializeField] private Vector2 heroCardSize = new Vector2(920f, 530f);
    [SerializeField, Min(1f)] private float idleSeconds = 5f;

    [Header("Arrival animation (tweak live in Play mode)")]
    [SerializeField, Range(1.5f, 3.5f)] private float flyInSeconds = 2.15f;
    [SerializeField] private float chargeSeconds = 0.48f;
    [SerializeField] private float holdSeconds = 1.0f;
    [SerializeField] private float swoopSeconds = 1.05f;

    [Header("3D pledge history prelude")]
    [SerializeField] private bool showHistoryFlight = true;
    [SerializeField, Range(3f, 9f)] private float historyFlightSeconds = 5.8f;
    private PledgeHistoryFlight historyFlight;

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
        if (started && Keyboard.current != null && Keyboard.current[Key.Digit2].wasPressedThisFrame)
            PlayJourneyPreview();
#endif

        if (!started || featureRoutine != null || idleRoutine != null || items.Count <= SlotCount)
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
            foreach(var card in pool)card.CancelRoutine();
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
        NoteActivity();
        if (historyFlight != null) historyFlight.Hide();
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
        if(revealFx!=null)revealFx.SetState(0,0);
        if (started)
            ShowGrid(-1, false);
    }

    private void OnDisable()
    {
        if (historyFlight != null) historyFlight.Hide();
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
    public void PlayPreview() => BeginPreview(false);

    [ContextMenu("Preview 3D Journey With Sample History")]
    public void PlayJourneyPreview() => BeginPreview(true);

    private void BeginPreview(bool sampleHistory)
    {
        if (!started)
            return;

        // Restart cleanly if a previous preview is still running, and don't let previews pile up.
        if (featureRoutine != null)
        {
            StopCoroutine(featureRoutine);
            featureRoutine = null;
            HideHero();
            if(revealFx!=null)revealFx.SetState(0,0);
        }
        if (historyFlight != null) historyFlight.Hide();

        if (idleRoutine != null)
        {
            StopCoroutine(idleRoutine);idleRoutine=null;
            foreach(var card in pool)card.CancelRoutine();
        }
        pendingFeature = false;
        items.RemoveAll(i => i.isPreview);
        if (sampleHistory)
        {
            for (int i = 0; i < 12; i++)
                items.Add(new Item { name = PreviewNames[i % PreviewNames.Length], drawing = MakePreviewDrawing(), isPreview = true });
        }

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
        new Color(0.55f, 0.92f, 1f), new Color(1f, 1f, 1f), new Color(0.24f, 0.62f, 1f), new Color(0.45f, 0.7f, 1f)
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
            int arrivingIndex = items.Count - 1;
            yield return PlayArrival(items[arrivingIndex], arrivingIndex);
        }

        NoteActivity();
        featureRoutine = null;
    }

    private PledgeRevealFx revealFx;
    private void EnsureRevealFx()
    {
        if(revealFx!=null)return;
        var go=new GameObject("MagneticAperture",typeof(RectTransform),typeof(CanvasRenderer),typeof(PledgeRevealFx));
        go.transform.SetParent(transform.parent,false);
        var rect=(RectTransform)go.transform;
        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0.5f,0.5f);
        rect.anchoredPosition=DesignPosition(0);rect.sizeDelta=new Vector2(1000,1000);
        revealFx=go.GetComponent<PledgeRevealFx>();revealFx.raycastTarget=false;
        revealFx.SetState(0,0);
    }
    private IEnumerator PlayArrival(Item item, int arrivingIndex)
    {
        EnsurePool();EnsureRevealFx();RefreshDepth();
        revealFx.SetState(0,0);
        int slots=SlotCount;
        Vector2 slotPos=DesignPosition(0);
        var hero=GetHero();hero.CancelRoutine();HideHero();
        revealFx.transform.SetAsLastSibling();
        if (showHistoryFlight && items.Count > 1)
        {
            var history = new List<PledgeHistoryEntry>();
            for (int i = 0; i < arrivingIndex; i++)
            {
                var earlier = items[i];
                history.Add(new PledgeHistoryEntry { name = earlier.name, drawing = earlier.drawing });
            }
            if (history.Count > 0)
            {
                if (historyFlight == null) historyFlight = PledgeHistoryFlight.Create(transform.parent);
                yield return historyFlight.Play(pool[1], history, depthPx, historyFlightSeconds, visibility =>
                {
                    for (int i = 0; i < slots; i++)
                    {
                        Vector2 position = DesignPosition(i);
                        pool[i].SetPose(position, Vector3.zero, 1f, pool[i].RestingAlpha * (1f - visibility));
                    }
                }, pool.GetRange(0, slots), progress => revealFx.SetAnticipation(progress * 0.65f));
            }
        }
        // Older pledges now frame an empty stage. Let the light breathe before
        // creating the new card; nobody is shown in the center and then removed.
        pool[0].SetEmpty();
        pool[0].SetPose(slotPos,Vector3.zero,1f,0f);
        revealFx.transform.SetAsLastSibling();
        yield return UITween.Run(0.65f,t=>revealFx.SetAnticipation(0.65f+Mathf.Sin(t*Mathf.PI)*0.12f),x=>x);
        yield return DimGrid(slots,DesignSize(1));
        FillSlotsWithNewest(slots);
        for(int i=0;i<slots;i++)
        {
            if(slotItem[i]>=0)pool[i].Populate(items[slotItem[i]].name,items[slotItem[i]].drawing);
            else pool[i].SetEmpty();
        }
        pool[0].SetPose(slotPos,Vector3.zero,1,0);

        // Anticipation: broken luminous arcs pull inward, then briefly tighten.
        UiSfx.Scan();
        yield return UITween.Run(chargeSeconds,t=>revealFx.SetState(t,Mathf.Lerp(0.65f,1f,Mathf.Sin(t*Mathf.PI*0.5f))),x=>x);
        hero.gameObject.SetActive(true);hero.Rect.SetAsLastSibling();
        hero.Rect.sizeDelta=DesignSize(0);hero.Populate(item.name,item.drawing);hero.SetNameProgress(0);
        hero.SetInkBloom(0);
        Vector3 rest=new Vector3(slotPos.x,slotPos.y,-depthPx*0.025f);
        UiSfx.Whoosh();
        // Spin strictly around the vertical axis. The card stays upright
        // throughout its entrance and its lettering is revealed on the catch.
        bool secondWhoosh=false, catchAccent=false;
        yield return UITween.Run(flyInSeconds,t=>
        {
            Vector3 pos,euler;
            float scale;
            if(t<0.16f)
            {
                float e=UITween.InOutCubic(t/0.16f);
                pos=rest+Vector3.Lerp(new Vector3(0,-85,depthPx*0.18f),new Vector3(0,20,depthPx*0.07f),e);
                euler=new Vector3(0,Mathf.Lerp(-70,-145,e),0);
                scale=Mathf.Lerp(0.2f,0.6f,e);
            }
            else if(t<0.76f)
            {
                float q=(t-0.16f)/0.6f;
                float turn=1f-Mathf.Pow(1f-q,1.35f);
                float sweep=UITween.OutCubic(q);
                pos=rest+Vector3.Lerp(new Vector3(0,20,depthPx*0.07f),new Vector3(0,18,0),sweep);
                euler=new Vector3(0,-145f+900f*turn,0);
                scale=Mathf.Lerp(0.6f,1.06f,sweep);
            }
            else
            {
                float q=(t-0.76f)/0.24f;
                float e=UITween.OutBack(q);
                pos=rest+new Vector3(0,Mathf.LerpUnclamped(18,0,e),0);
                euler=new Vector3(0,Mathf.LerpUnclamped(755,720,e),0);
                scale=Mathf.LerpUnclamped(1.06f,1f,e);
            }
            hero.SetPose3D(pos,euler,scale,Mathf.Clamp01(t*10f));
            hero.SetInkBloom(Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.80f,0.98f,t)));
            revealFx.SetState(0.9f+t*1.9f,1f-t*0.65f);
            if(!secondWhoosh && t>0.45f){secondWhoosh=true;UiSfx.Swoop();hero.Flash();}
            if(!catchAccent && t>0.76f){catchAccent=true;UiSfx.Burst(0.22f);hero.Flash();}
        },x=>x);
        hero.SetPose3D(rest,Vector3.zero,1f,1f);
        hero.SetInkBloom(1);hero.Flash();UiSfx.Burst(0.4f);
        yield return UITween.Run(0.34f,t=>
        {
            hero.SetNameProgress(t);
            hero.SetPose3D(rest,Vector3.zero,1,1);
            revealFx.SetState(1,0.65f);
        },x=>x);
        hero.SetNameProgress(1);revealFx.SetState(1,0.65f);
        // Let the handwriting breathe; subtle counter-rotation makes the glass feel suspended.
        yield return UITween.Run(holdSeconds,t=>
        {
            hero.SetPose3D(rest+new Vector3(0,Mathf.Sin(t*Mathf.PI*2)*4,0),Vector3.zero,1,1);
            revealFx.SetState(1,0.65f);
        },x=>x);
        UiSfx.Swoop();RestoreGrid(slots,DesignSize(1));
        // Magnetic docking: a short upward anticipation, then a restrained elastic settle.
        yield return UITween.Run(swoopSeconds,t=>
        {
            float e=UITween.OutCubic(t);
            Vector3 pos=Vector3.Lerp(rest,new Vector3(slotPos.x,slotPos.y,0),e);
            pos.y+=Mathf.Sin(t*Mathf.PI)*-18;
            hero.SetPose3D(pos,Vector3.zero,1+Mathf.Sin(t*Mathf.PI)*0.025f,1);
            revealFx.SetState(1,0.65f);
        },x=>x);
        pool[0].ShowLanded(slotPos,DesignSize(0));HideHero();UiSfx.Land();
        revealFx.transform.SetAsLastSibling();
        pool[0].HighlightFor(6f);
        revealFx.BeginAfterglow(pool[0],6f);
        yield return new WaitForSecondsRealtime(0.35f);
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
                card.Rect.sizeDelta = Vector2.Lerp(startSize[i], DesignSize(i), t);
                card.SetPose3D(
                    Vector3.Lerp(startPos[i], new Vector3(target.x, target.y, depthPx * 0.06f), t),
                    Vector3.zero,
                    Mathf.Lerp(startScale[i], 0.94f, t),
                    Mathf.Lerp(startAlpha[i], active && i > 0 ? 0.2f : 0f, t));
            }
        }, UITween.InOutCubic);
    }

    private void RestoreGrid(int slots, Vector2 cardSize)
    {
        GetLayout(slots, out _, out int cols, out _);
        for (int i = 1; i < slots; i++)
        {
            int distance = (i % cols) + (i / cols);
            pool[i].RestoreFromDim(GridPosition(i, slots), DesignSize(i), 0.085f * i);
        }
    }

    private IEnumerator IdleFlip()
    {
        int slots = SlotCount;
        // Snapshot hidden entries before replacing any slot. A card leaving this
        // batch must not immediately reappear elsewhere in the same batch.
        var hidden = new List<int>();
        for(int i=0;i<items.Count;i++)
            if(!IsOnScreen(i))hidden.Add(i);
        hidden.Sort((a,b)=>items[a].lastShownStamp.CompareTo(items[b].lastShownStamp));
        var order = new List<int>();
        for(int i=0;i<slots;i++)
            if(!pool[i].IsHighlighted)order.Add(i);
        Shuffle(order);
        int count = Mathf.Min(2,Mathf.Min(hidden.Count,order.Count));
        for(int i=0;i<count;i++)
        {
            int slot=order[i],next=hidden[i];
            AssignSlot(slot,next);
            Item item=items[next];
            pool[slot].FlipTo(item.name,item.drawing,i*0.12f);
        }
        yield return new WaitForSecondsRealtime(1.1f);
        idleRoutine=null;
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
            card.ReleaseToGrid(slot, DesignSize(i));
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

    public static Vector2 DesignPosition(int index)
    {
        switch(index)
        {
            case 0: return new Vector2(-0.04f,-17.54f);
            case 1: return new Vector2(-720.76f,169.24f);
            case 2: return new Vector2(-507.28f,-244.28f);
            case 3: return new Vector2(700.70f,164.80f);
            default: return new Vector2(527.24f,-283.28f);
        }
    }
    public static Vector2 DesignSize(int index) => index == 0 ? new Vector2(768.24f,768.12f) : new Vector2(585.84f,585.6f);
    private void GetLayout(int slots,out Vector2 size,out int cols,out int rows)
    {
        size=DesignSize(1); cols=3; rows=2;
    }
    private Vector2 GridPosition(int index,int slots) => DesignPosition(index);

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
