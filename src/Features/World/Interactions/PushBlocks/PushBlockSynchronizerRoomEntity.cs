using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class PushBlockSynchronizerRoomEntity(OracleRoomData room,
    PushBlockSynchronizerDatabase data,Func<PushBlockController> reservedPush,
    Func<byte,int,bool> tryCreateBlock) : Node2D, IRoomEntity, IFixedRoomEntity,
        IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    private int _state;
    public Node2D Node => this;
    public bool UpdatesDuringDialogue => _state == 0;
    public bool UpdatesDuringRoomEntityFreeze => _state == 0;
    public void SetTransitionDrawOffset(Vector2 offset) { }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        _state = 1;
        Visible = false;
        return ScreenTransitionPresentation.Hidden;
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        Visible = false;
        if (_state != 1) { _state = 1; return; }
        PushBlockController primary = reservedPush();
        if (!primary.Active) return;
        byte tile = primary.ActiveTile;
        int angle = primary.PushAngle;
        _state = 2;
        if (tile == data.ExcludedTile) return;
        for (int position = data.ScanStart; position >= 0; position--)
        {
            Vector2 center = new((position & 15) * 16 + 8,(position >> 4) * 16 + 8);
            if (room.GetMetatile(center) != tile) continue;
            Position = center;
            byte target = (byte)(position + data.DestinationOffsets[angle >> 3]);
            Vector2 targetCenter = new((target & 15) * 16 + 8,(target >> 4) * 16 + 8);
            // interactionCheckAdjacentTileIsSolid tests the ENTIRE byte.
            if (room.GetTerrainInfo(targetCenter).Collision != 0) continue;
            _ = tryCreateBlock((byte)position,angle);
        }
    }
}
