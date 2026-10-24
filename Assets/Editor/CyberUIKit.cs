using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>Shared sprite generation and UI construction helpers for the Drawing and Wall scene builders.</summary>
public static class CyberUIKit
{
    public const string SpriteFolder = "Assets/UI/Sprites";
    public const string BackgroundPath = "Assets/UI/Group 14.png";
    public const string KeyArtPath = "Assets/UI/CYBERCARNIVAL_KV4C_BG_Element 1.png";
    public const string ButtonPath = "Assets/UI/Group 8.png";
    public const string LogoPath = "Assets/UI/image 1.png";
    public const string FieldPath = "Assets/UI/Registration place holder.png";
    public const string IdleVideoPath = "Assets/Video/BG_Idle.mp4";
    public const string DrawVideoPath = "Assets/Video/CYBER_CARNIVAL_Background_4K_BG.mp4";

    public static readonly Color Cyan = new Color(0.30f, 0.86f, 1f, 1f);
    public static readonly Color NavyFallback = new Color(0.01f, 0.04f, 0.22f, 1f);

    public static Sprite Background, KeyArt, ButtonArt, Logo, Field;
    public static Sprite NeonFrame, NeonOutline, RoundedTop, RoundedBottom, RoundedAll, SoftDot;

    // ---- assets ------------------------------------------------------------------------------

    public static void EnsureAssets()
    {
        if (!AssetDatabase.IsValidFolder("Assets/UI"))
            AssetDatabase.CreateFolder("Assets", "UI");
        if (!AssetDatabase.IsValidFolder(SpriteFolder))
            AssetDatabase.CreateFolder("Assets/UI", "Sprites");

        Background = ConfigureImported(BackgroundPath);
        KeyArt = ConfigureImported(KeyArtPath);
        ButtonArt = ConfigureImported(ButtonPath);
        Logo = ConfigureImported(LogoPath);
        Field = ConfigureImported(FieldPath);

        NeonFrame = Generated("NeonFrame", 256, 80, (x, y) => NeonPixel(x, y, true));
        NeonOutline = Generated("NeonOutline", 256, 80, (x, y) => NeonPixel(x, y, false));
        RoundedTop = Generated("RoundedTop", 96, 32, (x, y) => RoundedPixel(x, y, 96, 28f, 0f));
        RoundedBottom = Generated("RoundedBottom", 96, 32, (x, y) => RoundedPixel(x, y, 96, 0f, 28f));
        RoundedAll = Generated("RoundedAll", 96, 32, (x, y) => RoundedPixel(x, y, 96, 28f, 28f));
        SoftDot = Generated("SoftDot", 64, 0, SoftDotPixel);
    }

    private static Sprite ConfigureImported(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);

