using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One neon card on the wall: the visitor's handwriting rendered as white ink, with their name beneath.
/// Cards live in fixed slots; they animate in/out, bob gently and can flash when a new pledge arrives.
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
    private bool hasPledge;

    public bool HasPledge => hasPledge;

    private void Awake()
    {
        rt = (RectTransform)transform;
        bobPhase = Random.value * 10f;
        if (group == null)
            group = GetComponent<CanvasGroup>();
        ApplyContent(null, null);
        group.alpha = 0f;
    }

    private void OnDestroy()
    {
        if (texture != null)
            Destroy(texture);
    }

    private void Update()
    {
        float time = Time.unscaledTime + bobPhase;
        rt.anchoredPosition = new Vector2(Mathf.Sin(time * 0.55f) * 2f, Mathf.Sin(time * 0.8f) * 5f);

        flash = Mathf.MoveTowards(flash, 0f, Time.unscaledDeltaTime * 1.4f);
        if (glow != null)
        {
            // Slow shimmer on filled cards, bright flare after a new pledge lands.
            float shimmer = hasPledge ? 0.12f + 0.1f * Mathf.Sin(time * 1.3f) : 0f;
            Color c = glow.color;
            c.a = Mathf.Max(shimmer, flash);
            glow.color = c;
        }
    }

    /// <summary>Fills the card without animating (use Show/Hide for transitions).</summary>
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

    /// <summary>Animate in. Filled cards pop at full opacity, empty ones settle at a dim placeholder.</summary>
    public void Reveal(float delay, bool highlight = false)
    {
        Restart(RevealRoutine(delay, highlight));
    }

    public void Conceal(float delay)
    {
        Restart(ConcealRoutine(delay));
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

    private void ApplyContent(string visitorName, byte[] drawingBytes)
    {
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

    /// <summary>
    /// Pledges are drawn as dark ink on a transparent canvas. Show them as white ink: keep alpha, force white.
    /// Older opaque "paper" pledges are converted by treating darkness as ink strength.
    /// </summary>
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
