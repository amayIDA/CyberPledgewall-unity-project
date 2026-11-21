Shader "UI/Figma Glass Group"
{
 Properties
 {
  [PerRendererData] _MainTex("Outer glow",2D)="white"{}
  _GlassTex("Glass",2D)="black"{}
  _InnerTex("Inner glow",2D)="black"{}
  _GlassRect("Glass xywh",Vector)=(0.2,0.2,0.6,0.6)
  _InnerRect("Inner xywh",Vector)=(0.1,0.1,0.8,0.8)
  _InnerEnabled("Inner enabled",Float)=1
 }
 SubShader
 {
  Tags {"Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True"}
  Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
  Blend One OneMinusSrcColor
  ColorMask RGB
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct appdata{float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
   struct v2f{float4 vertex:SV_POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;};
   sampler2D _MainTex,_GlassTex,_InnerTex;float4 _GlassRect,_InnerRect;float _InnerEnabled;
   v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;return o;}
   fixed4 layer(sampler2D t,float2 uv,float4 r){float2 q=(uv-float2(r.x,1-r.y-r.w))/r.zw;return tex2D(t,q)*step(0,q.x)*step(q.x,1)*step(0,q.y)*step(q.y,1);}
   fixed4 frag(v2f i):SV_Target
   {
    fixed4 a=tex2D(_MainTex,i.uv),b=layer(_GlassTex,i.uv,_GlassRect),c=layer(_InnerTex,i.uv,_InnerRect)*_InnerEnabled;
    float3 rgb=b.rgb*b.a+a.rgb*a.a*(1-b.a);
    rgb=1-(1-rgb)*(1-c.rgb*c.a);
    return fixed4(rgb*i.color.rgb*i.color.a,1);
   }
   ENDCG
  }
 }
}
