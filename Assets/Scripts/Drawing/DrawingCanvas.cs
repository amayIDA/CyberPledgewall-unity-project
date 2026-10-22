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
/// </summary>
[RequireComponent(typeof(RawImage))]
public class DrawingCanvas : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    // Matches the ~2.1:1 paper area of the design. PNG output must stay well below NetworkSession.DefaultMaxPayloadSize.
    public const int TextureWidth = 960;
    public const int TextureHeight = 448;
    private const int MaxUndo = 20;

    public BrushTool Tool { get; set; } = BrushTool.Pen;
    public Color BrushColor { get; set; } = new Color(0.04f, 0.04f, 0.04f, 1f);
    /// <summary>Base brush radius in texture pixels; each tool scales it.</summary>
    public float BrushSize { get; set; } = 6f;
    public bool CanUndo => _undo.Count > 0;
    public bool HasInk { get; private set; }

    private RawImage _rawImage;
    private Texture2D _texture;
    private Color32[] _pixels;
    private readonly List<Color32[]> _undo = new List<Color32[]>();
    private Vector2? _last;

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
        return _texture == null ? System.Array.Empty<byte>() : _texture.EncodeToPNG();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!TryGetPosition(eventData, out Vector2 pos))
            return;

        PushUndo();
        _last = pos;
        StampAlong(pos, pos);
        Upload();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!TryGetPosition(eventData, out Vector2 pos))
        {
            _last = null;
            return;
        }

        if (!_last.HasValue)
            PushUndo();

        StampAlong(_last ?? pos, pos);
        _last = pos;
        Upload();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _last = null;
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

    private void StampAlong(Vector2 from, Vector2 to)
    {
        GetToolParams(out float radius, out float hardness, out float opacity, out bool grain, out Color32 color);
        float step = Mathf.Max(0.75f, radius * 0.2f);
        float distance = Vector2.Distance(from, to);
        int count = Mathf.Max(1, Mathf.CeilToInt(distance / step));
        for (int i = 0; i <= count; i++)
            Stamp(Vector2.Lerp(from, to, i / (float)count), radius, hardness, opacity, grain, color);

        if (Tool != BrushTool.Eraser)
            HasInk = true;
    }

    private void GetToolParams(out float radius, out float hardness, out float opacity, out bool grain, out Color32 color)
    {
        color = BrushColor;
        grain = false;
        switch (Tool)
        {
            case BrushTool.Pencil:
                radius = BrushSize * 0.5f; hardness = 0.6f; opacity = 0.8f; grain = true; break;
            case BrushTool.Marker:
                radius = BrushSize * 1.8f; hardness = 1f; opacity = 0.9f; break;
            case BrushTool.Brush:
                radius = BrushSize * 1.5f; hardness = 0.15f; opacity = 0.18f; break;
            case BrushTool.Eraser:
                radius = BrushSize * 2.5f; hardness = 0.85f; opacity = 1f; break;
            default:
                radius = BrushSize; hardness = 0.9f; opacity = 1f; break;
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
