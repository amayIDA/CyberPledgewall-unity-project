Shader "UI/Figma Screen Glass"
{
    Properties { [PerRendererData] _MainTex("Sprite Texture",2D)="white"{} _Color("Tint",Color)=(1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcColor
        ColorMask RGB
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            struct v2f {float4 vertex:SV_POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;};
            sampler2D _MainTex;fixed4 _Color;
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color*_Color;o.uv=v.uv;return o;}
            fixed4 frag(v2f i):SV_Target {fixed4 c=tex2D(_MainTex,i.uv)*i.color;return fixed4(c.rgb*c.a,c.a);}
            ENDCG
        }
    }
}
