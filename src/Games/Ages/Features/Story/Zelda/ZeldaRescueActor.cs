using Godot;
using System.Collections.Generic;

namespace Oracles;

// impaInCutscene.s:$31:$07, bird.s:$4c:$04 and zelda.s:$ad:$06.
internal sealed class ZeldaRescueActor : RoomCutsceneCommandHost, IRoomEntity, IFixedRoomEntity,
    IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity, IRoomBlocker, ITalkTarget
{
    public override RoomEventContext Context { get; }
    private readonly ZeldaRescueEvent _owner;
    private readonly ZeldaRescueDatabase _data;
    private readonly CutsceneCommandRunner _runner;
    private readonly ScriptActorBinding<NpcCharacter> _binding;
    private readonly string _name;
    private readonly NpcCharacter _actor;
    private Vector2 _precise;
    private int _z, _speedZ;
    internal int State { get; private set; }
    public bool Finished { get; private set; }
    public Node2D Node => _actor;
    public bool UpdatesDuringDialogue => State == 0 || _name == "Bird";
    public bool UpdatesDuringRoomEntityFreeze => State == 0 || _name == "Bird";
    internal ZeldaRescueActor(RoomEventContext context, ZeldaRescueEvent owner, ZeldaRescueDatabase data, NpcRecord record)
    {
        Context = context; _owner = owner; _data = data;
        _name = record.Id switch { 0x31 => "Impa", 0x4c => "Bird", 0xad => "Zelda", _ => throw new System.ArgumentOutOfRangeException(nameof(record)) };
        _actor = NpcCharacter.CreateFromRecord(record with { CanFace = record.Id == 0x31 });
        // Zelda's commonInit neither marks solidity nor installs an NPC hitbox.
        if (_name == "Zelda") _actor.TimeWarpSolidMarked = false;
        _actor.SetAnimationRate(0);
        _binding = new(_name); _binding.Bind(_actor);
        _actor.Retired += _ => _owner.ReleaseRetiredActor(this);
        _runner = new(this);
        _precise = _actor.Position;
    }
    private byte Read(int address) => Context.Entities.RuntimeState.ReadWramByte(address);
    private void Write(int address, int value) => Context.Entities.RuntimeState.SetWramByte(address, unchecked((byte)value));
    private bool Flag(int flag) => Context.Rooms.SaveData.HasGlobalFlag(flag);
    public void SetTransitionDrawOffset(Vector2 offset) => _actor.SetTransitionDrawOffset(offset);
    public bool BlocksLink(Vector2 center) => State != 0 && !Finished && _actor.BlocksLinkCenter(center);
    public NpcCharacter? FindTalkTarget(Player player) => !Finished && _actor.CanScriptTalkTo(player, 6, 6, 10) ? _actor : null;
    internal bool QueueButton(NpcCharacter actor) => !Finished && _binding.QueueButton(actor);
    internal void Cancel() { _runner.Clear(); _binding.Clear(); Finish(); }
    private void Finish()
    {
        Finished = true;
        // State-zero interactionDelete may have retired this node before the
        // room-entry owner cancels its retained script hosts on the next load.
        if (GodotObject.IsInstanceValid(_actor)) _actor.SetActive(false);
    }
    private void SetAnimation(int animation) => _actor.SetScriptAnimation(_data.Animation(_name, animation));
    private void Initialize()
    {
        if (State != 0) return;
        State = 1;
        if (_name != "Zelda")
        {
            if (!IsLinkedGame || (Context.Inventory.Essences & 4) == 0 || Flag(AgesGlobalFlag.GotRingFromZelda)) { Finish(); return; }
            bool moved = Flag(AgesGlobalFlag.ImpaMovedAfterZeldaKidnapped), saved = Flag(AgesGlobalFlag.ZeldaSavedFromVire);
            if (_name == "Impa")
            {
                SetAnimation(!moved ? 9 : 2);
                if (moved)
                {
                    _precise.X = saved ? 0x48 : 0x38;
                    if (saved)
                    {
                        _precise.Y = 0x58;
                        _owner.SetInput(false, initialization: true);
                        Write(AgesWramAddress.wScrollMode, 0);
                        for (int address = 0xcfd0; address < 0xcfe0; address++) Write(address, 0);
                        _owner.CreateZelda();
                    }
                }
                _actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex);
            }
            else
            {
                if (moved) { if (saved) _precise.Y = 0x58; else _precise.X = 0x68; }
                SetAnimation(0); _speedZ = -0xc0;
            }
        }
        else SetAnimation(_actor.Record.DefaultAnimation);
        _actor.SetStatePosition(_precise);
        _actor.SetBlocksLink(_name != "Zelda");
        _actor.ShowInitializedPresentation();
        _runner.Start(_data.Commands(_name));
        // Zelda state zero falls through state one; the other two return.
        if (_name == "Zelda") AdvanceNative();
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Initialize();
        return Finished ? ScreenTransitionPresentation.Hidden : ScreenTransitionPresentation.Visible;
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        if (State == 0) { Initialize(); return; }
        AdvanceNative();
    }
    private void AdvanceNative()
    {
        if (_name == "Zelda") { _actor.AdvanceAnimationUpdates(1); _runner.AdvanceFrame(); return; }
        if (_name == "Bird") _actor.AnimateAsNpcOneUpdate(Context.Player);
        bool landed = OracleObjectMath.UpdateSpeedZ(ref _z, ref _speedZ, 0x20);
        if (_name == "Bird" && landed) _speedZ = -0xc0;
        _actor.SetScriptDrawOffset(Vector2.Down * (_z >> 8));
        _runner.AdvanceFrame();
        if (Finished) return;
        if (_name == "Bird")
        {
            if (Flag(AgesGlobalFlag.ImpaMovedAfterZeldaKidnapped) && !Flag(AgesGlobalFlag.ZeldaSavedFromVire) && (int)_precise.X != 0x68)
            { _precise.X = ((int)_precise.X + 1) & 255; _actor.SetStatePosition(_precise); }
        }
        else if (!Flag(AgesGlobalFlag.ImpaMovedAfterZeldaKidnapped)) { _actor.ClearFixedDrawPriority(); _actor.AnimateAsNpcOneUpdate(Context.Player); }
        else if (Flag(AgesGlobalFlag.ZeldaSavedFromVire)) _actor.AdvanceAnimationUpdates(1);
        else
        {
            _actor.ClearFixedDrawPriority();
            int previousAngle = _actor.NativeAngle;
            _actor.FaceLinkAndAnimateOneUpdate(Context.Player);
            if (_actor.NativeAngle != previousAngle)
            {
                // The native helper installs an animation and then animates it
                // once. Keep the positioned OAM stream selected by that write.
                SetAnimation(_actor.NativeAngle >> 3);
                _actor.AdvanceAnimationUpdates(1);
            }
        }
    }
    public override bool HasActorBinding(CutsceneActorId actor) => _binding.Matches(actor);
    public override bool TryConsumeActorButton(CutsceneActorId actor)
    {
        bool pressed = _binding.ConsumeButton();
        if (pressed) Context.Player.ApplyInteractionInvincibility(0x80);
        return pressed;
    }
    public override void SetActorButtonSensitive(string actor) => _binding.EnableButton();
    public override void InitializeActorCollisionRadii(string actor) => _actor.InitializeCollisionRadii();
    public override void SetInputEnabled(bool enabled) => _owner.SetInput(enabled);
    public override void ShowText(int id, string message, int? position) => Context.ShowDialogue(message, position, sourceTextId: id);
    public override void SetActorAnimation(string actor, int animation, string encodedAnimation) => _actor.SetScriptAnimation(encodedAnimation);
    public override void SetActorMovementAnimation(string actor, int angle, string animation)
    { _actor.SetNativeAngle(angle); _actor.SetScriptAnimation(animation); }
    public override void MoveActorAtSpeed(string actor, int speed, int angle)
    { _actor.SetNativeAngle(angle); _actor.SetStatePosition(NativeObjectMovement.ApplySpeed(Context.Entities.RuntimeState, ref _precise, speed, angle)); }
    public override int ReadMemory(string binding) => binding switch
    {
        "RescueSignal" => Read(0xcfd0), "ZHigh" => (_z >> 8) & 255,
        "LinkState" => Context.Player.NativeForcedMovementActive ? 0x0b : 1,
        _ when binding.StartsWith("Flag:", System.StringComparison.Ordinal) => Flag(System.Convert.ToInt32(binding[5..], 16)) ? 1 : 0,
        _ => throw UnsupportedCommand(binding)
    };
    public override bool MemoryEquals(string binding, int value) => ReadMemory(binding) == value;
    public override void WriteMemory(string binding, int value)
    {
        switch (binding)
        {
            case "RescueSignal": Write(0xcfd0, value); return;
            case "ScrollMode": Write(AgesWramAddress.wScrollMode, value); return;
            case "LinkDirection": Context.Player.Face(value switch { 0 => Vector2I.Up, 1 => Vector2I.Right, 3 => Vector2I.Left, _ => throw UnsupportedCommand(binding) }); return;
            case "LinkX": Context.Player.SetScriptedCoordinateHigh(horizontal: true, value); return;
            default: throw UnsupportedCommand(binding);
        }
    }
    public override bool GateOpen(string gate) => gate == "PaletteDone" ? !Context.Transitions.NativePaletteFadeActive : throw UnsupportedCommand(gate);
    public override void RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "FaceLink":
                int direction = ((OracleObjectMovement.Shared.RelativeAngle(_actor.Position, Context.Player.Position) + 4) & 0x18) >> 3;
                SetAnimation(direction); return;
            case "MoveLinkUp": Context.Player.RequestNativeForcedMovement(0x20, 0, 0, _owner); return;
            case "MoveLinkRight": Context.Player.RequestNativeForcedMovement(8, 8, 1, _owner); return;
            case "BeginJump": _speedZ = -0x200; return;
            default: throw UnsupportedCommand(handler);
        }
    }
    public override void ScriptEnded() { if (_name != "Zelda") Finish(); }
}
