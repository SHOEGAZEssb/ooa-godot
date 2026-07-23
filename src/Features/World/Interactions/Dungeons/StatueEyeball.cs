using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class StatueEyeball : TransitionOffsetNode2D
{
    private EnemyAnimationPlayer _animation = null!;
    private DungeonEntranceInteractionDatabase _data = null!;
    private bool _initialized;
    private int _direction = 4;

    internal int Direction => _direction;
    internal int AnimationIndex => _animation.AnimationIndex;
    internal bool Initialized => _initialized;
    internal ulong PixelHash => _animation is not null && _animation.HasFrames
        ? OracleGraphicsCache.PixelHash(_animation.CurrentTexture.GetImage())
        : 0;

    internal void Initialize(
        Vector2 position,
        DungeonEntranceInteractionDatabase data)
    {
        Name = "StatueEyeball";
        Position = position;
        ZIndex = NpcCharacter.FixedLowPriorityZIndex;
        Visible = false;
        _data = data;
        _animation = new EnemyAnimationPlayer(this, data.EyeVisuals.Count);
        var animations = new string[data.EyeVisuals.Count];
        for (int index = 0; index < data.EyeVisuals.Count; index++)
            animations[index] = data.EyeVisuals[index].Animation;
        DungeonEntranceInteractionDatabaseVisualRecord visual = data.EyeVisuals[0];
        Image image = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{visual.Sprite}.png");
        _animation.Load(image, animations, visual.TileBase, visual.Palette);
        _animation.SetAnimation(4);
        QueueRedraw();
    }

    internal void UpdateFrame(Player player)
    {
        if (!_initialized)
        {
            _initialized = true;
            Visible = true;
            return;
        }

        Vector2 tileCenter = new(
            Mathf.Floor(Position.X / OracleRoomData.MetatileSize) *
                OracleRoomData.MetatileSize + 8,
            Mathf.Floor(Position.Y / OracleRoomData.MetatileSize) *
                OracleRoomData.MetatileSize + 8);
        int angle = OracleObjectMath.AngleToward(tileCenter, player.Position);
        int low = angle & 0x07;
        if (low is not (0 or 1 or 7))
            angle = (angle & 0xfc) | 0x04;
        int direction = (angle >> 2) & 0x07;
        _direction = direction;
        DungeonEntranceInteractionDatabaseVisualRecord visual =
            _data.EyeVisuals[direction];
        Position = new Vector2(
            Mathf.Floor(tileCenter.X / 16.0f) * 16.0f + visual.LowX,
            Mathf.Floor(tileCenter.Y / 16.0f) * 16.0f + visual.LowY);

        // Subid $02 keeps interactionInitGraphics' default animation $04.
        // Direction is represented solely by moving that fixed eye around
        // the tile; selecting animations $00-$07 double-applies the OAM
        // offset and is only correct for subid $00.
    }

    public override void _Draw()
    {
        if (_animation is not null && _animation.HasFrames)
        {
            DrawTexture(
                _animation.CurrentTexture,
                new Vector2(-16, -16) + TransitionDrawOffset);
        }
    }
}