        bool dirty = false;
        if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
        if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; dirty = true; }
        if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
        if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; dirty = true; }
        if (importer.maxTextureSize < 4096) { importer.maxTextureSize = 4096; dirty = true; }
        if (importer.wrapMode != TextureWrapMode.Clamp) { importer.wrapMode = TextureWrapMode.Clamp; dirty = true; }
        if (dirty)
            importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private delegate Color32 PixelFunc(int x, int y);

    private static Sprite Generated(string name, int size, int border, PixelFunc pixel)
    {
        string path = SpriteFolder + "/" + name + ".png";
        if (!File.Exists(path))
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = pixel(x, y);
            tex.SetPixels32(pixels);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
        }

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spriteBorder = new Vector4(border, border, border, border);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ---- procedural pixels -------------------------------------------------------------------

    private static float RoundRectDistance(float px, float py, float hx, float hy, float r)
    {
        float qx = Mathf.Abs(px) - (hx - r);
        float qy = Mathf.Abs(py) - (hy - r);
        float ox = Mathf.Max(qx, 0f);
        float oy = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    // Premultiplied "over": result = top + bottom * (1 - top.a)
    private static void Over(ref Vector4 dst, Color color, float alpha)
    {
        alpha = Mathf.Clamp01(alpha);
        float inv = 1f - alpha;
        dst.x = color.r * alpha + dst.x * inv;
        dst.y = color.g * alpha + dst.y * inv;
        dst.z = color.b * alpha + dst.z * inv;
        dst.w = alpha + dst.w * inv;
    }

    private static Color32 ToStraight(Vector4 p)
    {
        if (p.w <= 0.0001f)
            return new Color32(0, 0, 0, 0);
        return new Color32(
            (byte)(Mathf.Clamp01(p.x / p.w) * 255f),
            (byte)(Mathf.Clamp01(p.y / p.w) * 255f),
            (byte)(Mathf.Clamp01(p.z / p.w) * 255f),
            (byte)(Mathf.Clamp01(p.w) * 255f));
    }

    private static Color32 NeonPixel(int x, int y, bool filled)
    {
        const float size = 256f;
        float px = x + 0.5f - size * 0.5f;
        float py = y + 0.5f - size * 0.5f;
        float half = size * 0.5f - 14f;

        float d = RoundRectDistance(px, py, half, half, 58f);
        float d2 = RoundRectDistance(px, py, half - 15f, half - 15f, 43f);
        float vertical = Mathf.SmoothStep(0.05f, 0.95f, (y + 0.5f) / size);   // 0 = bottom, 1 = top

        var p = Vector4.zero;
        var cyan = new Color(0.22f, 0.82f, 1f);

        // outer glow
        if (d > 0f)
            Over(ref p, cyan, Mathf.Exp(-d / 4.2f) * (filled ? 0.65f : 0.9f));

        if (filled)
        {
            Color top = new Color(0.01f, 0.04f, 0.30f);
            Color bottom = new Color(0.12f, 0.30f, 0.95f);
            Over(ref p, Color.Lerp(bottom, top, vertical), Mathf.Clamp01(0.5f - d) * 0.94f);

            // soft inner glow hugging the edge
            if (d < 0f)
                Over(ref p, cyan, Mathf.Exp(d / 7f) * 0.32f * Mathf.Clamp01(0.5f - d));
        }

        // bright outer stroke + thin inner line
        Over(ref p, new Color(0.30f, 0.88f, 1f), Mathf.Clamp01(2.9f - Mathf.Abs(d + 2f)));
        Over(ref p, new Color(0.62f, 0.92f, 1f), Mathf.Clamp01(1.5f - Mathf.Abs(d2)) * 0.85f);
        return ToStraight(p);
    }

    private static Color32 RoundedPixel(int x, int y, int size, float topRadius, float bottomRadius)
    {
        float half = size * 0.5f;
        float px = x + 0.5f - half;
        float py = y + 0.5f - half;
        float r = Mathf.Max(0.5f, py > 0f ? topRadius : bottomRadius);
        float d = RoundRectDistance(px, py, half, half, r);
        return new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255f));
    }

    private static Color32 SoftDotPixel(int x, int y)
    {
        float dx = (x + 0.5f - 32f) / 32f;
        float dy = (y + 0.5f - 32f) / 32f;
        float d = Mathf.Sqrt(dx * dx + dy * dy);
        float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f);
        return new Color32(255, 255, 255, (byte)(a * 255f));
    }

    // ---- construction helpers ----------------------------------------------------------------

    public static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static Image NewImage(string name, Transform parent, Sprite sprite, Color color, bool sliced = false, bool raycast = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    public static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color, FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    public static void Place(RectTransform rt, Vector2 anchoredPosition, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = size;
    }

    public static void Stretch(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    public static void ClearChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(root.GetChild(i).gameObject);
    }

    public static void SetArray<T>(SerializedProperty array, T[] items) where T : Object
    {
        array.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    /// <summary>Key-art plate + floating key-art layer + light motes, animated by CyberBackdrop.</summary>
    public static CyberBackdrop BuildBackdrop(Transform root)
    {
        RectTransform holder = NewRect("Backdrop", root);
        Stretch(holder);

        Image plate = NewImage("Plate", holder, Background, Color.white);
        FillParent(plate.rectTransform, Background);
        plate.gameObject.AddComponent<CanvasGroup>();

        Image keyArt = NewImage("KeyArt", holder, KeyArt, Color.white);
        FillParent(keyArt.rectTransform, KeyArt);

        RectTransform motes = NewRect("Motes", holder);
        Stretch(motes);

        var backdrop = holder.gameObject.AddComponent<CyberBackdrop>();
        var so = new SerializedObject(backdrop);
        so.FindProperty("plate").objectReferenceValue = plate.rectTransform;
        so.FindProperty("keyArt").objectReferenceValue = keyArt.rectTransform;
        so.FindProperty("keyArtImage").objectReferenceValue = keyArt;
        so.FindProperty("moteLayer").objectReferenceValue = motes;
        so.FindProperty("moteSprite").objectReferenceValue = SoftDot;
        so.ApplyModifiedPropertiesWithoutUndo();
        return backdrop;
    }

    public static SceneVideoBackground BuildVideoBackground(Transform root)
    {
        RectTransform holder = NewRect("VideoBackground", root);
        Stretch(holder);
        holder.SetAsFirstSibling();

        var image = holder.gameObject.AddComponent<RawImage>();
        image.color = Color.white;
        image.raycastTarget = false;

        var fitter = holder.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = 16f / 9f;

        holder.gameObject.AddComponent<VideoPlayer>();
        var video = holder.gameObject.AddComponent<SceneVideoBackground>();
        var so = new SerializedObject(video);
        so.FindProperty("idleClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<VideoClip>(IdleVideoPath);
        so.FindProperty("drawClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<VideoClip>(DrawVideoPath);
        so.ApplyModifiedPropertiesWithoutUndo();
        return video;
    }

    private static void FillParent(RectTransform rt, Sprite sprite)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(1920f, 1080f);
        var fitter = rt.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
    }

    /// <summary>
    /// Neon panel layers: a gradient fill (so it stretches cleanly at any size) under the glowing double outline.
    /// The root rect must be the visible size plus the sprite's glow margin (14px / frameMultiplier per side).
    /// </summary>
    public static void AddNeonLayers(RectTransform root, float frameMultiplier, Color top, Color bottom, float bias, float fillAlpha = 0.92f)
    {
        float margin = 14f / frameMultiplier + 1f;

        Image fill = NewImage("Fill", root, RoundedAll, new Color(1f, 1f, 1f, fillAlpha), true);
        fill.pixelsPerUnitMultiplier = 28f * frameMultiplier / 58f;
        Stretch(fill.rectTransform, margin, margin, margin, margin);
        fill.gameObject.AddComponent<UIGradient>().Set(top, bottom, bias);
        fill.transform.SetAsFirstSibling();

        Image outline = NewImage("Outline", root, NeonOutline, Color.white, true);
        outline.pixelsPerUnitMultiplier = frameMultiplier;
        Stretch(outline.rectTransform);
        outline.transform.SetSiblingIndex(1);
    }

    public static Image BuildLogo(Transform root, Vector2 position, float width)
    {
        Image logo = NewImage("Logo", root, Logo, Color.white);
        logo.preserveAspect = true;
        Place(logo.rectTransform, position, new Vector2(width, width * Logo.rect.height / Logo.rect.width));
        return logo;
    }

    /// <summary>Hologram button: transparent hit area + glow ring + face + label.</summary>
    public static Button BuildButton(Transform parent, string name, string label, float fontSize, Vector2 position)
    {
        var size = new Vector2(442f, 132f);
        var root = NewImage(name, parent, null, new Color(1f, 1f, 1f, 0f), false, true);
        Place(root.rectTransform, position, size);

        var button = root.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = root;

        Image glow = NewImage("Glow", root.transform, ButtonSprite(), new Color(0.45f, 0.9f, 1f, 0f));
        Stretch(glow.rectTransform);
        Image face = NewImage("Face", root.transform, ButtonSprite(), Color.white);
        Stretch(face.rectTransform);

        TextMeshProUGUI text = NewText("Label", root.transform, label, fontSize, Color.white, FontStyles.Normal);
        Stretch(text.rectTransform);

        var cyber = root.gameObject.AddComponent<CyberButton>();
        var so = new SerializedObject(cyber);
        so.FindProperty("glowRing").objectReferenceValue = glow;
        so.ApplyModifiedPropertiesWithoutUndo();
        return button;
    }

    private static Sprite ButtonSprite() => ButtonArt;

    /// <summary>Pill-shaped text button used for the small Undo / Clear controls.</summary>
    public static Button BuildChipButton(Transform parent, string name, string label, Vector2 anchoredPosition, Vector2 size)
    {
        Image chip = NewImage(name, parent, RoundedAll, new Color(0.04f, 0.16f, 0.45f, 0.18f), true, true);
        chip.pixelsPerUnitMultiplier = 2.2f;
        chip.rectTransform.anchorMin = chip.rectTransform.anchorMax = new Vector2(1f, 0f);
        chip.rectTransform.pivot = new Vector2(1f, 0f);
        chip.rectTransform.anchoredPosition = anchoredPosition;
        chip.rectTransform.sizeDelta = size;

        var button = chip.gameObject.AddComponent<Button>();
        button.targetGraphic = chip;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
        colors.pressedColor = new Color(0.8f, 0.85f, 1f, 1f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        TextMeshProUGUI text = NewText("Label", chip.transform, label, 24, new Color(0.05f, 0.18f, 0.45f, 1f), FontStyles.Bold);
        Stretch(text.rectTransform);
        return button;
    }

    /// <summary>Registration field built from the supplied artwork.</summary>
    public static CyberField BuildField(Transform parent, string name, string placeholder, Vector2 position, int characterLimit)
    {
        var resources = new TMP_DefaultControls.Resources();
        GameObject go = TMP_DefaultControls.CreateInputField(resources);
        go.name = name;
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        Place(rt, position, new Vector2(1004f, 158f));

        var image = go.GetComponent<Image>();
        image.sprite = Field;
        image.type = Image.Type.Simple;
        image.color = Color.white;

        var input = go.GetComponent<TMP_InputField>();
        input.characterLimit = characterLimit;
        input.caretColor = Color.white;
        input.caretWidth = 3;
        input.customCaretColor = true;
        input.selectionColor = new Color(0.3f, 0.8f, 1f, 0.45f);
        input.transition = Selectable.Transition.None;

        var area = (RectTransform)go.transform.Find("Text Area");
        Stretch(area, 40f, 40f, 40f, 10f);

        var text = input.textComponent as TextMeshProUGUI;
        text.fontSize = 40;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft;

        var ph = input.placeholder as TextMeshProUGUI;
        ph.text = placeholder;
        ph.fontSize = 40;
        ph.fontStyle = FontStyles.Normal;
        ph.color = Color.white;
        ph.alignment = TextAlignmentOptions.MidlineLeft;

        var field = go.AddComponent<CyberField>();
        var so = new SerializedObject(field);
        so.FindProperty("frame").objectReferenceValue = image;
        so.ApplyModifiedPropertiesWithoutUndo();
        return field;
    }
}
