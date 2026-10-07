using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class SpiritsGraveMovingPlatformSpawner : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity, IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly Func<int, bool> _triggerActive;
    private readonly Action<int> _playSound;
    private readonly int _spawnWait;
    private readonly OracleRuntimeState _runtime;
    private readonly Func<bool> _scriptSuspended;
    private readonly Func<Vector2, bool> _createPuff;
    private readonly Func<Vector2, int, bool> _createPlatform;
    private bool _initialized;
    private int _state;
    private int _counter;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;

    internal SpiritsGraveMovingPlatformSpawner(
        Func<int, bool> triggerActive,
        Action<int> playSound,
        int spawnWait,
        OracleRuntimeState runtime,
        Func<bool> scriptSuspended,
        Func<Vector2, bool> createPuff,
        Func<Vector2, int, bool> createPlatform)
    {
        _triggerActive = triggerActive;
        _playSound = playSound;
        _spawnWait = spawnWait;
        _runtime = runtime;
        _scriptSuspended = scriptSuspended;
        _createPuff = createPuff;
        _createPlatform = createPlatform;
        Name = "SpiritsGraveMovingPlatformSpawner";
        Visible = false;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
        => Advance(frame.Player.IsDying);

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
        => throw new InvalidOperationException("INTERAC$20:$05 preload requires Link's live death-trigger state.");

    public ScreenTransitionPresentation PrepareForScreenTransition(Player? player, ICollection<RoomEntitySpawn> spawns)
    {
        if (player is null) throw new InvalidOperationException("INTERAC$20:$05 preload requires Link's live death-trigger state.");
        if (!_initialized) Advance(player.IsDying);
        return ScreenTransitionPresentation.Hidden;
    }

    public void UpdateDuringScreenTransition(RoomEntityFrame frame)
    {
        if (!_initialized) Finished = true; // interactionDeleteAndRetIfEnabled02.
    }

    private void Advance(bool deathTriggered)
    {
        if (Finished) return;
        if (!_initialized)
        {
            _initialized = true;
            _runtime.SetWramByte(0xcfc1, 0);
            _runtime.SetWramByte(0xcfc2, 0);
        }
        if (deathTriggered || _scriptSuspended()) return;
        // dungeonScripts.s: spiritsGraveScript_spawnMovingPlatform. Each
        // setcoords yields; asm15 continues to the next command. Failed
        // checked allocations advance the script without retrying.
        switch (_state)
        {
            case 0:
                if (!_triggerActive(1)) return;
                Position = new(0x78, 0x48);
                _state = 1;
                break;
            case 1:
                _createPuff(Position);
                Position = new(0x78, 0x58);
                _state = 2;
                break;
            case 2:
                _createPuff(Position);
                _counter = _spawnWait;
                _state = 3;
                break;
            case 3:
                if (--_counter != 0) return;
                _createPlatform(new(0x78, 0x50), 0x09);
                _state = 4;
                break;
            case 4:
                _playSound(SoundId.SndSolvePuzzle);
                _state = 5;
                break;
            case 5:
                Finished = true;
                break;
        }
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }
}
