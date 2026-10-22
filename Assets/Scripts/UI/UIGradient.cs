using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Vertical two-colour gradient multiplied over an Image's own colour.</summary>
[AddComponentMenu("UI/Effects/Vertical Gradient")]
public class UIGradient : BaseMeshEffect
{
    [SerializeField] private Color top = Color.white;
    [SerializeField] private Color bottom = Color.white;
    [Tooltip("1 = linear. Above 1 keeps the top colour longer and ramps to the bottom colour near the end.")]
    [SerializeField] private float bias = 1f;

    public void Set(Color topColor, Color bottomColor, float biasPower = 1f)
    {
        top = topColor;
        bottom = bottomColor;
        bias = Mathf.Max(0.1f, biasPower);
        if (graphic != null)
            graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0)
            return;

        var verts = new List<UIVertex>();
        vh.GetUIVertexStream(verts);

        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (UIVertex v in verts)
        {
            minY = Mathf.Min(minY, v.position.y);
            maxY = Mathf.Max(maxY, v.position.y);
        }

        float height = Mathf.Max(0.0001f, maxY - minY);
        for (int i = 0; i < verts.Count; i++)
        {
            UIVertex v = verts[i];
            float fromTop = 1f - (v.position.y - minY) / height;
            Color c = Color.Lerp(top, bottom, Mathf.Pow(fromTop, bias));
            v.color = (Color)v.color * c;
            verts[i] = v;
        }

        vh.Clear();
        vh.AddUIVertexTriangleStream(verts);
    }
}
