using Godot;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Native $11:$x1 orbiters and the parent-following $84:$0c chest glow.</summary>
internal sealed class FaroreSparkleRoomEntity : IRoomEntity, IFixedRoomEntity,
    IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly FaroreSparkleSpawn _spawn;
    private readonly RoomEntityManager _entities;
    private readonly NpcCharacter _actor;
    private OracleObjectPosition _position;
    private int _counter;
    internal int State { get; private set; }
    internal int Angle { get; private set; }
    internal int SubId => _spawn.SubId;
    public Node2D Node => _actor;
    public bool Finished { get; private set; }
    // $11 does not set the always-update bit; state zero alone bypasses text.
    public bool UpdatesDuringDialogue => State == 0 || _spawn.Id == InteractionId.Sparkle;
    public bool UpdatesDuringRoomEntityFreeze => State == 0 || _spawn.Id == InteractionId.Sparkle;

    internal FaroreSparkleRoomEntity(FaroreSparkleSpawn spawn, RoomEntityManager entities)
    {
        if (spawn.Id != InteractionId.FaroreMakeChest &&
            (spawn.Id != InteractionId.Sparkle || spawn.SubId != 0x0c))
            throw new System.InvalidOperationException($"faroreMakeChest.s: unsupported sparkle ${spawn.Id:x2}:${spawn.SubId:x2}.");
        _spawn = spawn; _entities = entities;
        _actor = spawn.Data.Actor(spawn.Id, spawn.SubId, spawn.Position);
        _position = OracleObjectPosition.FromPixels(spawn.Position);
        Angle = spawn.Angle;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        if (State == 0)
        {
            State = 1; _counter = 0x30;
            _actor.SetFixedDrawPriority(_spawn.Id == InteractionId.FaroreMakeChest
                ? ObjectDrawPriority.FixedHighPriorityZIndex : ObjectDrawPriority.BehindLinkZIndex);
            _actor.SetScriptVisible(true);
            return;
        }
        if (_spawn.Id == InteractionId.Sparkle)
        {
            // $84:$0c compares the related slot's ID before copying it.
            if (_spawn.Parent is not { Finished: false }) { Delete(); return; }
            _actor.SetStatePosition(_spawn.Parent.Position);
            if ((_entities.RuntimeState.ReadWramByte(WramAddress.wTmpcfc0) & 1) != 0) { Delete(); return; }
            _actor.SetScriptVisible((frame.Counter & 1) == 0);
            _actor.AdvanceAnimationUpdates(1);
            return;
        }
        if (State is 1 or 3)
        {
            _position = NativeObjectMovement.ApplySpeed(_entities.RuntimeState, _position, State == 1 ? 0x28 : 0x50, Angle);
            _actor.SetStatePosition(_position.PixelPosition);
            _actor.AdvanceAnimationUpdates(1);
            if (State == 1)
            {
                if (--_counter == 0) State = 2;
            }
            else
            {
                Vector2 screen = _entities.WorldToScreen(_position.PixelPosition);
                // objectCheckWithinScreenBoundary uses wrapping bytes and +$07.
                if (((int)screen.Y + 7 & 255) >= 0x8f || ((int)screen.X + 7 & 255) >= 0xaf) Delete();
            }
            return;
        }
        if ((frame.Counter & 1) == 0)
        {
            Angle = (Angle + 1) & 0x1f;
            if (Angle == 0) _entities.RequestSound(SoundId.SndCircling);
        }
        Vector2I offset = NativeObjectMovement.CircleArcOffset(_entities.RuntimeState,
            _entities.RuntimeState.ReadWramByte(FaroreScriptHost.SparkleRadiusAddress), Angle);
        // Native writes retain the low bytes accumulated during the outward flight.
        _position = new((ushort)(((0x78 + offset.Y & 255) << 8) | (_position.YFixed & 255)),
            (ushort)(((0x58 + offset.X & 255) << 8) | (_position.XFixed & 255)));
        _actor.SetStatePosition(_position.PixelPosition);
        _actor.AdvanceAnimationUpdates(1);
        if (_entities.RuntimeState.ReadWramByte(FaroreScriptHost.SparkleFlyawayAddress) != 0) State = 3;
    }

    private void Delete() { Finished = true; _actor.SetActive(false); }
    public void SetTransitionDrawOffset(Vector2 offset) => _actor.SetTransitionDrawOffset(offset);
}

internal sealed record FaroreSparkleSpawn(FaroreVisualDatabase Data, int Id, int SubId,
    Vector2 Position, int Angle = 0, FaroreMakeChestRoomEntity? Parent = null) : RoomEntitySpawn;
