using Godot;
using System.Collections.Generic;

namespace oracleofages;

// object_code/ages/interactions/zelda.s:$ad:$00. This slot, rather than
// the room-event tick, dispatches the script after native animation.
internal sealed class RoomOfRitesZeldaActor : RoomCutsceneCommandHost, IRoomEntity,
    IFixedRoomEntity, IRoomEntityLifetime, IScreenTransitionPreloadRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, ITalkTarget
{
    public override RoomEventContext Context { get; }
    private readonly NpcCharacter _actor;
    private readonly ScriptActorBinding<NpcCharacter> _binding = new("Zelda");
    private readonly CutsceneCommandRunner _runner;
    private bool _initialized, _ownsMask, _ownsTransitions, _ownsText;
    private readonly IReadOnlyList<CutsceneCommand> _commands;
    private readonly string _extraSprite;
    public Node2D Node => _actor;
    public void SetTransitionDrawOffset(Vector2 offset) => _actor.SetTransitionDrawOffset(offset);
    public bool Finished { get; private set; }
    internal bool Visible { get => _actor.Visible; set => _actor.Visible = value; }
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;

    internal RoomOfRitesZeldaActor(RoomEventContext context, NpcRecord record)
    {
        Context = context;
        _actor = NpcCharacter.CreateFromRecord(record with { CanFace = false });
        _actor.SetAnimationRate(0);
        _actor.SetBlocksLink(false);
        _actor.SetCollisionRadii(0, 0);
        _actor.TimeWarpSolidMarked = false;
        _binding.Bind(_actor);
        _runner = new(this);
        _commands = CutsceneCommandCatalog.Load("res://assets/oracle/cutscenes/room_of_rites_zelda_commands.tsv");
        _extraSprite = GeneratedTable.Load("res://assets/oracle/cutscenes/room_of_rites_zelda_graphics.tsv",
            new GeneratedTableSchema("Room of Rites Zelda graphics", GeneratedTableKeySemantics.Unique,
                ["extra-sprite", "source"], ["extra-sprite"], headerRequired: true)).Rows[0].RequiredString(0);
    }

    private void Initialize()
    {
        if (_initialized || Finished) return;
        _initialized = true;
        if (Context.Rooms.SaveData.HasRoomFlag(_actor.Record.Group, _actor.Record.Room, 0x80))
        {
            Write(WramAddress.wDisableScreenTransitions, 1);
            _ownsTransitions = true;
            // @initSubid00 zeros wActiveMusic before requesting $38. Queue
            // the native request without restoring the room-selection byte.
            if (Context.Sound.NativeActiveMusic != 0)
            {
                Context.Sound.SetNativeActiveMusic(0);
                Context.Sound.PlayNativeSound(SoundId.MusZeldaSaved);
            }
        }
        _actor.AppendScriptGraphics(_extraSprite);
        Write(WramAddress.wInteractionIDToLoadExtraGfx, InteractionId.Zelda);
        Write(WramAddress.wLoadedTreeGfxIndex, InteractionId.Zelda);
        _actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex);
        _runner.Start(_commands);
        // zelda_state0 falls through zelda_state1's animate/runScript. This
        // also establishes script animation $05 before the first scroll draw.
        AdvanceNative();
    }

    private void Write(int address, int value) => Context.Entities.RuntimeState.SetWramByte(address, (byte)value);
    private void AdvanceNative() { _actor.AdvanceAnimationUpdates(1); _runner.AdvanceFrame(); }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        if (!_initialized) { Initialize(); return; }
        AdvanceNative();
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Initialize();
        return ScreenTransitionPresentation.Visible;
    }
    public NpcCharacter? FindTalkTarget(Player player) => !Finished && _actor.CanScriptTalkTo(player,
        _actor.ObjectCollisionBounds.Size.Y / 2, _actor.ObjectCollisionBounds.Size.X / 2, 10) ? _actor : null;
    internal bool QueueButton(NpcCharacter actor) => !Finished && _binding.QueueButton(actor);
    public override bool HasActorBinding(CutsceneActorId actor) => _binding.Matches(actor);
    public override void SetActorAnimation(string actor, int animation, string encodedAnimation) =>
        _binding.Require(actor).SetScriptAnimation(encodedAnimation);
    public override void SetActorCollisionRadii(string actor, int radiusY, int radiusX) =>
        _binding.Require(actor).SetCollisionRadii(radiusY, radiusX);
    public override void SetActorButtonSensitive(string actor) => _binding.EnableButton();
    public override bool TryConsumeActorButton(CutsceneActorId actor)
    {
        bool pressed = _binding.ConsumeButton();
        if (pressed) Context.Player.ApplyInteractionInvincibility(0x80);
        return pressed;
    }
    public override void SetDisabledObjects(int value)
    {
        Write(WramAddress.wDisabledObjects, value);
        _ownsMask = value == 0x11;
    }
    public override void ShowText(int textId, string message, int? textboxPosition)
    {
        Context.ShowDialogue(message, textboxPosition, sourceTextId: textId);
        _ownsText = true;
    }
    public override void RunNativeHandler(string handler)
    {
        if(handler!="RoomOfRitesCollapse")throw UnsupportedCommand(handler);
        if(Context.Entities.RoomOfRitesCollapse is not { } begin)throw UnsupportedCommand("CUTSCENE $0f has no ending owner");
        begin();
    }
    public override void ScriptEnded() { } // Native Zelda remains alive after scriptend.
    internal void Cancel()
    {
        _runner.Clear();
        _binding.Clear();
        if (_ownsText && Context.DialogueOpen) Context.CloseDialogue();
        if (_ownsMask && Context.Entities.RuntimeState.ReadWramByte(WramAddress.wDisabledObjects) == 0x11)
            Write(WramAddress.wDisabledObjects, 0);
        if (_ownsTransitions && Context.Entities.RuntimeState.ReadWramByte(WramAddress.wDisableScreenTransitions) == 1)
            Write(WramAddress.wDisableScreenTransitions, 0);
        _ownsMask = _ownsTransitions = _ownsText = false;
        Finished = true;
        if (GodotObject.IsInstanceValid(_actor)) _actor.SetActive(false);
    }
}
