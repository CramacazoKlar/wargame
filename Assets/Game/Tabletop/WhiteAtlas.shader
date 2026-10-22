Shader "Campaign/WhiteAtlas" {
 Properties { _MainTex("Atlas",2D)="white"{} }
 SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent"} Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex;
 struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;};struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;};
 v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
 fixed4 frag(v2f i):SV_Target {fixed4 c=tex2D(_MainTex,i.uv);float ink=1-smoothstep(.13,.67,c.b);float3 paper=float3(.94,.90,.79);return fixed4(lerp(paper,float3(.13,.12,.095),ink*.83),1);}
 ENDCG }
 }
}
