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
    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => _state == 0;
    public bool UpdatesDuringRoomEntityFreeze => _state == 0;
    internal int State => _state;
    internal int Counter => _counter;
    internal int SparkleRadius => entities.RuntimeState.ReadWramByte(FaroreScriptHost.SparkleRadiusAddress);

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        if (_state == 0)
        {
            Position = new(0x58, 0x78);
            entities.RuntimeState.SetWramByte(FaroreScriptHost.SparkleRadiusAddress, 0x30);
            entities.RuntimeState.SetWramByte(FaroreScriptHost.SparkleFlyawayAddress, 0);
            _counter = 0x5a;
            parent.BeginChestPresentation(Position);
            _state = 1;
            return;
        }
        if (_state == 4 && (frame.Counter & 1) == 0)
            entities.RuntimeState.SetWramByte(FaroreScriptHost.SparkleRadiusAddress, unchecked((byte)(SparkleRadius - 1)));
        if (_state == 10)
        {
            if (parent.PaletteBusy) return;
            parent.MakeChestFinished();
            Finished = true;
            return;
        }
        if (--_counter != 0) return;
        switch (_state++)
        {
            case 1:
                _counter = 0x30;
                foreach (var sparkle in parent.Visuals.Sparkles)
                    if (entities.TryCreateInteraction(new FaroreSparkleSpawn(parent.Visuals,
                        InteractionId.FaroreMakeChest, sparkle.SubId, sparkle.Position, sparkle.Angle)) is null) break;
                break;
            case 2: _counter = 0x1e; break;
            case 3: _counter = 0x50; break;
            case 4: _counter = 0x28; break;
            case 5:
                entities.RuntimeState.SetWramByte(FaroreScriptHost.SparkleFlyawayAddress, 1);
                if (entities.TryCreateInteraction(new FaroreSparkleSpawn(parent.Visuals, InteractionId.Sparkle, 0x0c, Position, Parent: this)) is null)
                {
                    // The unchecked relatedObj1 write retains HL=$e040 from
                    // getFreeInteractionSlot. $e056/$e057 alias sound WRAM.
                    entities.WriteSoundAllocationAlias(0xc056, 0x40);
                    entities.WriteSoundAllocationAlias(0xc057, (byte)(0xd0 + entities.NativeInteractionSlot(this)));
                }
                entities.TryCreatePuzzlePuff(new Vector2(0x58, 0x78));
                parent.InstallChestTile();
                _counter = 0x08;
                break;
            case 6 or 7 or 8:
                parent.BeginWhiteFlash();
                entities.RequestSound(SoundId.SndFadeOut);
                _counter = 0x10;
                break;
            case 9:
                parent.BeginWhiteFlash(4);
                entities.RequestSound(SoundId.SndFadeOut);
                break;
        }
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }
}

internal sealed record FaroreMakeChestSpawn(FaroreScriptHost Parent) : RoomEntitySpawn;
