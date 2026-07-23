using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal readonly record struct Condition(ConditionKind Kind, int A, int B, int C);
