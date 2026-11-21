using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Coordinates come from Figma's 3840 x 2160 artboards, rendered at 1920 x 1080.
public static class FigmaUIKit
{
    public const string Folder = "Assets/UI/Figma/";
    public const string BackgroundVideoPath = "Assets/Video/Pledge_Wall_BG.mp4";
    public static TMP_FontAsset Font;
    public static Material ScreenGlass;
    public static void Prepare()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder.TrimEnd('/') }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var t = AssetImporter.GetAtPath(path) as TextureImporter;
            if (t == null || !path.EndsWith(".png")) continue;
            if (t.textureType == TextureImporterType.Sprite && t.mipmapEnabled == false && t.maxTextureSize == 4096) continue;
            t.textureType = TextureImporterType.Sprite;
            t.spriteImportMode = SpriteImportMode.Single;
            t.mipmapEnabled = false;
            t.alphaIsTransparency = true;
            t.maxTextureSize = 4096;
            t.textureCompression = TextureImporterCompression.Uncompressed;
            t.wrapMode = TextureWrapMode.Clamp;
            t.SaveAndReimport();
        }
        ScreenGlass=AssetDatabase.LoadAssetAtPath<Material>(Folder+"ScreenGlass.mat");
        if(ScreenGlass==null){ScreenGlass=new Material(Shader.Find("UI/Figma Screen Glass"));AssetDatabase.CreateAsset(ScreenGlass,Folder+"ScreenGlass.mat");}
        Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Folder + "Fonts/Lato SDF.asset");
        if (Font == null)
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>(Folder + "Fonts/Lato-Regular.ttf");
            Font = TMP_FontAsset.CreateFontAsset(source, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024);
            Font.name = "Lato SDF";
            AssetDatabase.CreateAsset(Font, Folder + "Fonts/Lato SDF.asset");
            foreach (var atlas in Font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, Font);
            AssetDatabase.AddObjectToAsset(Font.material, Font);
            Font.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,!?':;@-/&()+");
            EditorUtility.SetDirty(Font);
            AssetDatabase.SaveAssets();
        }
    }
    public static void At(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x / 2f, -y / 2f);
        rt.sizeDelta = new Vector2(w / 2f, h / 2f);
    }
    public static Image Art(Transform parent, string asset, float x, float y, float w, float h)
    {
        var img = CyberUIKit.NewImage(asset, parent, AssetDatabase.LoadAssetAtPath<Sprite>(Folder + asset + ".png"), Color.white);
        At(img.rectTransform, x, y, w, h);
        if(asset.Contains("OriginalGlass") || asset.Contains("OriginalInnerGlow") || asset.Contains("OriginalOuterGlow") || asset.EndsWith("OriginalGlow")) img.material=ScreenGlass;
        return img;
    }
    public static Image GlassGroup(Transform parent,string name,string outer,string glass,Vector4 glassRect,string inner,Vector4 innerRect,float x,float y,float w,float h)
    {
        var image=Art(parent,outer,x,y,w,h);image.name=name;
        string path=Folder+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("UI/Figma Glass Group"));AssetDatabase.CreateAsset(material,path);}
        material.SetTexture("_GlassTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+glass+".png"));
        material.SetVector("_GlassRect",new Vector4(glassRect.x/w,glassRect.y/h,glassRect.z/w,glassRect.w/h));
        material.SetFloat("_InnerEnabled",inner==null?0:1);
        if(inner!=null)
        {
            material.SetTexture("_InnerTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+inner+".png"));
            material.SetVector("_InnerRect",new Vector4(innerRect.x/w,innerRect.y/h,innerRect.z/w,innerRect.w/h));
        }
        image.material=material;EditorUtility.SetDirty(material);return image;
    }
    public static TextMeshProUGUI Text(Transform parent, string name, string value, float x, float y, float w, float h, float size, Color color)
    {
        var text = CyberUIKit.NewText(name, parent, value, size / 2f, color);
        text.font = Font;
        At(text.rectTransform, x, y, w, h);
        return text;
    }
    public static void Base(Canvas canvas)
    {
        Prepare();
        CyberUIKit.ClearChildren(canvas.transform);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080);
        scaler.matchWidthOrHeight = 0.5f;
        var image = canvas.GetComponent<Image>();
        if(image != null) { image.color = new Color(0.024f,0.078f,0.239f); image.raycastTarget=false; }
        Art(canvas.transform,"Draw_imgBackground",0,0,3840,2160);
        ApplyVideoBackground(canvas);
        Brand(canvas.transform);
    }
    public static SceneVideoBackground ApplyVideoBackground(Canvas canvas)
    {
        var clip=AssetDatabase.LoadAssetAtPath<UnityEngine.Video.VideoClip>(BackgroundVideoPath);
        if(clip==null)throw new System.InvalidOperationException("Background video is not imported: "+BackgroundVideoPath);
        Transform existing=canvas.transform.Find("VideoBackground");
        var video=existing!=null?existing.GetComponent<SceneVideoBackground>():null;
        if(video==null)video=CyberUIKit.BuildVideoBackground(canvas.transform);
        // Place the moving background over the still fallback and under all UI.
        var fallback=canvas.transform.Find("Draw_imgBackground");
        if(fallback!=null)fallback.SetAsFirstSibling();
        video.transform.SetSiblingIndex(fallback!=null?1:0);
        var settings=new SerializedObject(video);
        settings.FindProperty("idleClip").objectReferenceValue=clip;
        settings.FindProperty("drawClip").objectReferenceValue=clip;
        settings.ApplyModifiedPropertiesWithoutUndo();
        var player=video.GetComponent<UnityEngine.Video.VideoPlayer>();
        player.clip=clip;player.playOnAwake=false;player.isLooping=true;
        player.renderMode=UnityEngine.Video.VideoRenderMode.APIOnly;
        player.audioOutputMode=UnityEngine.Video.VideoAudioOutputMode.None;
        var image=video.GetComponent<RawImage>();
        image.raycastTarget=false;image.texture=null;image.enabled=false;
        var flow=canvas.GetComponentInChildren<DrawingFlow>(true);
        if(flow!=null)
        {
            var state=new SerializedObject(flow);
            state.FindProperty("videoBackground").objectReferenceValue=video;
            state.ApplyModifiedPropertiesWithoutUndo();
        }
        return video;
    }
    public static void Brand(Transform root)
    {
        // The original vector artwork, rasterized without changing its SVG geometry.
        Art(root,"Draw_imgGroup",1259.39f,134.82f,757.24f,145.38f);
        Art(root,"Draw_imgGroup1",2106.57f,135.16f,474.04f,150.55f);
        Art(root,"Draw_imgVector",2059.10f,100.67f,1.44f,219.20f);
    }
    public static Button Button(Transform parent,string name,string label,float x,float y,float w,float h,bool submit=false)
    {
        Image image=Art(parent, submit?"Draw_imgGroup3":"Register_imgOriginalGlassTexture",x,y,w,h);
        image.name=name;
        image.raycastTarget=true;
        var button=image.gameObject.AddComponent<Button>();
        button.targetGraphic=image;
        var colors=button.colors;
        colors.highlightedColor=new Color(0.82f,0.96f,1f); colors.pressedColor=new Color(0.55f,0.8f,1f);
        button.colors=colors;
        var text=CyberUIKit.NewText("Label",image.transform,label,(submit?52.279f:64.56f)/2f,submit?new Color(0.004f,0.063f,0.36f):Color.white);
        text.font=Font; CyberUIKit.Stretch(text.rectTransform);
        return button;
    }
    public static CyberField Field(Transform parent)
    {
        var field=CyberUIKit.BuildField(parent,"NameInput","Name :",Vector2.zero,60);
        At((RectTransform)field.transform,1040,1003,1760,243);
        field.GetComponent<Image>().sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Folder+"Register_imgGroup2.png");
        var input=field.Input;
        input.textComponent.font=Font; input.textComponent.fontSize=61.496f/2f;
        ((TMP_Text)input.placeholder).font=Font; ((TMP_Text)input.placeholder).fontSize=61.496f/2f;
        CyberUIKit.Stretch(input.textViewport,36.7f,12,36.7f,12);
        return field;
    }
}
