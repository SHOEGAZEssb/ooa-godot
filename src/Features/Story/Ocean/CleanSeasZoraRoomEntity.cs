using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// zora.s:$0a/$0b native counters and $0c/$0d script/vertical-physics gate.
internal sealed class CleanSeasZoraRoomEntity : CutsceneCommandHost, IRoomEntity,
    IFixedRoomEntity, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly NpcCharacter _actor;
    private readonly SeaFairyDatabase _data;
    private readonly OracleRuntimeState _runtime;
    private readonly CutsceneCommandRunner _runner;
    private readonly Func<bool> _scriptTextActive;
    private readonly int _subid;
    private Vector2 _position;
    private int _state, _counter, _counter2, _phase, _z, _speedZ;
    private ICollection<RoomEntitySpawn>? _spawns;
    public Node2D Node => _actor;
    public override bool DialogueOpen => _scriptTextActive();
    internal CleanSeasZoraRoomEntity(CleanSeasZoraSpawn spawn, OracleRuntimeState runtime, Func<bool> scriptTextActive)
    {
        _data = spawn.Data; _subid = spawn.SubId; _position = spawn.Position; _runtime = runtime; _scriptTextActive = scriptTextActive;
        var record = _data.Visual("zora", _position, 0) with { SubId = _subid };
        _actor = NpcCharacter.CreateFromRecord(record);
        _actor.SetBlocksLink(false); _actor.TimeWarpSolidMarked = false;
        _runner = new(this);
        if (_subid >= 0x0c)
        {
            _runner.Start(_data.ZoraCommands(_subid));
            _runner.SetInitialMotionRegisters(new("Zora"), 0x3c, 0);
        }
    }
    public void SetTransitionDrawOffset(Vector2 offset) => _actor.SetTransitionDrawOffset(offset);
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        _spawns = spawns;
        if (_state == 0)
        {
            _state = 1; _counter = 30;
            _actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex); return;
        }
        if (_subid >= 0x0c)
        {
            if (!UpdateZ()) return;
            // zora_commonInit sets enabled bit7: physics and animation keep
            // running, while interactionRunScript reads live text/death gates.
            if (!frame.Player.IsDying && !ScriptExecutionBlocked)
            {
                _runner.AdvanceFrame();
                if (!_runner.Active) SetSignal();
            }
            _actor.AdvanceAnimationUpdates(1); return;
        }
        switch (_state)
        {
            case 1:
                if (--_counter != 0) return;
                _counter = 120; _counter2 = 1; _state = 2; return;
            case 2:
                if (--_counter == 0) { _counter = 90; _state = 3; SetAnimation(2); return; }
                if (--_counter2 != 0) return;
                _counter2 = 20; _phase = (_phase + 1) & 7;
                int[] table = [0,3,1,2,0,1,3,2,3,1,3,0,3,2,0,1];
                SetAnimation(table[_phase + (_subid - 0x0a) * 8]); return;
            case 3:
                _counter = (_counter - 1) & 255;
                if (_counter == 0) SetSignal();
                if (UpdateZ()) _speedZ = -0x180;
                return;
        }
    }
    private bool UpdateZ()
    {
        bool grounded = OracleObjectMath.UpdateSpeedZ(ref _z, ref _speedZ, 0x20);
        _actor.SetScriptDrawOffset(new(0, _z >> 8));
        return grounded;
    }
    private void SetSignal() => _runtime.SetWramByte(WramAddress.wTmpcfc0,
        (byte)(_runtime.ReadWramByte(WramAddress.wTmpcfc0) | 0x80));
    private void SetAnimation(int animation) => _actor.SetScriptAnimation(_data.Visual("zora", _position, animation).DownAnimation);
    public override bool HasActorBinding(CutsceneActorId actor) => actor.Value == "Zora";
    public override void SetActorAnimation(string actor, int animation, string encodedAnimation) => _actor.SetScriptAnimation(encodedAnimation);
    public override void SetActorMovementAnimation(string actor, int angle, string animation) => _actor.SetScriptAnimation(animation);
    public override void MoveActorAtSpeed(string actor, int speed, int angle) =>
        _actor.Position = NativeObjectMovement.ApplySpeed(_runtime, ref _position, speed, angle);
    public override void RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "zora_createExclamationMark": _spawns!.Add(new ExclamationMarkSpawn(_actor.Position + new Vector2(0, -14), false, 30)); break;
            case "zora_beginJump": _speedZ = -0x100; break;
            case "ToggleSignal": _runtime.SetWramByte(WramAddress.wTmpcfc0,
                (byte)(_runtime.ReadWramByte(WramAddress.wTmpcfc0) ^ 0x80)); break;
            default: throw UnsupportedCommand(handler);
        }
    }
    public override void ScriptEnded() { }
}

internal sealed record CleanSeasZoraSpawn(SeaFairyDatabase Data, Vector2 Position, int SubId) : RoomEntitySpawn;
