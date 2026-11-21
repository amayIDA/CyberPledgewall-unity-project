using System.IO;
using UnityEditor;
using UnityEngine;

// Editor-only capture utility, also useful when tuning the reveal timing.
public static class FigmaVisualQA
{
    private static double next, end;
    private static int frame;
    private static RenderTexture target;
    private static Texture2D pixels;
    private static string captureFolder = "Captures/Reveal";
    [MenuItem("Cyber Pledge Wall/Capture Reveal Preview")]
    public static void CaptureReveal()
    {
        if(!Application.isPlaying)return;
        var board=Object.FindAnyObjectByType<WallBoard>();if(board==null)return;
        BeginCapture(board, false);
    }
    [MenuItem("Cyber Pledge Wall/Capture 3D Journey Preview")]
    public static void CaptureJourney()
    {
        if (!Application.isPlaying) return;
        var board=Object.FindAnyObjectByType<WallBoard>();if(board==null)return;
        BeginCapture(board,true);
    }
    private static void BeginCapture(WallBoard board,bool journey)
    {
        EditorApplication.update-=Tick;
        if(target!=null){target.Release();Object.DestroyImmediate(target);}
        if(pixels!=null)Object.DestroyImmediate(pixels);
        captureFolder=journey?"Captures/SingleAxisAfterglow":"Captures/Reveal";
        Directory.CreateDirectory(captureFolder);
        frame=0;next=EditorApplication.timeSinceStartup;end=next+(journey?20.0:16.0);
        target=new RenderTexture(1280,720,24);pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
        if(journey)board.PlayJourneyPreview();else board.PlayPreview();
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;
    }
    static void Tick()
    {
        if(!Application.isPlaying || EditorApplication.timeSinceStartup>=end)
        {
            EditorApplication.update-=Tick;
            if(target!=null){target.Release();Object.DestroyImmediate(target);}if(pixels!=null)Object.DestroyImmediate(pixels);
            Debug.Log("Reveal preview captured: "+frame+" frames.");return;
        }
        if(EditorApplication.timeSinceStartup<next)return;
        next=EditorApplication.timeSinceStartup+1.0/12;
        var cam=Camera.main;if(cam==null)return;
        var previous=cam.targetTexture;var active=RenderTexture.active;
        try
        {
            cam.targetTexture=target;Canvas.ForceUpdateCanvases();cam.Render();RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();
            File.WriteAllBytes(captureFolder+"/frame-"+(frame++).ToString("D3")+".png",pixels.EncodeToPNG());
        }
        finally{cam.targetTexture=previous;RenderTexture.active=active;}
    }
}
