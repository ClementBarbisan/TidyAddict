// Zones de collecte : translucide en vue directe, silhouette atténuée à
// travers les murs (ZTest Greater), faces intérieures visibles (Cull Off)
// pour que la zone reste lisible quand on est dedans.
Shader "TidyAddict/ZoneXRay"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 0.3)
        _OccludedAlpha ("Occluded Alpha", Range(0, 1)) = 0.12
    }

    SubShader
    {
        Tags { "Queue" = "Transparent+50" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        // Passe 1 : parties cachées par la géométrie (vision à travers les murs)
        Pass
        {
            ZTest Greater

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _OccludedAlpha;

            struct v2f { float4 pos : SV_POSITION; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return fixed4(_Color.rgb, _OccludedAlpha);
            }
            ENDCG
        }

        // Passe 2 : parties directement visibles
        Pass
        {
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;

            struct v2f { float4 pos : SV_POSITION; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}
