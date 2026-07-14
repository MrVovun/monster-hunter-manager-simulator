//  CrtFx © NullTale - https://x.com/NullTale
Shader "Hidden/Vol/Crt"
{
	HLSLINCLUDE
    
    struct vert_in
    {
        float4 pos : POSITION;
        float2 uv  : TEXCOORD0;
    };

    struct frag_in
    {
        float2 uv     : TEXCOORD0;
        float4 vertex : SV_POSITION;
    };
                
    frag_in vert(vert_in input)
    {
        frag_in output;
        output.vertex = input.pos;
        output.uv     = input.uv;
        
        return output;
    }
    
    ENDHLSL

	SubShader 
	{
        ZTest Always
        ZWrite Off
        ZClip false
        Cull Off
		
        Pass	// 0
		{
			name "Rays"
			
	        HLSLPROGRAM
	        
			#pragma vertex vert
			#pragma fragment frag
	        
            #pragma multi_compile_local _MOTION _
	        	        
	        sampler2D _MainTex;
			sampler2D _MaskTex;
	        sampler2D _GlowTex;
	        
			float4 _Step;
			float4 _Data;
			float4 _Bleed;
	        
			float4 _R;
			float4 _G;
			float4 _B;
	        
			#define _GridSize   _Data.xy;
			#define _GridWeight _Data.z;
	        
			static const half _kernel[9] =
			{
			    0.10, 1.20, 0.30,
			    1.50, 4.00, 3.00,
			    0.10, 1.20, 0.30
			};
	        
	        float2 rotate(float2 vec, float angle)
	        {
		        float c = cos(angle);
		        float s = sin(angle);
	        	
	        	return float2(dot(vec, float2(c, -s)), dot(vec, float2(s, c)));
	        }
	        
            float luma(float3 rgb)
            {
                return dot(rgb, float3(.299, .587, .114));
            }
	        
            half bright(half3 rgb)
            {
                return max(max(rgb.r, rgb.g), rgb.b);
            }

			half3 sample(float2 uv, float blur, half3 mask)
			{
			    float2 step = _Step.xy * blur;

			    half4 center = tex2D(_MainTex, uv);

			    half4 left  = tex2D(_MainTex, uv + float2(-step.x, 0));
			    half4 right = tex2D(_MainTex, uv + float2( step.x, 0));

			    half4 up    = tex2D(_MainTex, uv + float2(0, -step.y));
			    half4 down  = tex2D(_MainTex, uv + float2(0,  step.y));

			    half4 ul    = tex2D(_MainTex, uv + float2(-step.x, -step.y));
			    half4 ur    = tex2D(_MainTex, uv + float2( step.x, -step.y));

			    half4 dl    = tex2D(_MainTex, uv + float2(-step.x,  step.y));
			    half4 dr    = tex2D(_MainTex, uv + float2( step.x,  step.y));

				half3 beam =
				      ul.rgb     * _kernel[0]
				    + up.rgb     * _kernel[1]
				    + ur.rgb     * _kernel[2]
				    + left.rgb   * _kernel[3]
				    + center.rgb * _kernel[4] * 3 * mask
				    + right.rgb  * _kernel[5]
				    + dl.rgb     * _kernel[6]
				    + down.rgb   * _kernel[7]
				    + dr.rgb     * _kernel[8];

				beam /= (
				      _kernel[0] + _kernel[1] + _kernel[2]
				    + _kernel[3] + _kernel[4] + _kernel[5]
				    + _kernel[6] + _kernel[7] + _kernel[8]);
	        	
			    return beam * mask;
			}
	        
			half4 frag(frag_in i) : SV_Target
			{
			    half3 color = 0;
				// float2 uvPix = (i.uv * 360 - frac(i.uv * 360)) / 360;
			    color += sample(i.uv, _R.w, _R.rgb);
			    color += sample(i.uv, _G.w, _G.rgb);
			    color += sample(i.uv, _B.w, _B.rgb);

			    half4 base = tex2D(_MainTex, i.uv);
				
				// ****************
			    half4 col  = lerp(base, half4(color, base.a), _Step.z);
			    half4 correction = tex2D(_GlowTex, float2(bright(col), 0));
				
			    float2 maskUV = i.uv * _GridSize;
				
			    half4 mask = tex2D(_MaskTex, maskUV);
				

			    col.rgba *= lerp(1, mask, _Data.z);
				col.rgb = lerp(col.rgb, correction.rgb, correction.a * luma(mask.rgb));
				//col.rgb = lerp(col.rgb, glow.rgb, glow.a * mask.a);

				//col.rgb = lerp(col.rgb, mask, _Data.w);
				
				// bleed
				half4 bleed = tex2D(_MainTex, i.uv + float2(-0.01, 0) * _Step.w).rgba;
				bleed += tex2D(_MainTex, i.uv + float2(-0.02, 0) * _Step.w).rgba;
				bleed += tex2D(_MainTex, i.uv + float2(-0.01, -0.01) * _Step.w).rgba;
				bleed += tex2D(_MainTex, i.uv + float2(-0.02, -0.02) * _Step.w).rgba;
				bleed /= 6;

				col += bleed * _Bleed.rgba * _Bleed.w; 
				
			    return col;
			}
			ENDHLSL
		}
	}
}