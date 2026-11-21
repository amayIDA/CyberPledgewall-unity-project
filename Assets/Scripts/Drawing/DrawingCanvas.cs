using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum BrushTool
{
    Pen,
    Pencil,
    Marker,
    Brush,
    Eraser
}

/// <summary>
/// Freehand surface. The texture is transparent: ink is stored in the alpha channel so the panel
/// behind it shows through here, and the wall can recolour the same PNG (white ink on navy).
/// The visitor pen uses the soft, speed-tapered brush stroke.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class DrawingCanvas : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    // Matches the Figma writing surface (3:1), so handwriting keeps its shape on the wall.
    public const int TextureWidth = 1200;
    public const int TextureHeight = 400;
    private const int MaxUndo = 20;
    private const float MinSampleSpacing = 1.15f;
    private const float TaperDistance = 28f;

    public BrushTool Tool { get; set; } = BrushTool.Brush;
    public Color BrushColor { get; set; } = new Color(0.04f, 0.04f, 0.04f, 1f);
    public float BrushSize { get; set; } = 7.5f;
    public bool CanUndo => _undo.Count > 0;
    public bool HasInk { get; private set; }

    private RawImage _rawImage;
    private Texture2D _texture;
    private Color32[] _pixels;
    private readonly List<Color32[]> _undo = new List<Color32[]>();
    private readonly List<Vector2> _stroke = new List<Vector2>(128);
    private Color32[] _strokeBase;
    private bool _drawing;

    private void Awake()
    {
        _rawImage = GetComponent<RawImage>();
        _rawImage.color = Color.white;
        _texture = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        _pixels = new Color32[TextureWidth * TextureHeight];
        Upload();
        _rawImage.texture = _texture;
    }

    private void OnDestroy()
    {
        if (_texture != null)
            Destroy(_texture);
    }

    public void Clear(bool resetHistory = false)
    {
        if (resetHistory)
            _undo.Clear();
        else
            PushUndo();

        System.Array.Clear(_pixels, 0, _pixels.Length);
        HasInk = false;
        _stroke.Clear();
        _drawing = false;
        Upload();
    }

    public void Undo()
    {
        if (_undo.Count == 0)
            return;

        _pixels = _undo[_undo.Count - 1];
        _undo.RemoveAt(_undo.Count - 1);
        HasInk = ContainsInk();
        Upload();
    }

    public byte[] EncodeToPng()
    {
        return EncodeForNetwork();
    }

    public byte[] EncodeForNetwork()
    {
        if (_texture == null)
            return System.Array.Empty<byte>();

        byte[] png = _texture.EncodeToPNG();
        const int budget = 160000;
        if (png.Length <= budget)
            return png;

        int w = TextureWidth / 2;
        int h = TextureHeight / 2;
        var small = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
        {
            int srcY = y * 2;
            for (int x = 0; x < w; x++)
            {
                int srcX = x * 2;
                int i = srcY * TextureWidth + srcX;
                small.SetPixel(x, y, _pixels[i]);
            }
        }

        small.Apply();
        png = small.EncodeToPNG();
        Object.Destroy(small);
        return png;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!TryGetPosition(eventData, out Vector2 pos))
            return;

        PushUndo();
        _strokeBase = (Color32[])_pixels.Clone();
        _stroke.Clear();
        _stroke.Add(pos);
        _drawing = true;
        RasterizeStroke(false);
        Upload();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_drawing)
            return;

        if (!TryGetPosition(eventData, out Vector2 pos))
            return;

        Vector2 last = _stroke[_stroke.Count - 1];
        if ((pos - last).sqrMagnitude < MinSampleSpacing * MinSampleSpacing)
            return;

        _stroke.Add(pos);
        RasterizeStroke(false);
        Upload();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_drawing)
            return;

        if (TryGetPosition(eventData, out Vector2 pos))
        {
            Vector2 last = _stroke[_stroke.Count - 1];
            if ((pos - last).sqrMagnitude > 0.25f)
                _stroke.Add(pos);
        }

        RasterizeStroke(true);
        Upload();
        _stroke.Clear();
        _strokeBase = null;
        _drawing = false;
    }

    private void Upload()
    {
        _texture.SetPixels32(_pixels);
        _texture.Apply();
    }

    private bool ContainsInk()
    {
        for (int i = 0; i < _pixels.Length; i++)
            if (_pixels[i].a > 8)
                return true;
        return false;
    }

    private void PushUndo()
    {
        if (_undo.Count >= MaxUndo)
            _undo.RemoveAt(0);
        _undo.Add((Color32[])_pixels.Clone());
    }

    private bool TryGetPosition(PointerEventData eventData, out Vector2 pos)
    {
        pos = default;
        RectTransform rt = _rawImage.rectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, eventData.position, eventData.pressEventCamera, out Vector2 local))
            return false;

        Rect rect = rt.rect;
        float nx = (local.x - rect.x) / rect.width;
        float ny = (local.y - rect.y) / rect.height;
        if (nx < 0f || nx > 1f || ny < 0f || ny > 1f)
            return false;

        pos = new Vector2(nx * TextureWidth, ny * TextureHeight);
        return true;
    }

    private void RasterizeStroke(bool finishing)
    {
        if (_strokeBase != null)
            System.Array.Copy(_strokeBase, _pixels, _pixels.Length);

        if (_stroke.Count == 0)
            return;

        GetToolParams(out float baseRadius, out float hardness, out float opacity, out bool grain, out Color32 color);

        float length = StrokeLength();
        float traveled = 0f;
        Vector2 prev = _stroke[0];
        Stamp(prev, RadiusAt(0f, 0f, length, finishing, baseRadius), hardness, opacity, grain, color);

        for (int i = 1; i < _stroke.Count; i++)
        {
            Vector2 a = _stroke[i - 1];
            Vector2 b = _stroke[i];
            Vector2 c0 = i - 2 >= 0 ? _stroke[i - 2] : a;
            Vector2 c1 = i + 1 < _stroke.Count ? _stroke[i + 1] : b;
            float seg = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(seg / 0.7f));
            for (int s = 1; s <= steps; s++)
            {
                float u = s / (float)steps;
                Vector2 p = CatmullRom(c0, a, b, c1, u);
                float speed = Vector2.Distance(prev, p);
                traveled += speed;
                Stamp(p, RadiusAt(traveled, speed, length, finishing, baseRadius), hardness, opacity, grain, color);
                prev = p;
            }
        }

        if (Tool != BrushTool.Eraser)
            HasInk = true;
    }

    private float StrokeLength()
    {
        float length = 0f;
        for (int i = 1; i < _stroke.Count; i++)
            length += Vector2.Distance(_stroke[i - 1], _stroke[i]);
        return Mathf.Max(length, 0.001f);
    }

    private float RadiusAt(float traveled, float speed, float length, bool finishing, float baseRadius)
    {
        float startTaper = Mathf.SmoothStep(0.18f, 1f, Mathf.Clamp01(traveled / TaperDistance));
        float brushEnd = 1f;
        if (finishing)
            brushEnd = Mathf.SmoothStep(0.12f, 1f, Mathf.Clamp01((length - traveled) / TaperDistance));

        float speedTaper = Mathf.Lerp(1.18f, 0.42f, Mathf.Clamp01(speed / 16f));
        float shortStroke = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(length / 40f));
        return Mathf.Max(0.65f, baseRadius * startTaper * brushEnd * speedTaper * shortStroke);
    }

    private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private void GetToolParams(out float radius, out float hardness, out float opacity, out bool grain, out Color32 color)
    {
        color = BrushColor;
        grain = false;
        switch (Tool)
        {
            case BrushTool.Pencil:
                radius = BrushSize * 0.5f; hardness = 0.55f; opacity = 0.78f; grain = true; break;
            case BrushTool.Marker:
                radius = BrushSize * 1.8f; hardness = 0.92f; opacity = 0.88f; break;
            case BrushTool.Brush:
                radius = BrushSize * 1.65f; hardness = 0.22f; opacity = 0.42f; break;
            case BrushTool.Eraser:
                radius = BrushSize * 2.5f; hardness = 0.85f; opacity = 1f; break;
            default:
                radius = BrushSize; hardness = 0.22f; opacity = 0.42f; break;
        }

        radius = Mathf.Max(0.8f, radius);
    }

    private void Stamp(Vector2 center, float radius, float hardness, float opacity, bool grain, Color32 color)
    {
        hardness = Mathf.Min(hardness, 0.99f);
        bool erase = Tool == BrushTool.Eraser;
        int x0 = Mathf.Max(0, Mathf.FloorToInt(center.x - radius - 1f));
        int x1 = Mathf.Min(TextureWidth - 1, Mathf.CeilToInt(center.x + radius + 1f));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(center.y - radius - 1f));
        int y1 = Mathf.Min(TextureHeight - 1, Mathf.CeilToInt(center.y + radius + 1f));

        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                float dx = x + 0.5f - center.x;
                float dy = y + 0.5f - center.y;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / radius;
                if (d > 1f)
                    continue;

                float coverage = d <= hardness ? 1f : (1f - d) / (1f - hardness);
                coverage *= coverage;
                float a = coverage * opacity;
                if (grain)
                    a *= 0.55f + 0.45f * Hash(x, y);

                int index = y * TextureWidth + x;
                Color32 p = _pixels[index];

                if (erase)
                {
                    p.a = (byte)(p.a * (1f - a));
                }
                else
                {
                    float existing = p.a / 255f;
                    float outAlpha = a + existing * (1f - a);
                    if (outAlpha > 0.0001f)
                    {
                        float keep = existing * (1f - a);
                        p.r = (byte)((color.r * a + p.r * keep) / outAlpha);
                        p.g = (byte)((color.g * a + p.g * keep) / outAlpha);
                        p.b = (byte)((color.b * a + p.b * keep) / outAlpha);
                    }
                    p.a = (byte)Mathf.Clamp(outAlpha * 255f, 0f, 255f);
                }

                _pixels[index] = p;
            }
        }
    }

    private static float Hash(int x, int y)
    {
        float s = Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f;
        return s - Mathf.Floor(s);
    }
}
