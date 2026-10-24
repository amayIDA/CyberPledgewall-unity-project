using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Builds the wall display: key-art backdrop and a 4x3 neon grid of up to 12 pledge cards.</summary>
public static class WallUIBuilder
{
    private const string WallScenePath = "Assets/Scenes/WallScene.unity";
    private const string PledgeCardPrefabPath = "Assets/Prefabs/UI/PledgeCard.prefab";

    // Card sprite frames are drawn with a 14px glow margin; the multiplier shrinks the 80px corners to the design's radius.
    private const float CornerMultiplier = 1.7f;
    private const float Margin = 14f / CornerMultiplier;
    private static readonly Vector2 CardVisible = new Vector2(500f, 290f);

    [MenuItem("Cyber Pledge Wall/Rebuild Wall UI")]
    public static void RebuildInWallScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.OpenScene(WallScenePath, OpenSceneMode.Single);
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("WallScene has no Canvas.");
            return;
        }

        Build(canvas);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Wall UI rebuilt and saved to WallScene.");
    }

    public static void Build(Canvas canvas)
    {
        CyberUIKit.EnsureAssets();
        GameObject cardPrefab = CreatePledgeCardPrefab();

        Transform root = canvas.transform;
        CyberUIKit.ClearChildren(root);

        var rootImage = canvas.GetComponent<Image>();
        if (rootImage != null)
            rootImage.color = CyberUIKit.NavyFallback;

        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        CyberBackdrop backdrop = CyberUIKit.BuildBackdrop(root);
        backdrop.gameObject.SetActive(false);
        SceneVideoBackground video = CyberUIKit.BuildVideoBackground(root);
        var videoSo = new SerializedObject(video);
        var carnival = AssetDatabase.LoadAssetAtPath<UnityEngine.Video.VideoClip>(CyberUIKit.DrawVideoPath);
        if (carnival != null)
        {
            videoSo.FindProperty("idleClip").objectReferenceValue = carnival;
            videoSo.FindProperty("drawClip").objectReferenceValue = carnival;
            videoSo.ApplyModifiedPropertiesWithoutUndo();
        }

        RectTransform grid = CyberUIKit.NewRect("PledgeGrid", root);
        CyberUIKit.Stretch(grid);

        float[] columns = { -705f, -235f, 235f, 705f };
        float[] rows = { 300f, 0f, -300f };
        var cards = new PledgeCard[12];
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 4; col++)
            {
                int index = row * 4 + col;
                RectTransform slot = CyberUIKit.NewRect("Slot" + (index + 1), grid);
                CyberUIKit.Place(slot, new Vector2(columns[col], rows[row]), CardVisible + Vector2.one * (Margin * 2f));

                var card = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefab, slot);
                card.name = "PledgeCard";
                CyberUIKit.Stretch((RectTransform)card.transform);
                cards[index] = card.GetComponent<PledgeCard>();
            }
        }

        var boardGo = new GameObject("WallBoard", typeof(RectTransform));
        boardGo.transform.SetParent(grid, false);
        var board = boardGo.AddComponent<WallBoard>();
        var boardSo = new SerializedObject(board);
        CyberUIKit.SetArray(boardSo.FindProperty("cards"), cards);
        boardSo.ApplyModifiedPropertiesWithoutUndo();

        TextMeshProUGUI status = CyberUIKit.NewText("WallStatusText", root, string.Empty, 20f, new Color(1f, 1f, 1f, 0.45f));
        CyberUIKit.Place(status.rectTransform, new Vector2(0f, -516f), new Vector2(1400f, 30f));

        var manager = Object.FindAnyObjectByType<WallManager>();
        if (manager != null)
        {
            var so = new SerializedObject(manager);
            so.FindProperty("board").objectReferenceValue = board;
            so.FindProperty("statusText").objectReferenceValue = status;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    public static GameObject CreatePledgeCardPrefab()
    {
        CyberUIKit.EnsureAssets();
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
            AssetDatabase.CreateFolder("Assets/Prefabs", "UI");

        var card = new GameObject("PledgeCard", typeof(RectTransform), typeof(CanvasGroup), typeof(PledgeCard));
        var cardRt = (RectTransform)card.transform;
        cardRt.sizeDelta = CardVisible + Vector2.one * (Margin * 2f);

        CyberUIKit.AddNeonLayers(cardRt, CornerMultiplier, new Color(0f, 0.05f, 0.25f), new Color(0.06f, 0.26f, 0.82f), 2.6f);

        // Edge flare that lights up when a new pledge lands.
        Image glow = CyberUIKit.NewImage("Glow", card.transform, CyberUIKit.NeonOutline, new Color(0.6f, 0.95f, 1f, 0f), true);
        glow.pixelsPerUnitMultiplier = CornerMultiplier;
        CyberUIKit.Stretch(glow.rectTransform);

        RectTransform area = CyberUIKit.NewRect("DrawingArea", card.transform);
        CyberUIKit.Stretch(area, 30f, 62f, 30f, 22f);

        var drawingGo = new GameObject("Drawing", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        drawingGo.transform.SetParent(area, false);
        var drawingRt = (RectTransform)drawingGo.transform;
        drawingRt.anchorMin = drawingRt.anchorMax = new Vector2(0.5f, 0.5f);
        drawingRt.pivot = new Vector2(0.5f, 0.5f);
        drawingRt.anchoredPosition = Vector2.zero;
        drawingRt.sizeDelta = Vector2.zero;
        var raw = drawingGo.GetComponent<RawImage>();
        raw.raycastTarget = false;
        raw.enabled = false;
        var fitter = drawingGo.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = DrawingCanvas.TextureWidth / (float)DrawingCanvas.TextureHeight;

        TextMeshProUGUI name = CyberUIKit.NewText("NameText", card.transform, string.Empty, 30f, Color.white, FontStyles.Normal, TextAlignmentOptions.BottomLeft);
        name.overflowMode = TextOverflowModes.Ellipsis;
        name.rectTransform.anchorMin = new Vector2(0f, 0f);
        name.rectTransform.anchorMax = new Vector2(1f, 0f);
        name.rectTransform.pivot = new Vector2(0f, 0f);
        name.rectTransform.anchoredPosition = new Vector2(44f, 28f);
        name.rectTransform.sizeDelta = new Vector2(-88f, 42f);

        var pledgeCard = card.GetComponent<PledgeCard>();
        var so = new SerializedObject(pledgeCard);
        so.FindProperty("nameText").objectReferenceValue = name;
        so.FindProperty("drawingImage").objectReferenceValue = raw;
        so.FindProperty("drawingFitter").objectReferenceValue = fitter;
        so.FindProperty("group").objectReferenceValue = card.GetComponent<CanvasGroup>();
        so.FindProperty("glow").objectReferenceValue = glow;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(card, PledgeCardPrefabPath);
        Object.DestroyImmediate(card);
        return prefab;
    }
}
