Shader "Campaign/ReplayObservation" {
 Properties {_MainTex("Observation mask",2D)="black"{} _Blue("Blue",Color)=(.08,.22,.82,1) _Red("Red",Color)=(.84,.12,.14,1)}
 SubShader {Tags{"Queue"="Transparent" "RenderType"="Transparent"} Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
  Pass {CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _MainTex;float4 _Blue,_Red;
   struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;};struct v2f{float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float4 screen:TEXCOORD1;};
   v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.screen=ComputeScreenPos(o.vertex);return o;}
   fixed4 frag(v2f i):SV_Target{float2 m=tex2D(_MainTex,i.uv).rg;float2 p=i.screen.xy/i.screen.w*_ScreenParams.xy;float a=step(frac((p.x+p.y)/22),.136364)*m.r;float b=step(frac((p.x-p.y)/12),.15)*m.g;float w=a+b;float4 color=(_Blue*a+_Red*b)/max(.001,w);color.a=min(1,w)*.5+max(m.r,m.g)*.035;return color;}
   ENDCG}
 }
}
