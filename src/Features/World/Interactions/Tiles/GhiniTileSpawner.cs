using Godot;

namespace Oracles;

// interactableTiles.s:nextToGhiniSpawner writes layout directly before checked allocation.
internal sealed class GhiniTileSpawner(RoomSession rooms,RoomEntityManager entities)
{
    internal bool UpdatePushAttempt(Vector2 position,Vector2I facing,Vector2 input)
    {
        if(entities.RuntimeState.ReadField("wIsMaplePresent")!=0)return false;
        if(!InteractableTilePushGeometry.TryGetCardinalInput(input,out _) ||
            !InteractableTilePushGeometry.IsAlignedForPush(position))
        {
            rooms.TilePushCounter=20;
            return false;
        }
        rooms.TilePushCounter=unchecked((byte)(rooms.TilePushCounter-1));
        if(rooms.TilePushCounter!=0)return false;
        var room=rooms.CurrentRoom;
        Vector2 front=position+InteractableTilePushGeometry.FrontTileOffset(facing);
        byte packed=(byte)room.GetPackedPosition(front);
        byte collision=room.GetTerrainInfo(front).Collision;
        room.SetPackedLayoutWithoutGraphics(packed,0);
        room.SetPackedTileCollision(packed,collision); // The raw write retains both CE collision and displayed grave.
        entities.TryCreateEnemy(new GhiniSpawn(new((packed&15)*16+8,(packed>>4)*16+8),1,
            "GraveGhini","code/interactableTiles.s:nextToGhiniSpawner"));
        return false;
    }
}
