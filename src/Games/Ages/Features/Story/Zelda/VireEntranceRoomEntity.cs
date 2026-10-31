using Godot;
using System.Collections.Generic;

namespace Oracles;

// vire.s:$b8:$00. The entrance actor retains its slot through its script.
internal sealed class VireEntranceRoomEntity : RoomCutsceneCommandHost, INativeInteractionRoomEntity,
    IFixedRoomEntity, IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity,
    IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    public override RoomEventContext Context { get; }
    private readonly VireEntranceDatabase _data;
    private readonly NpcCharacter _actor;
    private readonly CutsceneCommandRunner _runner;
    private Vector2 _precise;
    private bool _running;
    internal int State { get; private set; }
    internal int Counter => _runner.NativeCounter1;
    public bool Finished { get; private set; }
    public Node2D PresentationNode => _actor;
    public bool UpdatesDuringDialogue => State == 0;
    public bool UpdatesDuringRoomEntityFreeze => State == 0;
    internal VireEntranceRoomEntity(RoomEventContext context, VireEntranceDatabase data)
    {
        Context = context; _data = data; _runner = new(this);
        _actor = NpcCharacter.CreateFromRecord(data.Record, AgesGameContent.AssetRoot);
        _precise = _actor.Position; _actor.SetAnimationRate(0); _actor.SetScriptVisible(false);
        _actor.Retired += _ => { _runner.Clear(); Finished = true; ReleaseInputControl(); };
    }
    public void SetTransitionDrawOffset(Vector2 offset) => _actor.SetTransitionDrawOffset(offset);
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { if (State == 0 && !Finished) Initialize(); return Finished ? ScreenTransitionPresentation.Hidden : ScreenTransitionPresentation.Visible; }
    private void Initialize()
    {
        if (Context.Rooms.SaveData.HasRoomFlag(_data.Record.Group, _data.Record.Room, 0x40))
        { Finished = true; return; }
        Context.Sound.PlayNativeSound(_data.Sound);
        _runner.Start(_data.Commands); _runner.SetInitialMotionRegisters("Vire", _data.Speed, 0);
        State = 1; Context.Entities.RuntimeState.SetWramByte(0xcfd0, 0);
        _actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex);
        _actor.ShowInitializedPresentation();
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        if (State == 0) { Initialize(); return; }
        if (!_running && Context.Entities.RuntimeState.ReadField("wLinkInAir") == 0 && Context.Player.NativePuzzleResetVulnerable)
        {
            SetDisabledObjects(0x80); SetNativeMenuDisabled(0x80); // DISABLE_ALL_BUT_INTERACTIONS.
            Context.Player.ClearNativeItemParents(); Context.Player.DropHeldItemsForScript();
            Context.Player.Face(Vector2I.Up); _running = true;
        }
        if (_running) _runner.AdvanceFrame();
        if (!Finished) _actor.AdvanceAnimationUpdates(1);
    }
    public override bool HasActorBinding(CutsceneActorId actor) => actor.Value == "Vire";
    public override void ShowText(int textId, string message, int? position) => Context.ShowDialogue(message, position, sourceTextId: textId);
    public override void MoveActorAtSpeed(string actor, int speed, int angle) =>
        _actor.SetStatePosition(NativeObjectMovement.ApplySpeed(Context.Entities.RuntimeState, ref _precise, speed, angle));
    public override void OrRoomFlag(int flag) => Context.Rooms.SaveData.SetRoomFlag(_data.Record.Group, _data.Record.Room, checked((byte)flag));
    public override void SetMusic(int music)
    {
        if (music != 0xff) throw UnsupportedCommand($"Vire music ${music:x2}");
        Context.Sound.SetNativeActiveMusic(Context.Sound.NativeRoomMusic);
        Context.Sound.PlayNativeSound(Context.Sound.NativeRoomMusic);
    }
    public override void ScriptEnded()
    { SetDisabledObjects(0); SetNativeMenuDisabled(0); RelinquishInputControl(); Finished = true; }
}
