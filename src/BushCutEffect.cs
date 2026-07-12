using Godot;

namespace oracleofages;

public partial class BushCutEffect : Node2D
{
    private float _time;

    public override void _Process(double delta)
    {
        _time += (float)delta;
        if (_time >= 0.36f)
            QueueFree();
        else
            QueueRedraw();
    }

    public override void _Draw()
    {
        Color darkLeaf = Color.Color8(29, 92, 48);
        Color lightLeaf = Color.Color8(91, 170, 73);
        float distance = 3.0f + _time * 24.0f;
        float rise = _time * 18.0f;
        DrawRect(new Rect2(-distance - 2, -rise - 2, 3, 3), darkLeaf);
        DrawRect(new Rect2(distance - 1, -rise - 1, 3, 3), lightLeaf);
        DrawRect(new Rect2(-distance, rise - 1, 3, 3), lightLeaf);
        DrawRect(new Rect2(distance - 2, rise, 3, 3), darkLeaf);
    }
}
