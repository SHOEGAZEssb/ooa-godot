using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
public readonly record struct ImpaHelpEventRecord(int Group, int Room, int InteractionId, int SubId, int RoomFlag, int EdgeY, int PostTextFrames, int InputUpFrames, int TextId, int TextboxPosition, string Text);
