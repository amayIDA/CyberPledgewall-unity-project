using UnityEngine;
using UnityEngine.UI;

// Maintain Figma's fixed glow margins as the card changes from satellite to hero.
[ExecuteAlways]
public class FigmaCardSkin : MonoBehaviour
{
    [SerializeField] private RectTransform smallSkin;
    [SerializeField] private RectTransform featuredSkin;
    [SerializeField] private RectTransform drawingArea;
    [SerializeField] private RectTransform nameArea;
    [SerializeField] private Image glow;
    [SerializeField] private Sprite smallGlow;
    [SerializeField] private Sprite featuredGlow;
    private void OnEnable() => Layout();
    private void OnRectTransformDimensionsChange() => Layout();
    private void Layout()
    {
        var rect=transform as RectTransform;
        if(rect==null || smallSkin==null || featuredSkin==null || drawingArea==null || nameArea==null)return;
        bool featured=rect.rect.width>650f;
        smallSkin.gameObject.SetActive(!featured); featuredSkin.gameObject.SetActive(featured);
        float baseSize=featured?768.24f:585.84f;
        float k=rect.rect.width/baseSize;
        smallSkin.localScale=featuredSkin.localScale=Vector3.one*k;
        // Card content positions are measured inside the exact exported artwork.
        Place(drawingArea, featured?new Vector2(154.16f,154.52f):new Vector2(142.68f,142.04f),
            featured?new Vector2(460f,382.5f):new Vector2(301.5f,251.5f),k);
        Place(nameArea,featured?new Vector2(225f,564.47f):new Vector2(175f,410.84f),
            featured?new Vector2(318f,39.87f):new Vector2(235f,30f),k);
        if(glow!=null)glow.sprite=featured?featuredGlow:smallGlow;
    }
    private static void Place(RectTransform rt,Vector2 pos,Vector2 size,float k)
    {
        rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,1);
        rt.anchoredPosition=new Vector2(pos.x,-pos.y)*k; rt.sizeDelta=size*k;
    }
}
