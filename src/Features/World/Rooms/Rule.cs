using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal readonly record struct Rule(Condition[] Conditions, Operation[] Operations);
