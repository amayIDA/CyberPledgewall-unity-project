using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the drawing station UI (Welcome, Register, Draw, Thank You) as ordinary scene objects,
/// laid out to the Cyber Carnival design at a 1920x1080 reference. Safe to run repeatedly.
/// </summary>
public static class DrawingUIBuilder
{
    private const string DrawingScenePath = "Assets/Scenes/DrawingScene.unity";

    private static readonly Vector2 ButtonPosition = new Vector2(0f, -399f);

    [MenuItem("Cyber Pledge Wall/Rebuild Drawing UI")]
    public static void RebuildInDrawingScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.OpenScene(DrawingScenePath, OpenSceneMode.Single);
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("DrawingScene has no Canvas.");
            return;
        }

        Build(canvas);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Drawing UI rebuilt and saved to DrawingScene.");
    }

    public static void Build(Canvas canvas)
    {
        CyberUIKit.EnsureAssets();

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

        SceneVideoBackground video = CyberUIKit.BuildVideoBackground(root);

        // ---- Welcome ----
        CanvasGroup welcome = NewScreen(root, "WelcomeScreen");
        Button start = CyberUIKit.BuildButton(welcome.transform, "StartButton", "Touch to Explore", 28f, ButtonPosition);

        // ---- Register ----
        CanvasGroup register = NewScreen(root, "RegisterScreen");
        CyberField nameField = CyberUIKit.BuildField(register.transform, "NameInput", "Name", new Vector2(0f, 240f), 40);
        CyberField idField = CyberUIKit.BuildField(register.transform, "EmailIdInput", "Email ID", new Vector2(0f, 60f), 80);
        register.gameObject.AddComponent<KeyboardAvoider>();
        Button submit = CyberUIKit.BuildButton(register.transform, "SubmitButton", "Submit", 38f, ButtonPosition);

        // ---- Draw ----
        CanvasGroup draw = NewScreen(root, "DrawScreen");
        BuildDrawPanel(draw.transform, out RectTransform drawPanel, out DrawingCanvas drawing, out Button undo, out Button clear);
        Button send = CyberUIKit.BuildButton(draw.transform, "SendButton", "Send", 38f, ButtonPosition);

        // ---- Thank you ----
        CanvasGroup thanks = NewScreen(root, "ThanksScreen");
        BuildThanksPanel(thanks.transform, out RectTransform thanksPanel, out RectTransform thanksText);

        // ---- Status (small, unobtrusive; shows connection problems) ----
        TextMeshProUGUI status = CyberUIKit.NewText("ClientStatusText", root, string.Empty, 20f, new Color(1f, 1f, 1f, 0.5f));
        CyberUIKit.Place(status.rectTransform, new Vector2(0f, -508f), new Vector2(1400f, 30f));

        // Every direct child of a screen gets a CanvasGroup so the flow can fade it in.
        foreach (CanvasGroup screen in new[] { welcome, register, draw, thanks })
            foreach (Transform child in screen.transform)
                if (child.GetComponent<CanvasGroup>() == null)
                    child.gameObject.AddComponent<CanvasGroup>();

        // ---- Logic ----
        var controller = Object.FindAnyObjectByType<DrawingAppController>();
        if (controller == null)
            controller = new GameObject("DrawingAppController").AddComponent<DrawingAppController>();

        var controllerSo = new SerializedObject(controller);
        controllerSo.FindProperty("drawingCanvas").objectReferenceValue = drawing;
        controllerSo.FindProperty("statusText").objectReferenceValue = status;
        controllerSo.ApplyModifiedPropertiesWithoutUndo();

        var flowGo = new GameObject("DrawingFlow", typeof(RectTransform));
        flowGo.transform.SetParent(root, false);
        var flow = flowGo.AddComponent<DrawingFlow>();
        var so = new SerializedObject(flow);
        CyberUIKit.SetArray(so.FindProperty("screens"), new[] { welcome, register, draw, thanks });
        so.FindProperty("videoBackground").objectReferenceValue = video;
        so.FindProperty("network").objectReferenceValue = controller;
        so.FindProperty("drawing").objectReferenceValue = drawing;
        so.FindProperty("startButton").objectReferenceValue = start;
        so.FindProperty("registerButton").objectReferenceValue = submit;
        so.FindProperty("sendButton").objectReferenceValue = send;
        so.FindProperty("clearButton").objectReferenceValue = clear;
        so.FindProperty("undoButton").objectReferenceValue = undo;
        so.FindProperty("nameField").objectReferenceValue = nameField;
        so.FindProperty("employeeField").objectReferenceValue = idField;
        so.FindProperty("drawPanel").objectReferenceValue = drawPanel;
        so.FindProperty("thanksPanel").objectReferenceValue = thanksPanel;
        so.FindProperty("thanksText").objectReferenceValue = thanksText;
        so.ApplyModifiedPropertiesWithoutUndo();

        var hostGo = new GameObject("HostIpPanel", typeof(RectTransform), typeof(Image), typeof(HostIpPanel));
        hostGo.transform.SetParent(root, false);
        hostGo.transform.SetAsLastSibling();
        var host = hostGo.GetComponent<HostIpPanel>();
        var hostSo = new SerializedObject(host);
        hostSo.FindProperty("network").objectReferenceValue = controller;
        hostSo.ApplyModifiedPropertiesWithoutUndo();
    }

    private static CanvasGroup NewScreen(Transform root, string name)
    {
        RectTransform rt = CyberUIKit.NewRect(name, root);
        CyberUIKit.Stretch(rt);
        return rt.gameObject.AddComponent<CanvasGroup>();
    }

    private static RectTransform NewPanel(Transform parent, string name, Vector2 center, Vector2 visibleSize, Color top, Color bottom, float bias)
    {
        // The outline sprite keeps 14px of glow margin inside its texture.
        RectTransform panel = CyberUIKit.NewRect(name, parent);
        CyberUIKit.Place(panel, center, visibleSize + new Vector2(28f, 28f));
        CyberUIKit.AddNeonLayers(panel, 1f, top, bottom, bias);
        return panel;
    }

    private static void BuildDrawPanel(Transform parent, out RectTransform panel, out DrawingCanvas drawing, out Button undo, out Button clear)
    {
        panel = NewPanel(parent, "DrawPanel", new Vector2(0f, 28f), new Vector2(1195f, 660f),
            new Color(0.02f, 0.30f, 0.52f), new Color(0.10f, 0.26f, 0.86f), 1.3f);

        // Content area sits inside the inner hairline: 33px in from the visible edge (+14 glow margin).
        RectTransform content = CyberUIKit.NewRect("Content", panel);
        CyberUIKit.Stretch(content, 47f, 47f, 47f, 47f);

        Image header = CyberUIKit.NewImage("Header", content, CyberUIKit.RoundedTop, Color.white, true);
        header.pixelsPerUnitMultiplier = 1.1f;
        header.rectTransform.anchorMin = new Vector2(0f, 1f);
        header.rectTransform.anchorMax = new Vector2(1f, 1f);
        header.rectTransform.pivot = new Vector2(0.5f, 1f);
        header.rectTransform.anchoredPosition = Vector2.zero;
        header.rectTransform.sizeDelta = new Vector2(0f, 68f);
        header.gameObject.AddComponent<UIGradient>().Set(new Color(0.01f, 0.64f, 0.79f), new Color(0.02f, 0.18f, 0.44f), 1.15f);

        TextMeshProUGUI title = CyberUIKit.NewText("Title", header.transform, "My Cyber Pledge", 42f, Color.white, FontStyles.Bold);
        CyberUIKit.Stretch(title.rectTransform);

        Image paper = CyberUIKit.NewImage("Paper", content, CyberUIKit.RoundedBottom, Color.white, true);
        paper.pixelsPerUnitMultiplier = 1.1f;
        paper.rectTransform.anchorMin = Vector2.zero;
        paper.rectTransform.anchorMax = Vector2.one;
        paper.rectTransform.offsetMin = Vector2.zero;
        paper.rectTransform.offsetMax = new Vector2(0f, -68f);
        paper.gameObject.AddComponent<UIGradient>().Set(new Color(0.72f, 0.74f, 0.78f), new Color(0.71f, 0.78f, 0.93f), 2.4f);

        var surface = new GameObject("DrawingSurface", typeof(RectTransform), typeof(RawImage), typeof(DrawingCanvas));
        surface.transform.SetParent(paper.transform, false);
        CyberUIKit.Stretch((RectTransform)surface.transform);
        var raw = surface.GetComponent<RawImage>();
        raw.color = new Color(1f, 1f, 1f, 0f);   // DrawingCanvas turns it white at runtime; clear keeps the editor view clean
        raw.raycastTarget = true;
        drawing = surface.GetComponent<DrawingCanvas>();

        undo = CyberUIKit.BuildChipButton(paper.transform, "UndoButton", "Undo", new Vector2(-150f, 14f), new Vector2(120f, 46f));
        clear = CyberUIKit.BuildChipButton(paper.transform, "ClearButton", "Clear", new Vector2(-18f, 14f), new Vector2(120f, 46f));
    }

    private static void BuildThanksPanel(Transform parent, out RectTransform panel, out RectTransform text)
    {
        panel = NewPanel(parent, "ThanksPanel", new Vector2(0f, -47f), new Vector2(1195f, 660f),
            new Color(0f, 0.05f, 0.25f), new Color(0.06f, 0.26f, 0.82f), 2.6f);

        TextMeshProUGUI label = CyberUIKit.NewText("ThanksText", panel, "Thank You!", 130f, Color.white, FontStyles.Bold);
        CyberUIKit.Place(label.rectTransform, new Vector2(0f, 12f), new Vector2(1000f, 200f));
        text = label.rectTransform;
    }

    private static void SetVectors(SerializedProperty array, Vector2[] values)
    {
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            array.GetArrayElementAtIndex(i).vector2Value = values[i];
    }

    private static void SetFloats(SerializedProperty array, float[] values)
    {
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            array.GetArrayElementAtIndex(i).floatValue = values[i];
    }
}
