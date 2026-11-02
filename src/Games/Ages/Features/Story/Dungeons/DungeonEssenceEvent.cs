
namespace Oracles;

/// <summary>
/// Native INTERAC_ESSENCE $7f state 1-7 and
/// mainScripts.essenceScript_essenceGetCutscene for imported dungeons.
/// </summary>
internal sealed class DungeonEssenceEvent : IRoomEvent
{

    private readonly RoomEventContext _context;
    private readonly NativeScriptInputControl _input;
    private DungeonEssence? _essence;
    private DungeonEssenceEventPhase _phase;
    private int _counter;
    private int _fadeStep;

    internal DungeonEssenceEvent(RoomEventContext context)
    {
        _context = context;
        _input=new(context.Rooms.RuntimeState);
    }

    public bool HasState => _phase != DungeonEssenceEventPhase.Inactive;
    public bool BlocksGameplay => HasState && _input.LinkDisabled;
    internal int Counter => _counter;
    internal bool TracksEssence => _essence is not null;

    internal void Begin(DungeonEssence essence, Player player)
    {
        if (HasState || !ReferenceEquals(player, _context.Player))
            return;
        _essence = essence;
        _phase = DungeonEssenceEventPhase.AwaitingHeldPose;
        // essence.s state1 keeps ordinary Link behind the shared masks.
        player.ClearNativeItemParents();
        _input.SetDisabledObjects(0x81);_input.SetLinkCollisionsAndMenuDisabled(0x81);
        _context.Player.Face(Vector2I.Up);
        _context.RoomView.SetBackgroundFade(Colors.Black, 0.35f);
        _context.Sound.PlaySound(SoundId.SndDropEssence);
        _context.Sound.PlaySound(SoundId.SndCtrlSlowFadeOut);
    }

    public void UpdateFrame()
    {
        switch (_phase)
        {
            case DungeonEssenceEventPhase.Inactive:
                return;

            case DungeonEssenceEventPhase.AwaitingHeldPose:
                if (_essence?.ReadyForDialogue != true)
                    return;
                // essence.s keeps disabledObjects=$81 through the exit warp;
                // its script never clears that mask. WarpTo resets state04.
                _context.Player.RequestGetItemState(0x01);
                _context.ShowDialogue(
                    (_essence ?? throw new InvalidOperationException(
                        "The essence get event has no active essence.")).Message);
                _context.Rooms.SaveData.SetRoomFlag(
                    _essence.Group,
                    _essence.Room,
                    OracleSaveData.RoomFlagItem);
                _context.Inventory.GiveTreasure(
                    TreasureId.Essence,
                    _essence.EssenceIndex);
                // TREASURE_ESSENCE's collection row requests
                // MUS_GET_ESSENCE while TX_000e is open. The later native
                // script deliberately replaces it with MUS_ESSENCE.
                _context.Sound.PlaySound(SoundId.MusGetEssence);
                _phase = DungeonEssenceEventPhase.Dialogue;
                return;

            case DungeonEssenceEventPhase.Dialogue:
                if (_context.DialogueOpen)
                    return;
                // Native state5 only installs the script and clears its
                // counters. State6 starts executing it on the next update.
                _counter = 0;
                _phase = DungeonEssenceEventPhase.StartingScript;
                return;

            case DungeonEssenceEventPhase.StartingScript:
                _context.Sound.PlaySound(SoundId.MusEssence);
                // scriptCmd_playSound returns playSound's cleared carry.
                // The following asm/wait starts on the next script dispatch.
                _phase = DungeonEssenceEventPhase.StartingSwirl;
                return;

            case DungeonEssenceEventPhase.StartingSwirl:
                _essence?.StartEnergySwirl();
                _counter = 360;
                _phase = DungeonEssenceEventPhase.Swirl;
                return;

            case DungeonEssenceEventPhase.Swirl:
                if (--_counter != 0)
                    return;
                _context.Sound.PlaySound(SoundId.SndFadeOut);
                _counter = 0;
                _fadeStep = 0;
                _phase = DungeonEssenceEventPhase.StartingFadeWait;
                return;

            case DungeonEssenceEventPhase.StartingFadeWait:
                _counter = _fadeStep == 2 ? 40 : 20;
                _phase = DungeonEssenceEventPhase.FadeCadence;
                return;

            case DungeonEssenceEventPhase.FadeCadence:
                if (--_counter != 0)
                    return;
                _fadeStep++;
                _context.Sound.PlaySound(SoundId.SndFadeOut);
                if (_fadeStep < 3)
                {
                    _phase = DungeonEssenceEventPhase.StartingFadeWait;
                    return;
                }
                _phase = DungeonEssenceEventPhase.StoppingSwirl;
                return;

            case DungeonEssenceEventPhase.StoppingSwirl:
                _essence?.StopEnergySwirl();
                // state6 falls through state7 after scriptend:30 ->29 now.
                _counter = 29;
                _phase = DungeonEssenceEventPhase.WarpDelay;
                return;

            case DungeonEssenceEventPhase.WarpDelay:
                if (--_counter != 0)
                    return;
                Finish();
                return;
        }
    }

    public void Cancel()
    {
        _input.Cancel();
        if (_essence is not null &&
            GodotObject.IsInstanceValid(_essence))
        {
            _essence.StopEnergySwirl();
            _context.Player.CancelGetItemState();
            _essence.ReleasePlayerPose();
        }
        _essence = null;
        _phase = DungeonEssenceEventPhase.Inactive;
        _counter = 0;
        _context.RoomView.ClearBackgroundFade();
    }

    private void Finish()
    {
        _input.Relinquish(); // The source keeps both masks through the exit warp.
        _context.Sound.ClearActiveMusic();
        _context.Transitions.ApplyWarpWithDelayedFadeOut(
            _context.Player,
            _essence?.ExitWarp ?? throw new InvalidOperationException(
                "The essence get event has no active essence."),
            // essence.s state 7 clears wActiveMusic; checkPlayRoomMusic
            // returns while it is zero until the destination script resumes music.
            suppressDestinationMusic: true);
        StaticObjectState.Clear(_context.Rooms.RuntimeState); // essence.s state7, after warp/music publication.
        // The two-hand pose survives the source-room fade and Player.WarpTo
        // clears it when the destination loads. The source interaction itself
        // is freed there, so retaining it would leave CancelAll holding a
        // disposed Godot object on the next ordinary room change.
        _essence = null;
        _phase = DungeonEssenceEventPhase.Inactive;
    }

}

internal enum DungeonEssenceEventPhase
{
    Inactive,
    AwaitingHeldPose,
    Dialogue,
    StartingScript,
    StartingSwirl,
    Swirl,
    StartingFadeWait,
    FadeCadence,
    StoppingSwirl,
    WarpDelay
}
