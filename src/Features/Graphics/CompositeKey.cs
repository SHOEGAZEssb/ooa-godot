using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal readonly record struct CompositeKey(ulong BaseImageId, ulong ExtraImageId, int ExtraOffset);
