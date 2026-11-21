using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class DrawingUIBuilder
{
    [MenuItem("Cyber Pledge Wall/Rebuild Drawing UI")]
    public static void RebuildInDrawingScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/DrawingScene.unity");
        Build(Object.FindAnyObjectByType<Canvas>());
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
    static CanvasGroup Screen(Transform root,string name)
    {
        var rt=CyberUIKit.NewRect(name,root); CyberUIKit.Stretch(rt);
        return rt.gameObject.AddComponent<CanvasGroup>();
    }
    static void NamePanel(Transform root)
    {
        FigmaUIKit.GlassGroup(root,"NameGlass","Register_imgOriginalGlassGlow","Register_imgOriginalGlassTexture",new Vector4(151.4078f,150.8875f,1854.9445f,488.3049f),null,Vector4.zero,841.12f,684.96f,2156.16f,788.16f);
    }
    public static void Build(Canvas canvas)
    {
        FigmaUIKit.Base(canvas); var root=canvas.transform;
        var welcome=Screen(root,"WelcomeScreen");
        NamePanel(welcome.transform);
        FigmaUIKit.Text(welcome.transform,"WelcomeTitle","Your vision. Our future.",1040,895,1760,110,88,Color.white);
        FigmaUIKit.Text(welcome.transform,"WelcomeSubtitle","Share your vision for an AI-native government",1040,1080,1760,90,52,Color.white);
        var start=FigmaUIKit.Button(welcome.transform,"StartButton","Touch to begin",1552.188f,1637.7726f,735.6116f,193.6461f);

        var register=Screen(root,"RegisterScreen"); NamePanel(register.transform);
        FigmaUIKit.Text(register.transform,"NameTitle","Type your name",1693.634f,885.671f,453,72,64.2f,Color.white);
        var field=FigmaUIKit.Field(register.transform);
        register.gameObject.AddComponent<KeyboardAvoider>();
        var next=FigmaUIKit.Button(register.transform,"NextButton","Next",1552.188f,1637.7726f,735.6116f,193.6461f);

        var draw=Screen(root,"DrawScreen");
        // Decorative layers remain separate from the actual drawing texture and controls.
        FigmaUIKit.GlassGroup(draw.transform,"AnswerGlass","Draw_imgOriginalGlassGlow","Draw_imgOriginalGlassTexture",new Vector4(349.2518f,348.8927f,3170.4463f,1610.4508f),null,Vector4.zero,-14.4f,35.92f,3866.4f,2306.16f);
        FigmaUIKit.Art(draw.transform,"Draw_imgOriginalGlow",3752.64f,155.92f,1171.68f,1171.2f);
        var paper=FigmaUIKit.Art(draw.transform,"Draw_imgAnswerFieldVectors",417,779,3006,1142);
        var question=FigmaUIKit.Art(draw.transform,"Draw_imgGroup2",703.7088f,472.7778f,2432.5825f,330.9083f);
        FigmaUIKit.Text(question.transform,"Question","What is your vision for an AI-native government?",60,82,2310,130,101.195f,Color.white);
        var surface=CyberUIKit.NewRect("DrawingSurface",paper.transform);
        CyberUIKit.Stretch(surface,18,64,18,18);
        surface.gameObject.AddComponent<RawImage>();
        var drawing=surface.gameObject.AddComponent<DrawingCanvas>();
        var hint=FigmaUIKit.Text(paper.transform,"DrawingHint","Share your answer in up to three words...",873.8885f,520.7292f,1258.5304f,88.2707f,74.9f,new Color(0,0,0,0.35f));
        var hintDriver=hint.gameObject.AddComponent<DrawingHint>();
        var hintSo=new SerializedObject(hintDriver); hintSo.FindProperty("drawing").objectReferenceValue=drawing; hintSo.ApplyModifiedPropertiesWithoutUndo();
        var send=FigmaUIKit.Button(draw.transform,"SendButton","Submit",1730,1757,380,125,true);
        // Secondary tools sit within the writing area's lower corners.
        var undo=FigmaUIKit.Button(draw.transform,"UndoButton","Undo",510,1757,300,125,true);
        var clear=FigmaUIKit.Button(draw.transform,"ClearButton","Clear",3030,1757,300,125,true);

        var thanks=Screen(root,"ThanksScreen"); NamePanel(thanks.transform);
        var thanksText=FigmaUIKit.Text(thanks.transform,"ThanksText","Thank you!",1040,890,1760,150,118,Color.white);
        FigmaUIKit.Text(thanks.transform,"ThanksSubtitle","Your vision is now part of the pledge wall.",1040,1090,1760,90,52,Color.white);
        var status=FigmaUIKit.Text(root,"ClientStatusText","",600,2070,2640,64,40,Color.white);
        foreach(var screen in new[]{welcome,register,draw,thanks})
            foreach(Transform child in screen.transform) if(child.GetComponent<CanvasGroup>()==null)child.gameObject.AddComponent<CanvasGroup>();
        var controller=Object.FindAnyObjectByType<DrawingAppController>();
        if(controller==null)controller=new GameObject("DrawingAppController").AddComponent<DrawingAppController>();
        var controllerSo=new SerializedObject(controller);
        controllerSo.FindProperty("drawingCanvas").objectReferenceValue=drawing;
        controllerSo.FindProperty("statusText").objectReferenceValue=status; controllerSo.ApplyModifiedPropertiesWithoutUndo();
        var flowGo=CyberUIKit.NewRect("DrawingFlow",root).gameObject;
        var flow=flowGo.AddComponent<DrawingFlow>(); var so=new SerializedObject(flow);
        CyberUIKit.SetArray(so.FindProperty("screens"),new[]{welcome,register,draw,thanks});
        so.FindProperty("videoBackground").objectReferenceValue=root.Find("VideoBackground").GetComponent<SceneVideoBackground>();
        so.FindProperty("network").objectReferenceValue=controller;
        so.FindProperty("drawing").objectReferenceValue=drawing;
        so.FindProperty("startButton").objectReferenceValue=start;
        so.FindProperty("registerButton").objectReferenceValue=next;
        so.FindProperty("sendButton").objectReferenceValue=send;
        so.FindProperty("clearButton").objectReferenceValue=clear;
        so.FindProperty("undoButton").objectReferenceValue=undo;
        so.FindProperty("nameField").objectReferenceValue=field;
        so.FindProperty("drawPanel").objectReferenceValue=paper.rectTransform;
        so.FindProperty("thanksPanel").objectReferenceValue=thanksText.rectTransform;
        so.FindProperty("thanksText").objectReferenceValue=thanksText.rectTransform;
        so.ApplyModifiedPropertiesWithoutUndo();
        var configButton=FigmaUIKit.Button(root,"ConfigButton","Config",3550,2030,240,100,true);
        configButton.GetComponent<Image>().color=new Color(0.1f,0.5f,0.9f,0.18f);
        configButton.GetComponentInChildren<TMP_Text>().fontSize=18;
        configButton.GetComponentInChildren<TMP_Text>().color=new Color(0.55f,0.83f,1f);
        var host=configButton.gameObject.AddComponent<HostIpPanel>();
        var hostSo=new SerializedObject(host);
        hostSo.FindProperty("network").objectReferenceValue=controller;
        hostSo.FindProperty("configButton").objectReferenceValue=configButton;
        hostSo.ApplyModifiedPropertiesWithoutUndo();
        // Author only the welcome view visible; the flow animates the other screens.
        register.gameObject.SetActive(false); draw.gameObject.SetActive(false); thanks.gameObject.SetActive(false);
        root.Find("Draw_imgGroup").SetAsLastSibling();root.Find("Draw_imgGroup1").SetAsLastSibling();root.Find("Draw_imgVector").SetAsLastSibling();
    }
}
