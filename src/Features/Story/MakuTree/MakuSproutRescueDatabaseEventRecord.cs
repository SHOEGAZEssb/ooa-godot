using System;
using System.Collections.Generic;

namespace oracleofages;
internal readonly record struct MakuSproutRescueDatabaseEventRecord(int Group, int Room, int SproutId, int SproutSubId, int ControllerY, int ControllerX, int MoblinId, int MoblinY, int LeftX, int RightX, int InitialGatePosition, int ClearTile, int GateLeft, int GateInnerLeft, int GateInnerRight, int GateRight, int RoomFlag, int AdviceFlag, int SavedFlag, int StateMin, int StateMax, int MapTextLow, int TriggerRadiusY, int TriggerRadiusX, int JumpSpeedZ, int JumpGravity, int JumpSound, int GateCounter, int ShakeCounter, int FinalTextPosition, int PostTextId, string PostText);
