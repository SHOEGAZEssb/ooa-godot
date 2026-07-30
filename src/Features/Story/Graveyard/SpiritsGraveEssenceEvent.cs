using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Native INTERAC_ESSENCE $7f state 1-7 and
/// mainScripts.essenceScript_essenceGetCutscene for imported dungeons.
/// </summary>
internal sealed class SpiritsGraveEssenceEvent : IRoomEvent
{

    private readonly RoomEventContext _context;
    private readonly SpiritsGraveDatabase _database = new();
    private readonly WingDungeonDatabase _wingDungeon = new();
    private SpiritsGraveEssence? _essence;
    private SpiritsGraveEssenceEventPhase _phase;
    private int _counter;
    private int _fadeStep;

    internal SpiritsGraveEssenceEvent(RoomEventContext context)
    {
        _context = context;
    }

    public bool HasState => _phase != SpiritsGraveEssenceEventPhase.Inactive;
    public bool BlocksGameplay => HasState;
    internal int CurrentPhase => (int)_phase;
    internal int Counter => _counter;
    internal bool TracksEssence => _essence is not null;

    internal void Begin(SpiritsGraveEssence essence, Player player)
    {
        if (HasState || !ReferenceEquals(player, _context.Player))
            return;
        _essence = essence;
        _phase = SpiritsGraveEssenceEventPhase.AwaitingHeldPose;
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
            case SpiritsGraveEssenceEventPhase.Inactive:
                return;

            case SpiritsGraveEssenceEventPhase.AwaitingHeldPose:
                if (_essence?.ReadyForDialogue != true)
                    return;
                _context.ShowDialogue(EssenceMessage());
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
                _phase = SpiritsGraveEssenceEventPhase.Dialogue;
                return;

            case SpiritsGraveEssenceEventPhase.Dialogue:
                if (_context.DialogueOpen)
                    return;
                _context.Sound.PlaySound(OracleSoundEngine.MusEssence);
                _context.Sound.PlaySound(OracleSoundEngine.SndEnergyThing);
                _essence?.StartEnergySwirl();
                _counter = 360;
                _phase = SpiritsGraveEssenceEventPhase.Swirl;
                return;

            case SpiritsGraveEssenceEventPhase.Swirl:
                if (--_counter != 0)
                    return;
                _context.Sound.PlaySound(OracleSoundEngine.SndFadeOut);
                _counter = 20;
                _fadeStep = 0;
                _phase = SpiritsGraveEssenceEventPhase.FadeCadence;
                return;

            case SpiritsGraveEssenceEventPhase.FadeCadence:
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
                _phase = SpiritsGraveEssenceEventPhase.WarpDelay;
                return;

            case SpiritsGraveEssenceEventPhase.WarpDelay:
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
        _phase = SpiritsGraveEssenceEventPhase.Inactive;
        _counter = 0;
        _context.RoomView.ClearBackgroundFade();
    }

    private void Finish()
    {
        _context.Player.EndCutsceneControl();
        _context.Sound.PlaySound(OracleSoundEngine.SndCtrlStopMusic);
        Warp warp = EssenceWarp();
        _context.Transitions.ApplyWarpWithDelayedFadeOut(_context.Player, warp);
        // The two-hand pose survives the source-room fade and Player.WarpTo
        // clears it when the destination loads. The source interaction itself
        // is freed there, so retaining it would leave CancelAll holding a
        // disposed Godot object on the next ordinary room change.
        _essence = null;
        _phase = SpiritsGraveEssenceEventPhase.Inactive;
    }

    private string EssenceMessage() =>
        _essence?.EssenceIndex switch
        {
            0 => _database.EssenceMessage,
            1 => _wingDungeon.EssenceMessage,
            null => throw new InvalidOperationException(
                "The essence get event has no active essence."),
            int index => throw new InvalidOperationException(
                $"Essence ${index:x2} has no imported get text.")
        };

    private Warp EssenceWarp() =>
        _essence?.EssenceIndex switch
        {
            0 => new Warp(4, 0x11, -1, 0, 0, 0, 0x8d, 0x26, 0, 1),
            1 => new Warp(4, 0x38, -1, 0, 0, 1, 0x83, 0x25, 0, 1),
            null => throw new InvalidOperationException(
                "The essence get event has no active essence."),
            int index => throw new InvalidOperationException(
                $"Essence ${index:x2} has no imported exit warp.")
        };
}

internal enum SpiritsGraveEssenceEventPhase
{
    Inactive,
    AwaitingHeldPose,
    Dialogue,
    Swirl,
    FadeCadence,
    WarpDelay
}
