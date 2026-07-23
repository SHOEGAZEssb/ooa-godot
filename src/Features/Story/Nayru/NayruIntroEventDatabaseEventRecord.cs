using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
public readonly record struct NayruIntroEventDatabaseEventRecord(int Group, int Room, int IntroFlag, int CompletionRoomFlag, int BearRoomFlag, int TriggerX, int TriggerY, int BearDelayFrames, int PostBearTextFrames, int SingingFrames, int SingingSkipWindow, int SingingScrollPeriod, int SingingScrollSteps, int PossessionFadeHoldFrames, int PortalPosition, int PortalTile, int VignetteCount, int NpcJumpSpeedZ, int NpcJumpGravity, int DarkFadeFrames, int WhiteFadeOutFrames, int WhiteFadeInFrames, int NayruAscentSpeedZ, int NayruTransferZ, int NayruLandingDelay, int NayruFallSpeedZ, int NayruFallGravity);
