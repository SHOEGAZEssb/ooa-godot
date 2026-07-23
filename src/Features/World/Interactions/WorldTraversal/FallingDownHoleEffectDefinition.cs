using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed record FallingDownHoleEffectDefinition(FallingDownHoleEffectFrameRecord[] Frames, float Speed);
