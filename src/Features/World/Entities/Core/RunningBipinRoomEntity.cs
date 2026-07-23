using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// INTERAC_BIPIN $28:$00 starts at SPEED_100/angle $18 and reverses whenever
// X leaves [$28,$58). Its var3a animation toggles between $04 and $05 at the
// same boundary update.
internal sealed class RunningBipinRoomEntity
    : RoomEntityAdapter<NpcCharacter>, IVariableRoomEntity, IFixedRoomEntity,
        IRoomBlocker, ITalkTarget
{
    private Vector2 _precisePosition;
    private int _angle = 0x18;
    private bool _alternateAnimation;

    public RunningBipinRoomEntity(NpcCharacter npc)
        : base(npc, npc.SetTransitionDrawOffset)
    {
        _precisePosition = npc.Position;
    }

    internal int Angle => _angle;
    internal Vector2 PrecisePosition => _precisePosition;

    public void Update(double delta, Player player) =>
        Entity.UpdateNpc(delta, player.Position);

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.Active)
            return;

        _precisePosition += OracleObjectMath.VectorFromAngle32(_angle);
        Entity.Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        float relativeX = Entity.Position.X - 0x28;
        if (relativeX < 0 || relativeX >= 0x30)
        {
            _angle ^= 0x10;
            _alternateAnimation = !_alternateAnimation;
            Entity.SetScriptAnimation(_alternateAnimation
                ? Entity.Record.RightAnimation
                : Entity.Record.DownAnimation);
        }

        // bipin.s calls objectPreventLinkFromPassing after objectApplySpeed,
        // so Bipin pushes Link to the nearest collision edge when his own
        // movement creates the overlap.
        Entity.PreventPlayerPassing(frame.Player);
        Entity.UpdateDrawPriority(frame.Player.Position);
    }

    public bool BlocksLink(Vector2 linkCenter) => Entity.BlocksLinkCenter(linkCenter);
    public NpcCharacter? FindTalkTarget(Player player) =>
        Entity.CanTalkTo(player) ? Entity : null;
}
