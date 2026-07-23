using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed record EnemyDeathPuffEffectDefinition(int[] Palettes, List<EnemyDeathPuffEffectFrameRecord> NormalAnimation, List<EnemyDeathPuffEffectFrameRecord> HighKnockbackAnimation);
