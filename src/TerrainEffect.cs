using Godot;

namespace oracleofages;

public partial class TerrainEffect : Node2D
{
    private OracleRoomData.HazardType _hazard;
    private float _time;

    public void Initialize(OracleRoomData.HazardType hazard)
    {
        _hazard = hazard;
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        if (_time >= 0.45f)
            QueueFree();
        else
            QueueRedraw();
    }

    public override void _Draw()
    {
        float radius = 2.0f + _time * 20.0f;
        if (_hazard == OracleRoomData.HazardType.Hole)
        {
            Color shadow = Color.Color8(20, 24, 20);
            DrawRect(new Rect2(-5 + _time * 4.0f, -3 + _time * 3.0f, 10 - _time * 8.0f, 6 - _time * 5.0f), shadow);
            return;
        }

        Color dark = _hazard == OracleRoomData.HazardType.Lava
            ? Color.Color8(192, 56, 32)
            : Color.Color8(38, 91, 164);
        Color light = _hazard == OracleRoomData.HazardType.Lava
            ? Color.Color8(255, 180, 82)
            : Color.Color8(155, 207, 238);
        DrawArc(Vector2.Zero, radius, 0.0f, Mathf.Tau, 16, dark, 1.0f);
        DrawRect(new Rect2(-radius * 0.45f, -2.0f - _time * 10.0f, 2.0f, 2.0f), light);
        DrawRect(new Rect2(radius * 0.35f, -1.0f - _time * 8.0f, 2.0f, 2.0f), light);
    }
}
