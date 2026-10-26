using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

/// <summary>INTERAC_HARDHAT_WORKER $58:$03 patrol script.</summary>
internal sealed class BlackTowerPatrollingWorkerRoomEntity : BlackTowerNpcRoomEntity,
    IFixedRoomEntity, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly BlackTowerWorkerDatabase _data;
    private OracleObjectPosition _position;
    private bool _initialized;
    private int _patrolMode, _patrolCounter, _patrolDirection;
    internal BlackTowerPatrolScriptHost? Script { get; set; }
    internal OracleObjectPosition PrecisePosition => _position;
    internal int PatrolCounter => _patrolCounter;
    internal int PatrolDirection => _patrolDirection;
    internal int PatrolMode => _patrolMode;
    internal bool Initialized => _initialized;
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;

    internal BlackTowerPatrollingWorkerRoomEntity(NpcCharacter npc, BlackTowerWorkerDatabase data)
        : base(npc, npc.SetTransitionDrawOffset)
    {
        _data = data;
        npc.SetDirectionalAnimations(
            data.Visual("hardhat-0").Animation, data.Visual("hardhat-1").Animation,
            data.Visual("hardhat-2").Animation, data.Visual("hardhat-3").Animation);
        npc.ResetNativeNpcFacingState();
        npc.SetScriptButtonSensitive(false);
    }

    public override NpcCharacter? FindTalkTarget(Player player) =>
        _initialized && Script is { ButtonPending: false } && Entity.ScriptButtonSensitive &&
        Entity.CanTalkTo(player) ? Entity : null;

    internal void StartPatrolling() => _patrolMode = 1;

    internal void SetPatrolCounter(int value)
    {
        if (value is < 0 or > 0xff) throw new ArgumentOutOfRangeException(nameof(value));
        _patrolCounter = value;
    }

    internal void SetPatrolDirection(int direction)
    {
        if (direction is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(direction));
        _patrolDirection = direction;
        Entity.SetNativeAngle(direction * 8);
        RestorePatrolAnimation();
    }

    internal void RestorePatrolAnimation() =>
        Entity.SetScriptAnimation(_data.Visual($"hardhat-{_patrolDirection}").Animation);

    internal void DecrementPatrolCounter(OracleRuntimeState memory)
    {
        int old = _patrolCounter;
        _patrolCounter = (old - 1) & 0xff;
        // scriptHelp.hardhatWorker_decPatrolCounter -> writeFlagsTocddb.
        // The clean-US asm15 dispatch's rst table add leaves carry clear.
        int flags = 0x40 | (_patrolCounter == 0 ? 0x80 : 0) | ((old & 0x0f) == 0 ? 0x20 : 0);
        memory.SetWramByte(0xcddb, (byte)flags);
    }

    internal void Move(OracleRuntimeState memory)
    {
        _position = NativeObjectMovement.ApplySpeed(memory, _position, _data.Speed80, Entity.NativeAngle);
        Entity.Position = _position.PixelPosition;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.Active) return;
        var script = Script ?? throw new InvalidOperationException("Worker $58:$03 has no script owner.");
        if (!_initialized)
        {
            _initialized = true;
            _position = OracleObjectPosition.FromPixels(Entity.Position);
            Entity.Position = _position.PixelPosition;
            Entity.SetScriptAnimation(_data.Visual("hardhat-0").Animation);
            Entity.SetCollisionRadii(0, 0);
            Entity.SetDialogue(0x1000, string.Empty, canFace: true);
            script.Advance(frame.Player);
        }
        // hardhatWorker.s state0 calls runScript, then falls into state1 for
        // another eligible script visit before the single native tail.
        script.Advance(frame.Player);
        if (_patrolMode == 0) Entity.FaceLinkAndAnimateOneUpdate(frame.Player);
        else
        {
            Entity.AdvanceAnimationUpdates(1); // SPEED_80 is below the extra-animation threshold.
            Entity.PushPlayerAwayAndUpdateDrawPriority(frame.Player);
        }
    }
}
