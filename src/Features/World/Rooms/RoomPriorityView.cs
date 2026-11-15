namespace Oracles;

public partial class RoomPriorityView : Node2D
{
    public override void _Ready()
    {
        Material = new ShaderMaterial { Shader = new Shader { Code = """
            shader_type canvas_item;
            uniform vec4 background_fade : source_color;
            void fragment() {
                COLOR.rgb = mix(COLOR.rgb, background_fade.rgb, background_fade.a);
            }
            """ } };
    }

    public override void _Draw()
    {
        var room = GetParent<RoomView>();
        ((ShaderMaterial)Material).SetShaderParameter("background_fade", room.BackgroundFadeColor);
        room.DrawRoom(this, priority: true);
    }
}
