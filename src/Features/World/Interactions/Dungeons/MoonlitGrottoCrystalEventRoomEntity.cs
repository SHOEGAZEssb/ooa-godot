using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_DUNGEON_EVENTS $21:$0d and the two original Moonlit Grotto
/// scripts. The handler watches the shared crystal bitfield, owns both timed
/// dialogue sequences, and persists the per-room/global completion flags.
/// </summary>
internal sealed partial class MoonlitGrottoCrystalEventRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IPlayerRestriction
{
    private readonly DungeonMechanicDatabaseRecord _record;
    private readonly DungeonMechanicDatabase _data;
    private readonly OracleSaveData? _save;
    private readonly OracleRuntimeState _runtime;
    private readonly Action<int> _playSound;
    private readonly Action<int> _beginScreenShake;
    private readonly Action<int, string, Vector2> _showText;
    private readonly Func<bool> _dialogueOpen;
    private byte _observedSwitchState;
    private int _counter;
    private Vector2 _dialoguePosition;
    private MoonlitCrystalEventPhase _phase;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool DisablesSword => RestrictsPlayer;
    public bool DisablesItems => RestrictsPlayer;
    public bool DisablesMovement => RestrictsPlayer;
    public bool DisablesMenus => RestrictsPlayer;
    public bool DisablesScreenTransitions => RestrictsPlayer;
    internal bool RestrictsPlayer => !Finished && _phase != MoonlitCrystalEventPhase.Waiting;
    internal MoonlitCrystalEventPhase Phase => _phase;
    internal int Counter => _counter;

    internal MoonlitGrottoCrystalEventRoomEntity(
        DungeonMechanicDatabaseRecord record,
        DungeonMechanicDatabase data,
        OracleSaveData? save,
        OracleRuntimeState runtime,
        Action<int> playSound,
        Action<int> beginScreenShake,
        Action<int, string, Vector2> showText,
        Func<bool> dialogueOpen)
    {
        if (record.Id != 0x21 || record.SubId != 0x0d)
            throw new ArgumentOutOfRangeException(nameof(record));
        _record = record;
        _data = data;
        _save = save;
        _runtime = runtime;
        _playSound = playSound;
        _beginScreenShake = beginScreenShake;
        _showText = showText;
        _dialogueOpen = dialogueOpen;
        _observedSwitchState = runtime.ReadWramByte(
            OracleRuntimeState.SwitchStateAddress);
        Name = $"MoonlitCrystalEvent_{record.Room:x2}";
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;

        switch (_phase)
        {
            case MoonlitCrystalEventPhase.Waiting:
            {
                byte current = _runtime.ReadWramByte(
                    OracleRuntimeState.SwitchStateAddress);
                if (current == _observedSwitchState || frame.Player.IsDying)
                    return;
                _observedSwitchState = current;
                _dialoguePosition = frame.Player.Position;
                _counter = _data.MoonlitFirstWait;
                _phase = MoonlitCrystalEventPhase.FirstWait;
                break;
            }

            case MoonlitCrystalEventPhase.FirstWait:
                if (--_counter != 0)
                    return;
                _playSound(OracleSoundEngine.SndCtrlStopSfx);
                _beginScreenShake(_data.MoonlitRumbleWait);
                _playSound(_data.MoonlitRumbleSound);
                _counter = _data.MoonlitRumbleWait;
                _phase = MoonlitCrystalEventPhase.Rumbling;
                break;

            case MoonlitCrystalEventPhase.Rumbling:
                if (--_counter != 0)
                    return;
                _showText(0x1200, _data.Text(0x1200), _dialoguePosition);
                _phase = MoonlitCrystalEventPhase.FirstDialogue;
                break;

            case MoonlitCrystalEventPhase.FirstDialogue:
                if (_dialogueOpen())
                    return;
                _save?.SetRoomFlag(
                    _record.Group,
                    _record.Room,
                    (byte)_data.MoonlitRoomFlag);
                if ((_runtime.ReadWramByte(
                        OracleRuntimeState.SwitchStateAddress) &
                     _data.MoonlitAllCrystalsMask) !=
                    _data.MoonlitAllCrystalsMask)
                {
                    Finished = true;
                    return;
                }
                _runtime.SetWramByte(OracleRuntimeState.SpinnerStateAddress, 0);
                _counter = _data.MoonlitAllWait;
                _phase = MoonlitCrystalEventPhase.AllWait;
                break;

            case MoonlitCrystalEventPhase.AllWait:
                if (--_counter != 0)
                    return;
                _beginScreenShake(100);
                _playSound(_data.MoonlitBigExplosionSound);
                _counter = _data.MoonlitExplosionWait;
                _phase = MoonlitCrystalEventPhase.ExplosionWait;
                break;

            case MoonlitCrystalEventPhase.ExplosionWait:
                if (--_counter != 0)
                    return;
                _playSound(_data.MoonlitSolveSound);
                _counter = _data.MoonlitSolveWait;
                _phase = MoonlitCrystalEventPhase.SolveWait;
                break;

            case MoonlitCrystalEventPhase.SolveWait:
                if (--_counter != 0)
                    return;
                _showText(0x1201, _data.Text(0x1201), _dialoguePosition);
                _phase = MoonlitCrystalEventPhase.AllDialogue;
                break;

            case MoonlitCrystalEventPhase.AllDialogue:
                if (_dialogueOpen())
                    return;
                _save?.SetGlobalFlag(_data.MoonlitGlobalFlag);
                Finished = true;
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported Moonlit Grotto crystal phase {_phase}.");
        }
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }
}

internal enum MoonlitCrystalEventPhase
{
    Waiting,
    FirstWait,
    Rumbling,
    FirstDialogue,
    AllWait,
    ExplosionWait,
    SolveWait,
    AllDialogue
}
