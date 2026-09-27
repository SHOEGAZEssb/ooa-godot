using Godot;
using System;

namespace oracleofages;

internal partial class ObjectTerrainShadow : Node2D
{
    private Func<bool> _drawn = null!;
    private Func<Vector2> _offset = null!;

    internal void Initialize(Func<bool> drawn, Func<Vector2> offset)
    {
        _drawn = drawn;
        _offset = offset;
        // queueDrawEverything appends terrain effects after every object.
        ZAsRelative = false;
        ZIndex = ObjectDrawPriority.TerrainShadowZIndex;
    }

    // Rendering samples the global update counter, including frozen actors.
    // Never advance an independent animation clock at the host frame rate.
    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        if (!_drawn()) return;
        TerrainShadowDefinition shadow = TerrainShadow.Load();
        DrawTexture(shadow.Texture, shadow.Offset + _offset());
    }
}
