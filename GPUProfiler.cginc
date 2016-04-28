#include "UnityCG.cginc"

struct appdata_t {
	float4 vertex : POSITION;
	fixed4 color : COLOR;
	float2 texcoord : TEXCOORD0;
};

struct v2f {
	float4 vertex : SV_POSITION;
	fixed4 color : COLOR;
	float2 texcoord : TEXCOORD0;
};

sampler2D _MainTex;
uniform float4 _MainTex_ST;
uniform fixed4 _Color;
uniform fixed4 _OutlineColor;
uniform float _OutlineOffset;

v2f vert (appdata_t v)
{
	v2f o;
	o.vertex = mul(UNITY_MATRIX_MVP, v.vertex);
	o.color = v.color * _Color;
	o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
#ifdef UNITY_HALF_TEXEL_OFFSET
	o.vertex.xy += (_ScreenParams.zw-1.0)*float2(-1,1);
#endif
#ifdef GPUPROFILER_OUTLINE
	o.color.rgb = _OutlineColor;
	o.vertex.xy += float2(_OutlineOffset, -_OutlineOffset);
#endif
	return o;
}

fixed4 frag (v2f i) : SV_Target
{
	fixed4 col = i.color;
	col.a *= tex2D(_MainTex, i.texcoord).a;
	clip (col.a - 0.01);
	return col;
}
