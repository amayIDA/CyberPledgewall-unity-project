using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class WallUIBuilder
{
    const string PrefabPath="Assets/Prefabs/UI/PledgeCard.prefab";
    [MenuItem("Cyber Pledge Wall/Rebuild Wall UI")]
    public static void RebuildInWallScene()
    {
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/WallScene.unity");
        Build(Object.FindAnyObjectByType<Canvas>());
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
    }
    public static void Build(Canvas canvas)
    {
        FigmaUIKit.Base(canvas);
        var prefab=CreatePledgeCardPrefab();
        var grid=CyberUIKit.NewRect("PledgeConstellation",canvas.transform); CyberUIKit.Stretch(grid);
        var cards=new PledgeCard[WallBoard.MaxVisible];
        for(int i=0;i<cards.Length;i++)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,grid);
            go.name=i==0?"FeaturedPledge":"SatellitePledge"+i;
            CyberUIKit.Place((RectTransform)go.transform,WallBoard.DesignPosition(i),WallBoard.DesignSize(i));
            if(i>1)
            {
                int variant=i==2?1:i==3?2:3;
                var skin=go.transform.Find("SatelliteGlass");
                var composite=skin.GetChild(0).GetComponent<Image>();
                composite.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(FigmaUIKit.Folder+"Wall_imgOriginalOuterGlow"+variant+".png");
                var material=new Material(composite.material);
                material.SetTexture("_InnerTex",AssetDatabase.LoadAssetAtPath<Texture2D>(FigmaUIKit.Folder+"Wall_imgOriginalInnerGlow"+variant+".png"));
                string path=FigmaUIKit.Folder+"SatelliteGlass"+variant+".mat";
                var saved=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(saved==null){AssetDatabase.CreateAsset(material,path);saved=material;}else {EditorUtility.CopySerialized(material,saved);Object.DestroyImmediate(material);}
                composite.material=saved;
                skin.GetChild(1).GetComponent<Image>().sprite=AssetDatabase.LoadAssetAtPath<Sprite>(FigmaUIKit.Folder+"Wall_imgGroup"+(variant+2)+".png");
            }
            cards[i]=go.GetComponent<PledgeCard>();
        }
        var boardGo=CyberUIKit.NewRect("WallBoard",grid).gameObject;
        var board=boardGo.AddComponent<WallBoard>(); var so=new SerializedObject(board);
        CyberUIKit.SetArray(so.FindProperty("cards"),cards);
        so.FindProperty("heroCardSize").vector2Value=WallBoard.DesignSize(0);
        so.FindProperty("idleSeconds").floatValue=5f;
        so.FindProperty("showHistoryFlight").boolValue=true;
        so.FindProperty("historyFlightSeconds").floatValue=5.8f;
        so.FindProperty("flyInSeconds").floatValue=2.15f;
        so.FindProperty("chargeSeconds").floatValue=0.48f;
        so.FindProperty("holdSeconds").floatValue=1.8f;
        so.FindProperty("swoopSeconds").floatValue=0.75f;
        so.ApplyModifiedPropertiesWithoutUndo();
        var status=FigmaUIKit.Text(canvas.transform,"WallStatusText","",600,2070,2640,60,36,Color.white);
        var manager=Object.FindAnyObjectByType<WallManager>();
        if(manager!=null){var m=new SerializedObject(manager);m.FindProperty("board").objectReferenceValue=board;m.FindProperty("statusText").objectReferenceValue=status;m.ApplyModifiedPropertiesWithoutUndo();}
    }
    static RectTransform Skin(Transform parent,bool big)
    {
        var rt=CyberUIKit.NewRect(big?"FeaturedGlass":"SatelliteGlass",parent);
        rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,1); rt.anchoredPosition=Vector2.zero;
        rt.sizeDelta=big?new Vector2(768.24f,768.12f):new Vector2(585.84f,585.6f);
        FigmaUIKit.GlassGroup(rt,big?"HeroGlassComposite":"SmallGlassComposite",big?"Wall_imgOriginalOuterGlow4":"Wall_imgOriginalOuterGlow","Wall_imgOriginalGlassTexture",
            big?new Vector4(238.43f,237.76f,1059.8f,1059.8f):new Vector4(239.86f,238.75f,693.2f,693.2f),
            big?"Wall_imgOriginalInnerGlow4":"Wall_imgOriginalInnerGlow",
            big?new Vector4(109.92f,109.92f,1317.36f,1316.16f):new Vector4(110.16f,109.44f,953.04f,952.32f),0,0,big?1536.48f:1171.68f,big?1536.24f:1171.2f);
        FigmaUIKit.Art(rt,big?"Wall_imgGroup6":"Wall_imgGroup2",big?311.08f:287.35f,big?311.03f:286.70f,big?914.48f:598.12f,big?760.41f:497.43f);
        return rt;
    }
    public static GameObject CreatePledgeCardPrefab()
    {
        FigmaUIKit.Prepare();
        var go=new GameObject("PledgeCard",typeof(RectTransform),typeof(CanvasGroup),typeof(PledgeCard));
        var rt=(RectTransform)go.transform; rt.sizeDelta=WallBoard.DesignSize(1);
        var small=Skin(rt,false); var big=Skin(rt,true);
        var glow=CyberUIKit.NewImage("ArrivalGlow",rt,AssetDatabase.LoadAssetAtPath<Sprite>(FigmaUIKit.Folder+"Wall_imgOriginalInnerGlow.png"),new Color(0.4f,0.94f,1f,0));
        CyberUIKit.Stretch(glow.rectTransform,55,55,55,55);
        var area=CyberUIKit.NewRect("DrawingArea",rt);
        var imageGo=new GameObject("Drawing",typeof(RectTransform),typeof(RawImage),typeof(AspectRatioFitter));imageGo.transform.SetParent(area,false);
        var raw=imageGo.GetComponent<RawImage>();raw.raycastTarget=false;raw.enabled=false;
        var fit=imageGo.GetComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fit.aspectRatio=DrawingCanvas.TextureWidth/(float)DrawingCanvas.TextureHeight;
        var name=CyberUIKit.NewText("NameText",rt,"",22.48f,Color.white); name.font=FigmaUIKit.Font;
        name.enableAutoSizing=true;name.fontSizeMin=14;name.fontSizeMax=33.67f;name.overflowMode=TextOverflowModes.Ellipsis;
        var card=new SerializedObject(go.GetComponent<PledgeCard>());
        card.FindProperty("nameText").objectReferenceValue=name;card.FindProperty("drawingImage").objectReferenceValue=raw;card.FindProperty("drawingFitter").objectReferenceValue=fit;card.FindProperty("group").objectReferenceValue=go.GetComponent<CanvasGroup>();card.FindProperty("glow").objectReferenceValue=glow;card.FindProperty("emptyAlpha").floatValue=0.65f;card.ApplyModifiedPropertiesWithoutUndo();
        var skin=go.AddComponent<FigmaCardSkin>(); var s=new SerializedObject(skin);
        s.FindProperty("smallSkin").objectReferenceValue=small;s.FindProperty("featuredSkin").objectReferenceValue=big;s.FindProperty("drawingArea").objectReferenceValue=area;s.FindProperty("nameArea").objectReferenceValue=name.rectTransform;s.FindProperty("glow").objectReferenceValue=glow;
        s.FindProperty("smallGlow").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Sprite>(FigmaUIKit.Folder+"Wall_imgOriginalInnerGlow.png");
        s.FindProperty("featuredGlow").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Sprite>(FigmaUIKit.Folder+"Wall_imgOriginalInnerGlow4.png");s.ApplyModifiedPropertiesWithoutUndo();
        skin.enabled=false;skin.enabled=true;
        var prefab=PrefabUtility.SaveAsPrefabAsset(go,PrefabPath);Object.DestroyImmediate(go);return prefab;
    }
}
