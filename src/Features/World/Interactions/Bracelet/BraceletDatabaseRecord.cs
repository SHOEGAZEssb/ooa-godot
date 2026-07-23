using System;

namespace oracleofages;
internal readonly record struct BraceletDatabaseRecord(int Item, int PickupSound, int ThrowSound, int Damage, int RadiusY, int RadiusX, int CollisionZRadius, int Gravity, int InitialSpeedZ, int SpeedRaw, int TossSpeedRaw, int PushSpeedRaw, int PushFrames, int PowerGlovePushSpeedRaw, int PowerGlovePushFrames, int HeavyPropertyMask, int GrabPullFrames, int LiftLowFrames, int LiftMidFrames, int LiftHighFrames, int ThrowFrames, string Source);
