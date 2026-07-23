using System;

namespace oracleofages;
internal readonly record struct BlackTowerDoorwayEventDatabaseRecord(int Group, int Room, int InteractionId, int SubId, int Y, int X, int ClearPositionA, int ClearPositionB, int ObjectRadiusY, int ObjectRadiusX, int LinkRadiusY, int LinkRadiusX, int RoomFlagMask, int ClearDestinationGroup, int ClearDestinationRoom, int SetDestinationGroup, int SetDestinationRoom, int WarpTransition, int DestinationPosition, int WarpTransition2, int Sound, string Source);
