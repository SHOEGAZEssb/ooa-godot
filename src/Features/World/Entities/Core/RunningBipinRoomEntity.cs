using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// INTERAC_BIPIN $28:$00 consumes the imported @bipin0/@updateSpeed native
// record, including its raw speed, angle, X interval, and var3a toggle.
internal sealed class RunningBipinRoomEntity
    : NpcCharacterRoomEntityAdapter, IVariableRoomEntity, IFixedRoomEntity,
        IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity
{
    private readonly RunningBipinRecord _data;
    private readonly OracleRuntimeState _runtime;
    private Vector2 _precisePosition;
    private int _angle;
    private int _animation;

    public RunningBipinRoomEntity(NpcCharacter npc, RunningBipinRecord data, OracleRuntimeState runtime)
        : base(npc, npc.SetTransitionDrawOffset)
    {
        _data = data;
        _runtime = runtime;
        _precisePosition = npc.Position;
        _angle = data.InitialAngle;
        _animation = data.InitialAnimation;
    }

    public NpcCharacter Npc => Entity;
    internal FamilyNpcScriptHost? Script { get; set; }

    public void Update(double delta, Player player) =>
        Entity.UpdateNpc(delta, player.Position);

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.Active)
            return;

        Entity.Position = NativeObjectMovement.ApplySpeed(
            _runtime, ref _precisePosition, _data.SpeedRaw, _angle);
        float relativeX = Entity.Position.X - _data.MinimumX;
        if (relativeX < 0 || relativeX >= _data.SpanX)
        {
            _angle ^= _data.ReverseAngleXor;
            _animation ^= _data.AnimationToggleXor;
            Entity.SetScriptAnimation(
                _animation == _data.InitialAnimation
                    ? _data.InitialAnimationData
                    : _data.AlternateAnimationData);
        }

        Script?.Advance(frame.Player);
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
