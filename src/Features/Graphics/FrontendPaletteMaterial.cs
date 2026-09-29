using Godot;
using System;

namespace oracleofages;

/// <summary>RGB5 fade followed by the clean ROM's GBA-mode palette conversion.</summary>
internal static class FrontendPaletteMaterial
{
    internal static ShaderMaterial Create()
    {
        byte[] components = OracleGraphicsData.ReadBytes(
            "res://assets/oracle/metadata/gba_palette_components.bin", 32);
        var material = new ShaderMaterial { Shader = new Shader { Code = """
            shader_type canvas_item;
            uniform float fade_offset = 0.0;
            uniform int gba_components[32];
            void fragment() {
                vec4 pixel = texture(TEXTURE, UV) * COLOR;
                // Restore compatibility canvas transfer before quantizing the
                // source RGB5 components. Brightening follows the fade, as in
                // loadGraphics.s:refreshDirtyPalettes/@gbaBrightenPalette.
                ivec3 component = ivec3(min(floor(sqrt(pixel.rgb) * 31.0 + 0.5)
                    + vec3(fade_offset), vec3(31.0)));
                vec3 bright = vec3(ivec3(gba_components[component.r],
                    gba_components[component.g], gba_components[component.b]));
                pixel.rgb = floor(bright * 255.0 / 31.0) / 255.0;
                COLOR = pixel;
            }
            """ } };
        material.SetShaderParameter("gba_components", Array.ConvertAll(components, value => (int)value));
        return material;
    }
}
