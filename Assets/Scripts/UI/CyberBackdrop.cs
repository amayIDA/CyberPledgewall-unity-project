using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Animated key-art backdrop: the tunnel plate breathes, the floating key-art layer drifts on a
/// different rhythm (parallax), drifting light motes twinkle, and the title layer can fade between
/// "hero" (welcome) and "ghost" (all other screens).
/// </summary>
public class CyberBackdrop : MonoBehaviour
{
    [SerializeField] private RectTransform plate;
    [SerializeField] private RectTransform keyArt;
    [SerializeField] private Image keyArtImage;
    [SerializeField] private RectTransform moteLayer;
    [SerializeField] private Sprite moteSprite;
    [SerializeField] private int moteCount = 30;
    [SerializeField] private float ghostAlpha = 0.1f;
    [Tooltip("Wall display: show the title only as a faint watermark from the start.")]
    [SerializeField] private bool startAsGhost;

    private struct Mote
    {
        public RectTransform rt;
        public Image image;
        public Vector2 velocity;
        public float phase;
        public float speed;
        public float baseAlpha;
        public float life;      // < 0 for ambient motes, otherwise seconds remaining for burst particles
        public float lifeTotal;
    }

    private const float PlateBaseScale = 1.04f;

    private Mote[] motes;
    private Coroutine keyArtRoutine;
    private Coroutine introRoutine;
    private static readonly Color Cyan = new Color(0.45f, 0.85f, 1f);
    private static readonly Color Gold = new Color(1f, 0.82f, 0.35f);

    private void Awake()
    {
        BuildMotes();
    }

    private void OnEnable()
    {
        introRoutine = StartCoroutine(Intro());
    }

    private void Start()
    {
        if (startAsGhost)
            SetTitleHero(false, 0.01f);
    }

    private IEnumerator Intro()
    {
        if (plate == null)
            yield break;

        var group = plate.GetComponent<CanvasGroup>();
        if (group != null)
            group.alpha = 0f;
        plate.localScale = Vector3.one * 1.16f;

        yield return UITween.Run(1.6f, t =>
        {
            plate.localScale = Vector3.one * Mathf.Lerp(1.16f, PlateBaseScale, t);
            if (group != null)
                group.alpha = t;
        });

        introRoutine = null;
    }

    private void Update()
    {
        float time = Time.unscaledTime;

        if (plate != null && introRoutine == null)
            plate.localScale = Vector3.one * (PlateBaseScale + 0.018f * Mathf.Sin(time * 0.45f));

        // Scales stay above 1 so the drift never exposes the canvas edge.
        if (keyArt != null)
        {
            keyArt.anchoredPosition = new Vector2(Mathf.Sin(time * 0.31f) * 10f, Mathf.Sin(time * 0.52f) * 7f);
            keyArt.localScale = Vector3.one * (1.035f + 0.012f * Mathf.Sin(time * 0.7f));
        }

        UpdateMotes(time, Time.unscaledDeltaTime);
    }

    /// <summary>Hero = full title key art (welcome). Ghost = faint watermark behind forms and panels.</summary>
    public void SetTitleHero(bool hero, float duration = 0.8f)
    {
        if (keyArtImage == null)
            return;

        keyArtRoutine = UITween.Stop(this, keyArtRoutine);
        float from = keyArtImage.color.a;
        float to = hero ? 1f : ghostAlpha;
        keyArtRoutine = StartCoroutine(UITween.Run(duration, t =>
        {
            Color c = keyArtImage.color;
            c.a = Mathf.Lerp(from, to, t);
            keyArtImage.color = c;
        }, UITween.InOutCubic));
    }

    /// <summary>Radial sparkle burst, used for the thank-you celebration.</summary>
    public void Burst(Vector2 anchoredPosition, int count = 46)
    {
        if (moteLayer == null || moteSprite == null)
            return;

        for (int i = 0; i < count; i++)
        {
            float angle = Random.value * Mathf.PI * 2f;
            float speed = Random.Range(220f, 820f);
            float size = Random.Range(10f, 30f);
            CreateMote(anchoredPosition, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed, size,
                Random.value < 0.55f ? Gold : Cyan, 0.95f, Random.Range(0.9f, 1.7f));
        }
    }

    private void BuildMotes()
    {
        if (moteLayer == null || moteSprite == null)
            return;

        motes = new Mote[moteCount + 128];
        for (int i = 0; i < moteCount; i++)
        {
            Vector2 pos = new Vector2(Random.Range(-980f, 980f), Random.Range(-560f, 560f));
            Vector2 vel = new Vector2(Random.Range(-6f, 6f), Random.Range(10f, 34f));
            CreateMote(pos, vel, Random.Range(5f, 16f), Random.value < 0.35f ? Gold : Cyan, Random.Range(0.25f, 0.6f), -1f);
        }
    }

    private void CreateMote(Vector2 pos, Vector2 velocity, float size, Color color, float alpha, float life)
    {
        int slot = -1;
        for (int i = 0; i < motes.Length; i++)
        {
            if (motes[i].rt == null)
            {
                slot = i;
                break;
            }
            if (motes[i].life == 0f && motes[i].lifeTotal > 0f)
            {
                slot = i;
                break;
            }
        }

        if (slot < 0)
            return;

        Mote m = motes[slot];
        if (m.rt == null)
        {
            var go = new GameObject("Mote", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(moteLayer, false);
            m.rt = (RectTransform)go.transform;
            m.image = go.GetComponent<Image>();
            m.image.sprite = moteSprite;
            m.image.raycastTarget = false;
        }

        m.rt.gameObject.SetActive(true);
        m.rt.anchoredPosition = pos;
        m.rt.sizeDelta = Vector2.one * size;
        m.velocity = velocity;
        m.phase = Random.value * 10f;
        m.speed = Random.Range(0.6f, 1.8f);
        m.baseAlpha = alpha;
        m.life = life;
        m.lifeTotal = life < 0f ? 0f : life;
        color.a = alpha;
        m.image.color = color;
        motes[slot] = m;
    }

    private void UpdateMotes(float time, float dt)
    {
        if (motes == null)
            return;

        for (int i = 0; i < motes.Length; i++)
        {
            Mote m = motes[i];
            if (m.rt == null || !m.rt.gameObject.activeSelf)
                continue;

            Vector2 pos = m.rt.anchoredPosition;
            Color c = m.image.color;

            if (m.life < 0f)
            {
                pos += m.velocity * dt;
                pos.x += Mathf.Sin(time * 0.4f + m.phase) * 8f * dt;
                if (pos.y > 580f)
                    pos = new Vector2(Random.Range(-980f, 980f), -580f);
                c.a = m.baseAlpha * (0.45f + 0.55f * Mathf.Sin(time * m.speed + m.phase));
            }
            else
            {
                m.life -= dt;
                float t = 1f - Mathf.Clamp01(m.life / m.lifeTotal);
                m.velocity *= Mathf.Exp(-2.2f * dt);
                pos += m.velocity * dt;
                c.a = m.baseAlpha * (1f - t) * (0.7f + 0.3f * Mathf.Sin(time * 30f + m.phase));
                m.rt.localScale = Vector3.one * Mathf.Lerp(1.2f, 0.2f, t);
                if (m.life <= 0f)
                {
                    m.life = 0f;
                    m.rt.gameObject.SetActive(false);
                }
            }

            m.rt.anchoredPosition = pos;
            m.image.color = c;
            motes[i] = m;
        }
    }
}
