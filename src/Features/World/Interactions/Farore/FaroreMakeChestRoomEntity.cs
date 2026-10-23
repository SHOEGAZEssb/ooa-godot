using Godot;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_FARORE_MAKECHEST $11:$00's ordered native counter lane.</summary>
internal sealed partial class FaroreMakeChestRoomEntity(
    FaroreScriptHost parent, RoomEntityManager entities) : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private int _state;
    private int _counter;
    private int _sparkleRadius = 0x30;
    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => true;
    public bool UpdatesDuringRoomEntityFreeze => true;
    internal int State => _state;
    internal int Counter => _counter;
    internal int SparkleRadius => _sparkleRadius;

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        if (_state == 0)
        {
            _counter = 0x5a;
            // faroreMakeChest.s:darkenRoomLightly and camera focus begin here.
            _state = 1;
            return;
        }
        if (_state == 4 && (frame.Counter & 1) == 0)
            _sparkleRadius = (_sparkleRadius - 1) & 0xff;
        if (--_counter != 0) return;
        switch (_state++)
        {
            case 1: _counter = 0x30; break;
            case 2: _counter = 0x1e; break;
            case 3: _counter = 0x50; break;
            case 4: _counter = 0x28; break;
            case 5:
                entities.TryCreatePuzzlePuff(new Vector2(0x58, 0x78));
                parent.InstallChestTile();
                _counter = 0x08;
                break;
            case 6 or 7 or 8:
                entities.RequestSound(SoundId.SndFadeOut);
                _counter = 0x10;
                break;
            case 9:
                entities.RequestSound(SoundId.SndFadeOut);
                parent.MakeChestFinished();
                Finished = true;
                break;
        }
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }
}

internal sealed record FaroreMakeChestSpawn(FaroreScriptHost Parent) : RoomEntitySpawn;
