using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed record Definition(int InteractionId, int Sound, List<RockDebrisEffectFrameRecord> Animation);
