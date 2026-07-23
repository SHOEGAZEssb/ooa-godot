using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal readonly record struct BlackTowerEntranceEventDatabaseEventRecord(int Group, int Room, int GuardId, int GuardSubId, int EssenceMask, int ItemFlag, int AftermathFlag, int CompleteFlag, int InitialY, int InitialX, int CompletedY, int CompletedX, int MoveSpeed, int MoveCounter, int ScreenOffsetY, int IntroWait, int PostWait, int SourceTransition, int DestinationTransition, int ExplanationTextId, string ExplanationText);
