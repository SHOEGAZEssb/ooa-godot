using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed record NpcCharacterAnimationFrame(Texture2D Texture, int Duration, int Parameter, Vector2 Offset);
