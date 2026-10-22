Shader "Campaign/WornMapPaper" {
 Properties { _MainTex("Paper",2D)="white"{} _Color("Tint",Color)=(1,1,1,1) _MapRect("World bounds",Vector)=(-50,-30,50,30) }
 SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True"} Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex;fixed4 _Color;float4 _MapRect;
 struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float2 world:TEXCOORD1;fixed4 color:COLOR;};
 v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.world=mul(unity_ObjectToWorld,v.vertex).xy;o.color=v.color*_Color;return o;}
 float bite(float t,float side){float s=side*7.13;float wave=.024+.017*sin(t*4.7+s)+.012*sin(t*13.1+s);float notch=pow(max(0,sin(t*1.71+s)),28)*(.13+.09*sin(t*.31+s));return max(.008,wave+notch);}
 fixed4 frag(v2f i):SV_Target {float2 p=i.world;float distance=min(min(p.x-_MapRect.x-bite(p.y,0),_MapRect.z-p.x-bite(p.y,1)),min(p.y-_MapRect.y-bite(p.x,2),_MapRect.w-p.y-bite(p.x,3)));clip(distance);fixed4 c=tex2D(_MainTex,i.uv)*i.color;c.rgb*=lerp(.79,1,smoothstep(0,.035,distance));c.a*=smoothstep(0,.009,distance);return c;}
 ENDCG }
 }
}
