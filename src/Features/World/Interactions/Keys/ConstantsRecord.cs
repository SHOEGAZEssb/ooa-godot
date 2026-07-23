using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal readonly record struct ConstantsRecord(byte RoomFlag, byte InformativeMask, int PushCounter, int OpenSound, int NoKeyTextId, string NoKeyMessage, int InteractionId, int FirstKey, int InitialSpeedZ, int Gravity, int HoldFrames, string Source);
