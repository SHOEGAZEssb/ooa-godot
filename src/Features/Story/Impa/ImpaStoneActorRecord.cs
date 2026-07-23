using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
public readonly record struct ImpaStoneActorRecord(int Group, int Room, int InteractionId, int SubId, int InitialY, int InitialX, int MovedY, int LeftX, int RightX, int CollisionRadiusY, int CollisionRadiusX, int LeftRoomFlag, int RightRoomFlag, int ApproachY, int ApproachX, int TargetY, int TargetX, int CloseRadius, int LeaveY, int LeaveX, string SpriteName, int TileBase, int Palette, string Animation, int FinalLayoutTile, int FinalCollision, int LinkTargetY, int LinkTargetX, bool SourceGrayscaleInverted)
{
    public NpcRecord ToNpcRecord(int y, int x) => new(Group, Room, InteractionId, SubId, y, x, 0, 0, SpriteName, TileBase, Palette, 0, false, Animation, Animation, Animation, Animation, string.Empty);
}
