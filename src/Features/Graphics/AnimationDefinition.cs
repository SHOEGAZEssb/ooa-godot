using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed record AnimationDefinition(AnimationFrameDefinition[] Frames, int LoopStart);
