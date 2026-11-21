using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One glass card on the wall: the visitor's handwriting rendered as white ink, with their name beneath.
/// WallBoard can drive pose (carousel / hero); otherwise the card bobs in place.
/// </summary>
public class PledgeCard : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private RawImage drawingImage;
    [SerializeField] private AspectRatioFitter drawingFitter;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Image glow;
    [SerializeField] private float emptyAlpha = 0.28f;

    private RectTransform rt;
    private Texture2D texture;
    private Coroutine routine;
    private float bobPhase;
    private float flash;
    private float highlightUntil;
    public bool IsHighlighted => Time.unscaledTime < highlightUntil;
    public void HighlightFor(float seconds) => highlightUntil = Time.unscaledTime + seconds;
    private bool hasPledge;
    private bool posedExternally;
    private Vector2 restPosition;

    public bool HasPledge => hasPledge;
    public float RestingAlpha => hasPledge ? 1f : emptyAlpha;
    public RectTransform Rect => rt != null ? rt : (rt = (RectTransform)transform);

    private void Awake()
    {
        rt = (RectTransform)transform;
        bobPhase = Random.value * 10f;
        if (group == null)
            group = GetComponent<CanvasGroup>();
        if (group.alpha <= 0f)
            group.alpha = 0f;
    }

    private void OnDestroy()
    {
        if (texture != null)
            Destroy(texture);
    }

    private void Update()
    {
        if (posedExternally)
        {
            TickGlow();
            return;
        }

        float time = Time.unscaledTime + bobPhase;
        rt.anchoredPosition = restPosition + new Vector2(Mathf.Sin(time * 0.55f) * 2f, Mathf.Sin(time * 0.8f) * 5f);
        TickGlow();
    }

    private void TickGlow()
    {
        float time = Time.unscaledTime + bobPhase;
        flash = Mathf.MoveTowards(flash, 0f, Time.unscaledDeltaTime * 1.4f);
        if (glow == null)
            return;

        float shimmer = hasPledge ? 0.12f + 0.1f * Mathf.Sin(time * 1.3f) : 0f;
        Color c = glow.color;
        float left=highlightUntil-Time.unscaledTime;
        float accent=left>0?(0.34f+0.08f*Mathf.Sin(time*2f))*Mathf.SmoothStep(0,1,Mathf.Clamp01(left/1.5f)):0;
        c.a = Mathf.Max(Mathf.Max(shimmer, flash),accent);
        glow.color = c;
    }

    public void Flash()
    {
        flash = 1f;
    }

    public void ReleaseToGrid(Vector2 slotPosition, Vector2 size)
    {
        posedExternally = false;
        if (rt == null)
            rt = (RectTransform)transform;
        restPosition = slotPosition;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition3D = new Vector3(slotPosition.x, slotPosition.y, 0f);
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
        gameObject.SetActive(true);
    }

    public void SetPose(Vector2 position, Vector3 euler, float scale, float alpha)
    {
        SetPose3D(new Vector3(position.x, position.y, 0f), euler, scale, alpha);
    }

    /// <summary>Full pose including depth (z, canvas units; negative = toward the viewer).</summary>
    public void SetPose3D(Vector3 position, Vector3 euler, float scale, float alpha)
    {
        posedExternally = true;
        if (rt == null)
            rt = (RectTransform)transform;
        rt.anchoredPosition3D = position;
        rt.localRotation = Quaternion.Euler(euler);
        rt.localScale = Vector3.one * scale;
        if (group == null)
            group = GetComponent<CanvasGroup>();
        group.alpha = alpha;
        gameObject.SetActive(alpha > 0.001f || scale > 0.01f);
    }

    public void Populate(string visitorName, byte[] drawingBytes)
    {
        ApplyContent(visitorName, drawingBytes);
        hasPledge = true;
    }

    public void SetEmpty()
    {
        ApplyContent(null, null);
        hasPledge = false;
    }

    public void Reveal(float delay, bool highlight = false)
    {
        posedExternally = false;
        Restart(RevealRoutine(delay, highlight));
    }

    public void Conceal(float delay)
    {
        Restart(ConcealRoutine(delay));
    }

    public Coroutine FlipTo(string visitorName, byte[] drawingBytes, float delay = 0f)
    {
        Restart(FlipRoutine(visitorName, drawingBytes, delay));
        return routine;
    }

    private void Restart(IEnumerator next)
    {
        if (routine != null)
            StopCoroutine(routine);
        routine = StartCoroutine(next);
    }

    private IEnumerator RevealRoutine(float delay, bool highlight)
    {
        float target = hasPledge ? 1f : emptyAlpha;
        float startScale = highlight ? 0.55f : 0.84f;
        group.alpha = 0f;
        transform.localScale = Vector3.one * startScale;

        yield return UITween.Run(highlight ? 0.9f : 0.65f, t =>
        {
            transform.localScale = Vector3.one * Mathf.LerpUnclamped(startScale, 1f, t);
            group.alpha = Mathf.Clamp01(t * 1.6f) * target;
            if (highlight && t > 0.55f && flash < 0.05f)
                flash = 1f;
        }, highlight ? UITween.OutElastic : UITween.OutBack, delay);

        transform.localScale = Vector3.one;
        group.alpha = target;
        routine = null;
    }

    private IEnumerator ConcealRoutine(float delay)
    {
        float from = group.alpha;
        yield return UITween.Run(0.3f, t =>
        {
            group.alpha = Mathf.Lerp(from, 0f, t);
            transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.9f, t);
        }, UITween.InCubic, delay);
        routine = null;
    }

    private IEnumerator FlipRoutine(string visitorName, byte[] drawingBytes, float delay)
    {
        posedExternally = true;
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        Vector2 pos = restPosition;
        yield return UITween.Run(0.28f, t =>
        {
            SetPose3D(new Vector3(pos.x, pos.y, -70f * t), new Vector3(0f, 90f * t, 0f), 1f, 1f);
        }, UITween.InCubic);

        Populate(visitorName, drawingBytes);
        Flash();

        yield return UITween.Run(0.38f, t =>
        {
            SetPose3D(new Vector3(pos.x, pos.y, -70f * (1f - t)), new Vector3(0f, -90f * (1f - t), 0f), 1f, 1f);
        }, UITween.OutCubic);

        posedExternally = false;
        ReleaseToGrid(restPosition, rt.sizeDelta);
        routine = null;
    }

    // ---- arrival choreography helpers --------------------------------------------------------

    private RectMask2D inkMask;
    private Image scanCore;
    private Image scanGlow;
    private Coroutine punchRoutine;
    private Coroutine restoreRoutine;

    public float Alpha => group != null ? group.alpha : 0f;

    private RectTransform InkArea => drawingImage != null ? drawingImage.rectTransform.parent as RectTransform : null;

    /// <summary>World position of the ink scanner's bright edge (for emitting sparks).</summary>
    public Vector3 ScanWorldPoint => scanCore != null ? scanCore.rectTransform.position : transform.position;

    public void CancelRoutine()
    {
        if (routine != null)
            StopCoroutine(routine);
        routine = null;
        if(punchRoutine != null) StopCoroutine(punchRoutine);
        punchRoutine=null;
        if (restoreRoutine != null)
            StopCoroutine(restoreRoutine);
        restoreRoutine = null;
    }

    /// <summary>Hide ink + name so they can be revealed by SetInkProgress / SetNameProgress.</summary>
    public void PrepareReveal()
    {
        SetInkProgress(0f);
        SetNameProgress(0f);
    }

    public void SetInkProgress(float p)
    {
        RectTransform area = InkArea;
        if (area == null)
            return;

        if (inkMask == null)
        {
            inkMask = area.GetComponent<RectMask2D>();
            if (inkMask == null)
                inkMask = area.gameObject.AddComponent<RectMask2D>();
        }

        p = Mathf.Clamp01(p);
        inkMask.enabled = p < 1f;
        inkMask.padding = new Vector4(0f, 0f, Mathf.Max(0f, area.rect.width * (1f - p)), 0f);

        bool scanning = p > 0.001f && p < 0.999f;
        if (scanning && scanCore == null)
        {
            scanGlow = MakeScanImage("ScanGlow", new Color(0.35f, 0.85f, 1f, 0.28f));
            scanCore = MakeScanImage("ScanCore", new Color(0.85f, 1f, 1f, 0.95f));
        }

        if (scanCore == null)
            return;

        scanCore.gameObject.SetActive(scanning);
        scanGlow.gameObject.SetActive(scanning);
        if (!scanning)
            return;

        // The bar spans the card's full height (top edge to bottom edge); only its x follows the ink edge.
        float h = Rect.rect.height - 8f;
        scanCore.rectTransform.sizeDelta = new Vector2(4f, h);
        scanGlow.rectTransform.sizeDelta = new Vector2(46f, h);
        Vector3 world = area.TransformPoint(new Vector3(area.rect.xMin + p * area.rect.width, area.rect.center.y, 0f));
        Vector3 local = Rect.InverseTransformPoint(world);
        Vector2 center = Rect.rect.center;
        scanCore.rectTransform.anchoredPosition = scanGlow.rectTransform.anchoredPosition = new Vector2(local.x, center.y);
        scanCore.transform.SetAsLastSibling();
    }

    public void SetInkBloom(float progress)
    {
        if (inkMask != null) inkMask.enabled=false;
        if (scanCore != null) {scanCore.gameObject.SetActive(false);scanGlow.gameObject.SetActive(false);}
        if(drawingImage == null)return;
        drawingImage.color = new Color(1,1,1,Mathf.Clamp01(progress));
        drawingImage.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.92f,1,Mathf.Clamp01(progress));
    }

    public void SetNameProgress(float p)
    {
        if (nameText == null)
            nameText = GetComponentInChildren<TMP_Text>(true);
        if (nameText == null)
            return;

        nameText.maxVisibleCharacters = p >= 1f ? 99999 : Mathf.CeilToInt(nameText.text.Length * Mathf.Clamp01(p));
    }

    public void ClearReveal()
    {
        if (inkMask != null)
            inkMask.enabled = false;
        if (scanCore != null)
        {
            scanCore.gameObject.SetActive(false);
            scanGlow.gameObject.SetActive(false);
        }

        if (nameText != null)
            nameText.maxVisibleCharacters = 99999;
    }

    private Image MakeScanImage(string objectName, Color color)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(Rect, false);
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.color = color;
        RectTransform r = img.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        go.SetActive(false);
        return img;
    }

    /// <summary>Scale punch that settles back to 1 (used when a card lands).</summary>
    public void Punch(float amount = 0.07f, float duration = 0.45f)
    {
        if (punchRoutine != null)
            StopCoroutine(punchRoutine);
        punchRoutine = StartCoroutine(UITween.Run(duration, t =>
        {
            float wobble = Mathf.Sin(t * Mathf.PI * 2f) * Mathf.Exp(-3.2f * t);
            transform.localScale = Vector3.one * (1f + amount * wobble);
            if (t >= 1f)
                transform.localScale = Vector3.one;
        }, x => x));
    }

    /// <summary>Snap to its grid slot, fully visible (used when the hero card lands here).</summary>
    public void ShowLanded(Vector2 slotPosition, Vector2 size)
    {
        CancelRoutine();
        ReleaseToGrid(slotPosition, size);
        group.alpha = 1f;
        Flash();
        Punch();
    }

    /// <summary>Ease a dimmed card back to normal and re-seat it in the grid.</summary>
    public void RestoreFromDim(Vector2 slotPosition, Vector2 size, float delay)
    {
        CancelRoutine();
        restPosition = slotPosition;
        restoreRoutine = StartCoroutine(RestoreRoutine(slotPosition, size, delay));
    }

    private IEnumerator RestoreRoutine(Vector2 slotPosition, Vector2 size, float delay)
    {
        float target = hasPledge ? 1f : emptyAlpha;
        float fromAlpha = group.alpha;
        float fromScale = transform.localScale.x;
        float fromZ = rt.anchoredPosition3D.z;
        rt.sizeDelta = size;
        yield return UITween.Run(0.6f, t =>
        {
            group.alpha = Mathf.Lerp(fromAlpha, target, Mathf.Clamp01(t));
            transform.localScale = Vector3.one * Mathf.LerpUnclamped(fromScale, 1f, t);
            rt.anchoredPosition3D = new Vector3(slotPosition.x, slotPosition.y, Mathf.Lerp(fromZ, 0f, Mathf.Clamp01(t)));
            if (hasPledge && t > 0.2f && t < 0.3f)
                flash = Mathf.Max(flash, 0.4f);
        }, UITween.OutCubic, delay);

        ReleaseToGrid(slotPosition, size);
        group.alpha = target;
        restoreRoutine = null;
    }

    private void ApplyContent(string visitorName, byte[] drawingBytes)
    {
        highlightUntil=0;
        ClearReveal();
        if (nameText == null)
            nameText = GetComponentInChildren<TMP_Text>(true);
        if (drawingImage == null)
            drawingImage = GetComponentInChildren<RawImage>(true);

        if (nameText != null)
            nameText.text = visitorName ?? string.Empty;

        if (texture != null)
        {
            Destroy(texture);
            texture = null;
        }

        if (drawingImage == null)
            return;

        drawingImage.texture = null;
        drawingImage.enabled = false;

        if (drawingBytes == null || drawingBytes.Length == 0)
            return;

        var loaded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!loaded.LoadImage(drawingBytes))
        {
            Destroy(loaded);
            return;
        }

        RecolourToWhiteInk(loaded);
        loaded.filterMode = FilterMode.Bilinear;
        loaded.wrapMode = TextureWrapMode.Clamp;
        texture = loaded;
        drawingImage.texture = texture;
        drawingImage.color = Color.white;
        drawingImage.enabled = true;
        if (drawingFitter != null)
            drawingFitter.aspectRatio = texture.width / (float)texture.height;
    }

    private static void RecolourToWhiteInk(Texture2D tex)
    {
        Color32[] pixels = tex.GetPixels32();
        bool opaquePaper = pixels.Length > 0 && pixels[0].a > 200;
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 p = pixels[i];
            byte alpha = p.a;
            if (opaquePaper)
            {
                float luminance = (0.299f * p.r + 0.587f * p.g + 0.114f * p.b) / 255f;
                alpha = (byte)(Mathf.Clamp01((0.92f - luminance) * 1.6f) * 255f);
            }

            pixels[i] = new Color32(255, 255, 255, alpha);
        }

        tex.SetPixels32(pixels);
        tex.Apply();
    }
}
