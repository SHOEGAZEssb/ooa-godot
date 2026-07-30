using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Native INTERAC_ESSENCE $7f state 1-7 and
/// mainScripts.essenceScript_essenceGetCutscene for imported dungeons.
/// </summary>
internal sealed class DungeonEssenceEvent : IRoomEvent
{

    private readonly RoomEventContext _context;
    private DungeonEssence? _essence;
    private DungeonEssenceEventPhase _phase;
    private int _counter;
    private int _fadeStep;

    internal DungeonEssenceEvent(RoomEventContext context)
    {
        _context = context;
    }

    public bool HasState => _phase != DungeonEssenceEventPhase.Inactive;
    public bool BlocksGameplay => HasState;
    internal int CurrentPhase => (int)_phase;
    internal int Counter => _counter;
    internal bool TracksEssence => _essence is not null;

    internal void Begin(DungeonEssence essence, Player player)
    {
        if (HasState || !ReferenceEquals(player, _context.Player))
            return;
        _essence = essence;
        _phase = DungeonEssenceEventPhase.AwaitingHeldPose;
        _context.Player.BeginCutsceneControl();
        _context.Player.Face(Vector2I.Up);
        _context.RoomView.SetBackgroundFade(Colors.Black, 0.35f);
        _context.Sound.PlaySound(OracleSoundEngine.SndDropEssence);
        _context.Sound.PlaySound(OracleSoundEngine.SndCtrlSlowFadeOut);
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
                _context.ShowDialogue(
                    (_essence ?? throw new InvalidOperationException(
                        "The essence get event has no active essence.")).Message);
                _context.Rooms.SaveData.SetRoomFlag(
                    _essence.Group,
                    _essence.Room,
                    OracleSaveData.RoomFlagItem);
                _context.Inventory.GiveTreasure(
                    TreasureDatabase.TreasureEssence,
                    _essence.EssenceIndex);
                // TREASURE_ESSENCE's collection row requests
                // MUS_GET_ESSENCE while TX_000e is open. The later native
                // script deliberately replaces it with MUS_ESSENCE.
                _context.Sound.PlaySound(OracleSoundEngine.MusGetEssence);
                _phase = DungeonEssenceEventPhase.Dialogue;
                return;

            case DungeonEssenceEventPhase.Dialogue:
                if (_context.DialogueOpen)
                    return;
                _context.Sound.PlaySound(OracleSoundEngine.MusEssence);
                _context.Sound.PlaySound(OracleSoundEngine.SndEnergyThing);
                _essence?.StartEnergySwirl();
                _counter = 360;
                _phase = DungeonEssenceEventPhase.Swirl;
                return;

            case DungeonEssenceEventPhase.Swirl:
                if (--_counter != 0)
                    return;
                _context.Sound.PlaySound(OracleSoundEngine.SndFadeOut);
                _counter = 20;
                _fadeStep = 0;
                _phase = DungeonEssenceEventPhase.FadeCadence;
                return;

            case DungeonEssenceEventPhase.FadeCadence:
                if (--_counter != 0)
                    return;
                _fadeStep++;
                _context.Sound.PlaySound(OracleSoundEngine.SndFadeOut);
                if (_fadeStep < 3)
                {
                    _counter = _fadeStep == 2 ? 40 : 20;
                    return;
                }
                _essence?.StopEnergySwirl();
                _counter = 30;
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
        if (_essence is not null &&
            GodotObject.IsInstanceValid(_essence))
        {
            _essence.StopEnergySwirl();
            _essence.ReleasePlayerPose();
        }
        _essence = null;
        _phase = DungeonEssenceEventPhase.Inactive;
        _counter = 0;
        _context.RoomView.ClearBackgroundFade();
    }

    private void Finish()
    {
        _context.Player.EndCutsceneControl();
        _context.Sound.PlaySound(OracleSoundEngine.SndCtrlStopMusic);
        _context.Transitions.ApplyWarpWithDelayedFadeOut(
            _context.Player,
            _essence?.ExitWarp ?? throw new InvalidOperationException(
                "The essence get event has no active essence."));
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
    Swirl,
    FadeCadence,
    WarpDelay
}
