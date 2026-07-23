using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed record EnemyAnimationPlayerAnimationFrame(Texture2D Texture, Texture2D? DamageTexture, int Duration, int Parameter);
