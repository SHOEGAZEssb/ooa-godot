using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
public readonly record struct FleeRecord(string Actor, int Delay, int Angle, float Speed, int WaitJumpSpeedZ, int WaitGravity, bool RepeatWaitJump, int EscapeJumpSpeedZ, int EscapeGravity, bool RepeatEscapeJump, bool WaitForLanding, int WaitAnimation, int EscapeAnimation);
