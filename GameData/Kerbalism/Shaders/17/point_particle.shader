// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// render a point sprite
// - POINT_COLOR: point color
// - POINT_SIZE: point size on screen, in pixels


Shader "Custom/PointParticle"
{
  Properties
  {
    POINT_COLOR ("Point color", Color) = (1.0,1.0,1.0,1.0)
    POINT_SIZE ("Point size in pixels", Float) = 16.0
  }
  SubShader
  {
    Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
    Blend SrcAlpha One
    ColorMask RGB
    Cull Off Lighting Off ZWrite Off

    Pass
    {
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma target 2.5

      fixed4 POINT_COLOR;
      float POINT_SIZE;

      struct v2f
      {
        float size : PSIZE;
        float inv_hsize : TEXCOORD0;
        float2 screen_coords : TEXCOORD1;
      };

      v2f vert(float4 in_pos : POSITION, out float4 pos : SV_POSITION)
      {
        // output clip-space vertex position
        pos = UnityObjectToClipPos(in_pos);

        // pass down point size and inverse of point half-size
        v2f o;
        o.size = POINT_SIZE;
        o.inv_hsize = 2.0 / o.size;

        // calculate and pass down screen-space position of point center
        o.screen_coords = (pos.xy / pos.w) * 0.5 + 0.5;
        o.screen_coords *= _ScreenParams.xy;
        o.screen_coords.y = _ScreenParams.y - o.screen_coords.y;
        return o;
      }

      half4 frag(v2f i, UNITY_VPOS_TYPE screen_pos : VPOS) : COLOR
      {
        // calculate normalized distance between fragment screen-space
        // position, and point center in screen-space
        float k = 1.0 - distance(screen_pos.xy, i.screen_coords.xy) * i.inv_hsize;

        // calculate output color
        half4 output = POINT_COLOR;
        output.w *= k;
        return output;
      }
      ENDCG
    }
  }
}