using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class BlackTowerConfrontationActor : RoomCutsceneCommandHost, IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IRoomBlocker, ITalkTarget
{
    private readonly BlackTowerConfrontationDatabase _data;
    private readonly BlackTowerConfrontationEvent _owner;
    private readonly NpcCharacter _actor;
    private readonly CutsceneCommandRunner _runner;
    private Vector2 _precise;
    private int _z, _speedZ, _var3f;
    private bool _pressed, _ended;
    public override RoomEventContext Context { get; }
    public Node2D Node => _actor;
    internal int Id { get; }
    internal int State { get; private set; }
    internal int ZFixed => _z;
    internal Vector2 PrecisePosition => _precise;
    internal CutsceneCommandRunner Runner => _runner;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => State == 0;
    public bool UpdatesDuringRoomEntityFreeze => State == 0;
    internal BlackTowerConfrontationActor(RoomEventContext context, BlackTowerConfrontationDatabase data,
        BlackTowerConfrontationEvent owner, int id, Vector2 position)
    {
        Context = context; _data = data; _owner = owner; Id = id; _precise = position;
        _actor = data.Actor(id, position); _runner = new(this);
    }
    public bool BlocksLink(Vector2 center) => Id == 0x37 && !Finished && _actor.BlocksLinkCenter(center);
    public NpcCharacter? FindTalkTarget(Player player) => !Finished && Id == 0x37 && _actor.ScriptButtonSensitive && _actor.CanTalkTo(player) ? _actor : null;
    internal void PressButton() => _pressed = true;
    public void SetTransitionDrawOffset(Vector2 offset) => _actor.SetTransitionDrawOffset(offset);
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        bool initializing = State == 0;
        if (State == 0)
        {
            State = 1;
            if (Id == 0x37)
            {
                if (Context.Rooms.SaveData.HasRoomFlag(4, 0xfc, 0x80)) { Finish(); return; }
                _owner.SpawnSword(this);
                Write(0xcfde, 0); Write(0xcfdf, 0); Animation(0);
                _runner.Start(_data.Ralph); _runner.AdvanceFrame();
            }
            else { _actor.SetFixedDrawPriority(ObjectDrawPriority.FixedLowPriorityZIndex); _actor.ShowInitializedPresentation(); _runner.Start(_data.Ambi); return; }
        }
        _runner.AdvanceFrame();
        if (_ended) { Finish(); return; }
        if (Id == 0x37 || (Read(0xcfc0) & 2) == 0) _actor.AdvanceAnimationUpdates(1);
        _actor.SetScriptDrawOffset(Vector2.Down * (_z >> 8));
        if (Id == 0x37)
        {
            _actor.PreventPlayerPassing(frame.Player);
            _actor.UpdateDrawPriority(frame.Player.Position, _z >> 8, withTerrainEffects: true);
            // ralphState0 marks solidity after both initial script dispatches.
            // The room-$40 branch has already selected the collapsed position;
            // subsequent script motion does not move this reservation.
            if (initializing) _actor.SetStatePosition(_actor.Position);
        }
    }
    private byte Read(int address) => Context.Entities.RuntimeState.ReadWramByte(address);
    private void Write(int address, int value) => Context.Entities.RuntimeState.SetWramByte(address, unchecked((byte)value));
    private void Animation(int index) => _actor.SetScriptAnimation(_data.Animation(Id, index));
    private void Finish() { Finished = true; _actor.SetActive(false); }
    private void Flags(int value) => Write(0xcddb, value);
    public override bool HasActorBinding(CutsceneActorId actor) => actor.Value == "Actor";
    public override void SetInputEnabled(bool enabled) => _owner.Input(enabled);
    public override void InitializeActorCollisionRadii(string actor) => _actor.InitializeCollisionRadii();
    public override void SetActorButtonSensitive(string actor) => _actor.SetScriptButtonSensitive(true);
    public override bool TryConsumeActorButton(CutsceneActorId actor)
    { _actor.SetScriptButtonSensitive(true); bool pressed = _pressed; _pressed = false; return pressed; }
    public override bool RoomFlagSet(int flag) => Context.Rooms.SaveData.HasRoomFlag(4, 0xfe, (byte)flag);
    public override void OrRoomFlag(int flag) => Context.Rooms.SaveData.SetRoomFlag(4, 0xfe, (byte)flag);
    public override bool GateOpen(string gate) => gate == "PaletteDone" ? !Context.Transitions.NativePaletteFadeActive : throw UnsupportedCommand(gate);
    public override int ReadMemory(string binding)
    {
        string[] parts = binding.Split(':');
        return parts[0] switch { "Bit" => (Read(0xcfc0) >> int.Parse(parts[1])) & 1,
            "Flag" => (Read(0xcddb) & Convert.ToInt32(parts[1], 16)) != 0 ? 1 : 0, _ => throw UnsupportedCommand(binding) };
    }
    public override bool MemoryEquals(string binding, int value) => ReadMemory(binding) == value;
    public override void SetActorAnimation(string actor, int index, string encodedAnimation) => _actor.SetScriptAnimation(encodedAnimation);
    public override void SetActorMovementAnimation(string actor, int angle, string encodedAnimation) => _actor.SetScriptAnimation(encodedAnimation);
    public override void SetActorCoordinates(string actor, int y, int x)
    { _precise = new Vector2(x, y) + new Vector2(_precise.X % 1, _precise.Y % 1); _actor.Position = _precise.Floor(); }
    public override void MoveActorAtSpeed(string actor, int speed, int angle)
    { _actor.Position = NativeObjectMovement.ApplySpeed(Context.Entities.RuntimeState, ref _precise, speed, angle); }
    public override void WriteObjectByte(string actor, int address, int value)
    { if (address == 0x7f) _var3f = value; else throw UnsupportedCommand($"object ${address:x2}"); }
    public override void ShowText(int id, string message, int? position) => Context.ShowDialogue(message, position, sourceTextId: id);
    public override void SetMusic(int music) { Context.Sound.SetNativeActiveMusic(music); Context.Sound.PlaySound(music); }
    public override void ScriptEnded() => _ended = true;
    public override void RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "SpawnAmbi": _owner.SpawnAmbi(); return;
            case "ResetMusic": Context.Sound.PlayRoomMusic(4, 0xfe, Context.Rooms.SaveData); return;
            case "Jump": _speedZ = -0x400; Context.Sound.PlaySound(SoundId.SndJump); return;
            case "Move": MoveActorAtSpeed("Actor", _runner.ActorSpeed(new("Actor")), _runner.ActorAngle(new("Actor"))); return;
            case "Gravity": Flags(OracleObjectMath.UpdateSpeedZ(ref _z, ref _speedZ, 0xc0) ? 0x80 : 0); return;
            case "Flash": Flash(); return;
            case "FadeIn": _owner.Fade(false, 4); return;
            case "FaceLink":
                int direction = (OracleObjectMovement.Shared.RelativeAngle(_actor.Position, Context.Player.Position) + 4) & 0x18;
                Animation(direction >> 3); return;
            case "Flicker": _actor.SetScriptVisible((FrameCounter & 1) != 0); return;
            case "Decrement": _var3f = (_var3f - 1) & 255; Flags(_var3f == 0 ? 0x80 : 0); return;
            case "Rise": _z = unchecked((short)(_z - 0x400)); Flags((_z >> 8) == -0x40 ? 0x80 : (_z >> 8) < -0x40 ? 0x10 : 0); return;
            default:
                if (handler.StartsWith("Toggle:", StringComparison.Ordinal)) { Write(0xcfc0, Read(0xcfc0) ^ (1 << int.Parse(handler.AsSpan(7)))); return; }
                throw UnsupportedCommand(handler);
        }
    }
    private void Flash()
    {
        int state = Read(0xcfde), flags = 0;
        switch (state)
        {
            case 0: Write(0xcfdf, 10); _owner.ClearPalettes(); Write(0xcfde, 1); break;
            case 1 or 2:
                Write(0xcfdf, Read(0xcfdf) - 1);
                if (Read(0xcfdf) == 0) { Write(0xcfdf, 10); _owner.Fade(true, 1, 3); Write(0xcfde, state + 1); } break;
            case 3: Write(0xcfdf, 20); _owner.ClearPalettes(); Write(0xcfde, 4); break;
            case 4: Write(0xcfdf, Read(0xcfdf) - 1); if (Read(0xcfdf) == 0) flags = 0x80; break;
            default: throw UnsupportedCommand($"ralph_flashScreen state ${state:x2}");
        }
        Flags(flags);
    }
}
