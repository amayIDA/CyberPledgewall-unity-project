using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

// Run only in Play mode. All test pledges stay in memory and are never sent to storage.
public static class PledgeHistoryFlightQA
{
    public static string Status { get; private set; } = "Not run";
    public static void Run()
    {
        var board=UnityEngine.Object.FindAnyObjectByType<WallBoard>();
        if(!Application.isPlaying || board==null)throw new InvalidOperationException("Open WallScene in Play mode.");
        Status="Running";board.StartCoroutine(Check(board));
    }
    private static void Require(bool condition,string label)
    {
        if(!condition){Status="FAILED: "+label;throw new Exception(Status);}
    }
    private static IEnumerator Check(WallBoard board)
    {
        try
        {
            board.Clear();board.PlayPreview();
            yield return new WaitForSecondsRealtime(1f);
            var flight=UnityEngine.Object.FindAnyObjectByType<PledgeHistoryFlight>(FindObjectsInactive.Include);
            Require(flight==null || !flight.IsRunning,"First pledge skips history flight");
            board.Clear();
            var make=typeof(WallBoard).GetMethod("MakePreviewDrawing",BindingFlags.NonPublic|BindingFlags.Static);
            var png=(byte[])make.Invoke(null,null);
            board.AddSilently("QA Historical Author",png);board.PlayPreview();
            yield return new WaitForSecondsRealtime(1f);
            flight=UnityEngine.Object.FindAnyObjectByType<PledgeHistoryFlight>(FindObjectsInactive.Include);
            Require(flight!=null && flight.IsRunning && flight.LastHistoryCount==1,"Single historical pledge gets a flight");
            board.Clear();Require(!flight.IsRunning && !flight.gameObject.activeSelf,"Clear cancels a running flight");
            board.AddSilently("QA Duplicate Image Earlier",png);board.Add("QA Duplicate Image New",png);
            yield return new WaitForSecondsRealtime(1f);
            Require(flight.IsRunning && flight.LastHistoryCount==1,"Equal image data does not exclude an earlier pledge");
            board.Clear();
            for(int i=0;i<100;i++)board.AddSilently("QA Author "+i,png);
            board.PlayPreview();yield return new WaitForSecondsRealtime(1f);
            Require(flight.IsRunning && flight.LastHistoryCount==100,"Archive includes every older pledge before sampling");
            Require(flight.VisiblePoolCount==24,"Large archive uses a bounded pool");
            var names=new HashSet<string>();
            foreach(var card in flight.GetComponentsInChildren<PledgeCard>())
                names.Add(card.GetComponentInChildren<TMP_Text>().text);
            Require(names.Count==24,"Drawings that share image bytes retain their distinct authors");
            board.Clear();Require(!flight.IsRunning,"Interrupting a large gallery leaves no flight running");
            board.PlayJourneyPreview();yield return new WaitForSecondsRealtime(1f);
            Require(flight.IsRunning && flight.LastHistoryCount==12,"Sample preview uses historical pledges");
            Require(flight.VisiblePoolCount==24,"Repeated preview reuses the card pool");
            float deadline = Time.realtimeSinceStartup + 8f;
            while (flight.IsRunning && Time.realtimeSinceStartup < deadline) yield return null;
            Require(flight.HasDocked && flight.LastDockError < 0.01f,"Gallery docks into exact Figma positions");
            var pool=(List<PledgeCard>)typeof(WallBoard).GetField("pool",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(board);
            for(int i=1;i<WallBoard.MaxVisible;i++)
            {
                Vector2 home=WallBoard.DesignPosition(i);
                Require(Vector3.Distance(pool[i].Rect.anchoredPosition3D,new Vector3(home.x,home.y,0))<0.01f,"Dock position "+i);
                Require(Vector2.Distance(pool[i].Rect.sizeDelta,WallBoard.DesignSize(i))<0.01f,"Dock dimensions "+i);
                Require(Quaternion.Angle(pool[i].Rect.localRotation,Quaternion.identity)<0.01f,"Dock rotation "+i);
                Require(pool[i].Alpha>0.6f,"Wall remains visible at handoff "+i);
            }
            Require(!pool[0].HasPledge && pool[0].Alpha<0.01f,"Center is empty at the camera stop");
            var fx=UnityEngine.Object.FindAnyObjectByType<PledgeRevealFx>();
            Require(fx!=null && fx.IsAnticipating,"Glow anticipates the new pledge at the empty center");
            Require(pool[1].HasPledge,"Previous featured pledge remains on a satellite");
            var hero=(PledgeCard)typeof(WallBoard).GetMethod("GetHero",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(board,null);
            Require(hero.Alpha<0.01f,"Reveal waits until the camera has landed");
            var ink=hero.GetComponentInChildren<UnityEngine.UI.RawImage>(true);
            deadline=Time.realtimeSinceStartup+6f;
            while(hero.Alpha<0.05f && Time.realtimeSinceStartup<deadline)yield return null;
            float angularTravel=0f;
            Quaternion previous=hero.Rect.localRotation;
            while(ink.color.a<0.05f && Time.realtimeSinceStartup<deadline)
            {
                yield return null;
                angularTravel+=Quaternion.Angle(previous,hero.Rect.localRotation);
                previous=hero.Rect.localRotation;
                Require(hero.GetComponentInChildren<TMP_Text>(true).maxVisibleCharacters==0,"Name stays hidden during rotations");
                Require(Vector3.Dot(hero.Rect.localRotation*Vector3.up,Vector3.up)>0.99999f,"Spin is confined to the vertical axis");
            }
            Require(angularTravel>540f,"Arrival performs multiple visible 3D rotations before revealing ink");
            while(ink.color.a<0.99f && Time.realtimeSinceStartup<deadline)yield return null;
            yield return new WaitForSecondsRealtime(0.15f);
            Require(Quaternion.Angle(hero.Rect.localRotation,Quaternion.identity)<8f,"Card catches facing the viewer for reading");
            deadline=Time.realtimeSinceStartup+5f;
            while(hero.Alpha>0.01f && Time.realtimeSinceStartup<deadline)yield return null;
            Require(fx.IsAfterglowing && pool[0].IsHighlighted,"Latest pledge receives a lingering accent after landing");
            yield return new WaitForSecondsRealtime(2f);
            Require(fx.IsAfterglowing && pool[0].IsHighlighted,"Accent persists beyond the initial reveal");
            board.Clear();
            Require(!fx.IsAfterglowing && !pool[0].IsHighlighted,"Clearing removes the highlight without orphan effects");
            Require(!flight.gameObject.activeSelf,"Preview cleanup restores the wall");
            Status="Passed: first pledge, single history, shared image data, 100-pledge archive, distinct authors, interruption, pool reuse, exact satellite landing, empty center, anticipation glow, reveal order, single-axis rotations, readable catch, lasting afterglow, preview cleanup.";
            Debug.Log("History journey QA: "+Status);
        }
        finally
        {
            // Restore real saved submissions after tests; never leave a test-cleared
            // runtime wall without its archive for the next tablet submission.
            board.Clear();
            if(WallManager.Instance!=null)WallManager.Instance.LoadSavedPledgesOntoWall();
        }
    }
}
