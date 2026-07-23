using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal readonly record struct Operation(OperationKind Kind, int[] Values);
