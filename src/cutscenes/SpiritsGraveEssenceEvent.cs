using Godot;

namespace oracleofages;

/// <summary>
/// Native INTERAC_ESSENCE $7f state 1-7 and
/// mainScripts.essenceScript_essenceGetCutscene for dungeon 1.
/// </summary>
internal sealed class SpiritsGraveEssenceEvent : IRoomEvent
{
    private enum Phase
    {
        Inactive,
        AwaitingHeldPose,
        Dialogue,
        Swirl,
        FadeCadence,
        WarpDelay
    }

    private readonly RoomEventContext _context;
    private readonly SpiritsGraveDatabase _database = new();
    private SpiritsGraveEssence? _essence;
    private Phase _phase;
    private int _counter;
    private int _fadeStep;

    internal SpiritsGraveEssenceEvent(RoomEventContext context)
    {
        _context = context;
    }

    public bool HasState => _phase != Phase.Inactive;
    public bool BlocksGameplay => HasState;
    internal int CurrentPhase => (int)_phase;
    internal int Counter => _counter;

    internal void Begin(SpiritsGraveEssence essence, Player player)
    {
        if (HasState || !ReferenceEquals(player, _context.Player))
            return;
        _essence = essence;
        _phase = Phase.AwaitingHeldPose;
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
            case Phase.Inactive:
                return;

            case Phase.AwaitingHeldPose:
                if (_essence?.ReadyForDialogue != true)
                    return;
                _context.ShowDialogue(_database.EssenceMessage);
                _context.Rooms.SaveData.SetRoomFlag(
                    4, 0x11, OracleSaveData.RoomFlagItem);
                _context.Inventory.GiveTreasure(TreasureDatabase.TreasureEssence, 0);
                _phase = Phase.Dialogue;
                return;

            case Phase.Dialogue:
                if (_context.DialogueOpen)
                    return;
                _context.Sound.PlaySound(OracleSoundEngine.MusEssence);
                _context.Sound.PlaySound(OracleSoundEngine.SndEnergyThing);
                _essence?.StartEnergySwirl();
                _counter = 360;
                _phase = Phase.Swirl;
                return;

            case Phase.Swirl:
                if (--_counter != 0)
                    return;
                _context.Sound.PlaySound(OracleSoundEngine.SndFadeOut);
                _counter = 20;
                _fadeStep = 0;
                _phase = Phase.FadeCadence;
                return;

            case Phase.FadeCadence:
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
                _phase = Phase.WarpDelay;
                return;

            case Phase.WarpDelay:
                if (--_counter != 0)
                    return;
                Finish();
                return;
        }
    }

    public void Cancel()
    {
        _essence?.StopEnergySwirl();
        _essence?.ReleasePlayerPose();
        _essence = null;
        _phase = Phase.Inactive;
        _counter = 0;
        _context.RoomView.ClearBackgroundFade();
    }

    private void Finish()
    {
        _essence?.ReleasePlayerPose();
        _context.Player.EndCutsceneControl();
        _context.Sound.PlaySound(OracleSoundEngine.SndCtrlStopMusic);
        var warp = new WarpDatabase.Warp(
            4, 0x11, -1, 0, 0,
            0, 0x8d, 0x26, 0, 1);
        _context.Transitions.ApplyWarpWithDelayedFadeOut(_context.Player, warp);
        _phase = Phase.Inactive;
    }
}
