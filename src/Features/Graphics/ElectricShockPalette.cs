using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal static class ElectricShockPalette
{
    private static readonly Lazy<Color[,]> BackgroundData = new(() =>
        OracleGraphicsData.LoadPalette("res://assets/oracle/metadata/electric_shock_bg_palette.bin"));
    private static readonly Lazy<IReadOnlyDictionary<int, Color[]>> ObjectData = new(() =>
    {
        Color[,] palettes = OracleGraphicsData.LoadPalette(
            "res://assets/oracle/metadata/electric_shock_obj_palette.bin");
        var result = new Dictionary<int, Color[]>();
        for (int slot = 0; slot < 8; slot++)
        {
            var colors = new Color[4];
            for (int shade = 0; shade < 4; shade++) colors[shade] = palettes[slot, shade];
            colors[0] = Colors.Transparent;
            result.Add(slot, colors);
        }
        return result;
    });

    internal static Color[,] Background => BackgroundData.Value;
    internal static IReadOnlyDictionary<int, Color[]> Objects => ObjectData.Value;

    internal static ShaderMaterial CreateLinkMaterial()
    {
        // Link's atlas retains the exact standard OBJ 0/5 colors. Map those
        // three opaque color IDs to PALH_0c without altering atlas geometry.
        var shader = new Shader
        {
            Code = """
                shader_type canvas_item;
                uniform bool damage_palette;
                uniform vec4 shade1 : source_color;
                uniform vec4 shade2 : source_color;
                uniform vec4 shade3 : source_color;
                void fragment() {
                    vec4 pixel = texture(TEXTURE, UV);
                    vec3 a = damage_palette ? vec3(31.0,22.0,6.0)/31.0 : vec3(0.0);
                    vec3 b = damage_palette ? vec3(27.0,0.0,0.0)/31.0 : vec3(2.0,21.0,8.0)/31.0;
                    vec3 c = damage_palette ? vec3(0.0) : vec3(31.0,26.0,17.0)/31.0;
                    if (distance(pixel.rgb,a) < 0.005) pixel.rgb = damage_palette ? shade3.rgb : shade1.rgb;
                    else if (distance(pixel.rgb,b) < 0.005) pixel.rgb = shade2.rgb;
                    else if (distance(pixel.rgb,c) < 0.005) pixel.rgb = damage_palette ? shade1.rgb : shade3.rgb;
                    COLOR = pixel;
                }
                """
        };
        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("shade1", Objects[0][1]);
        material.SetShaderParameter("shade2", Objects[0][2]);
        material.SetShaderParameter("shade3", Objects[0][3]);
        return material;
    }
}
