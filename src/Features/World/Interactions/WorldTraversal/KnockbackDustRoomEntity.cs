using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// INTERAC_FALLDOWNHOLE $0f:$01: stationary animation1, visible80, then
// toggle visibility before testing the terminal animation parameter.
internal sealed partial class KnockbackDustRoomEntity : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly EnemyAnimationPlayer _animation;
    private readonly int _z;
    private bool _initialized;
    public Node2D Node => this;
    public bool Finished { get; private set; }
    internal int AnimationParameter => _initialized ? _animation.CurrentParameter : 0;
    internal int ElapsedUpdates { get; private set; }

    internal KnockbackDustRoomEntity(Vector2 position, int z)
    {
        Position = OracleObjectMath.ToPixelPosition(position);
        _z = unchecked((sbyte)z);
        Visible = false;
        ZIndex = NpcCharacter.FixedHighPriorityZIndex;
        var table = GeneratedTable.Load("res://assets/oracle/effects/knockback_dust.tsv",
            new GeneratedTableSchema("INTERAC$0f:$01", GeneratedTableKeySemantics.Ordered,
                ["tile-base", "palette", "animation"], headerRequired: true));
        GeneratedTableRow row = table.SingleRow();
        _animation = new EnemyAnimationPlayer(this, 1);
        _animation.Load(OracleGraphicsCache.LoadImage("res://assets/oracle/gfx/spr_common_sprites.png"),
            [row.RequiredString(2)], row.UnsignedDecimal(0), row.UnsignedDecimal(1));
        _animation.SetAnimation(0);
    }

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Advance();
    public void UpdateDuringScreenTransition(RoomEntityFrame frame) => Advance();
    private void Advance()
    {
        if (Finished) return;
        ElapsedUpdates++;
        if (!_initialized) { _initialized = true; Visible = true; QueueRedraw(); return; }
        Visible = !Visible;
        if ((AnimationParameter & 0x80) != 0) { Finished = true; Visible = false; return; }
        _animation.Advance();
    }
    public override void _Draw()
    {
        if (Visible && !Finished)
            DrawTexture(_animation.CurrentTexture, new Vector2(-16, -16 + _z) + TransitionDrawOffset);
    }
}
