using UnityEngine;
using UnityEngine.UI;

// Card-shaped cyan rim light with two slow travelling highlights. No confetti,
// gravity particles or radial HUD ticks; the artwork stays unobstructed.
public class PledgeRevealFx : MaskableGraphic
{
    private float phase, strength;
    private bool anticipating;
    private PledgeCard following;
    private float afterglowUntil;
    public bool IsAnticipating => anticipating && strength > 0.001f;
    public bool IsAfterglowing => following != null && Time.unscaledTime < afterglowUntil;

    private void ResetFollow()
    {
        following=null;afterglowUntil=0;
        Vector2 center=WallBoard.DesignPosition(0);
        rectTransform.anchoredPosition3D=new Vector3(center.x,center.y,0);
        rectTransform.localRotation=Quaternion.identity;rectTransform.localScale=Vector3.one;
    }
    public void SetAnticipation(float alpha)
    {
        ResetFollow();anticipating=true;phase=1;strength=alpha;SetVerticesDirty();
    }
    public void SetState(float progress,float alpha)
    {
        ResetFollow();anticipating=false;phase=progress;strength=alpha;SetVerticesDirty();
    }
    public void BeginAfterglow(PledgeCard card,float seconds)
    {
        following=card;afterglowUntil=Time.unscaledTime+seconds;
        anticipating=false;phase=1;strength=0.75f;SetVerticesDirty();
    }
    private void LateUpdate()
    {
        if(following!=null)
        {
            float left=afterglowUntil-Time.unscaledTime;
            if(left<=0 || !following.gameObject.activeInHierarchy)
            {
                following=null;afterglowUntil=0;strength=0;SetVerticesDirty();return;
            }
            rectTransform.position=following.Rect.position;
            rectTransform.localScale=following.Rect.localScale;
            strength=0.75f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,1.5f,left));
        }
        if(strength>0.001f && !anticipating)SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();if(strength<=0.001f)return;
        Halo(vh,370,new Color(0.03f,0.48f,1,0.15f*strength));
        if(anticipating)
        {
            Halo(vh,160,new Color(0.1f,0.78f,1,0.13f*strength));return;
        }
        float half=Mathf.Lerp(315,276,Mathf.Clamp01(phase));
        var size=new Vector2(half,half);
        float head=Mathf.Repeat(Time.unscaledTime*0.17f,1);
        const int segments=160;
        for(int i=0;i<segments;i++)
        {
            float u=i/(float)segments,v=(i+1)/(float)segments;
            float tail=Mathf.Min(Mathf.Repeat(head-u,1),Mathf.Repeat(head+0.5f-u,1));
            float hot=tail<0.16f?Mathf.Pow(1-tail/0.16f,2):0;
            Color tint=Color.Lerp(new Color(0.1f,0.65f,1),new Color(0.7f,0.97f,1),hot);
            tint.a=(0.12f+hot*0.78f)*strength;
            Strip(vh,u,v,size,72,0.65f+hot*0.8f,tint);
            if(hot>0.01f)
            {
                tint.a=hot*strength*0.045f;
                Strip(vh,u,v,size,72,7+hot*5,tint);
            }
        }
        for(int i=0;i<2;i++)
        {
            Vector2 at=Point(head+i*0.5f,size,72);
            Color light=new Color(0.75f,0.98f,1,strength*0.65f);
            Quad(vh,at+new Vector2(-4,0),at+new Vector2(0,2),at+new Vector2(4,0),at+new Vector2(0,-2),light);
            light.a*=0.4f;
            Quad(vh,at+new Vector2(-1,-6),at+new Vector2(1,-6),at+new Vector2(1,6),at+new Vector2(-1,6),light);
        }
    }
    private static Vector2 Point(float u,Vector2 h,float r)
    {
        float horizontal=2*(h.x-r),vertical=2*(h.y-r),arc=Mathf.PI*r*0.5f;
        float d=Mathf.Repeat(u,1)*(2*horizontal+2*vertical+4*arc);
        if(d<horizontal)return new Vector2(h.x-r-d,h.y);d-=horizontal;
        if(d<arc)return Arc(new Vector2(-h.x+r,h.y-r),r,Mathf.PI*0.5f+d/r);d-=arc;
        if(d<vertical)return new Vector2(-h.x,h.y-r-d);d-=vertical;
        if(d<arc)return Arc(new Vector2(-h.x+r,-h.y+r),r,Mathf.PI+d/r);d-=arc;
        if(d<horizontal)return new Vector2(-h.x+r+d,-h.y);d-=horizontal;
        if(d<arc)return Arc(new Vector2(h.x-r,-h.y+r),r,Mathf.PI*1.5f+d/r);d-=arc;
        if(d<vertical)return new Vector2(h.x,-h.y+r+d);d-=vertical;
        return Arc(new Vector2(h.x-r,h.y-r),r,d/r);
    }
    private static Vector2 Arc(Vector2 center,float r,float a)=>center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r;
    private static void Strip(VertexHelper vh,float u,float v,Vector2 h,float r,float w,Color color)
    {
        Quad(vh,Point(u,h-Vector2.one*w,r-w),Point(v,h-Vector2.one*w,r-w),
            Point(v,h+Vector2.one*w,r+w),Point(u,h+Vector2.one*w,r+w),color);
    }
    private static void Halo(VertexHelper vh,float radius,Color color)
    {
        const int n=80;int start=vh.currentVertCount;
        vh.AddVert(Vector2.zero,color,Vector2.zero);Color edge=color;edge.a=0;
        for(int i=0;i<=n;i++)
        {
            float a=i/(float)n*Mathf.PI*2;
            vh.AddVert(new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,edge,Vector2.zero);
            if(i>0)vh.AddTriangle(start,start+i,start+i+1);
        }
    }
    private static void Quad(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)
    {
        int n=vh.currentVertCount;vh.AddVert(a,color,Vector2.zero);vh.AddVert(b,color,Vector2.zero);
        vh.AddVert(c,color,Vector2.zero);vh.AddVert(d,color,Vector2.zero);
        vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
    }
}
