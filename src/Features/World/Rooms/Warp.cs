using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
public readonly record struct Warp(int SourceGroup, int SourceRoom, int SourcePosition, int EdgeMask, int SourceTransition, int DestinationGroup, int DestinationRoom, int DestinationPosition, int DestinationParameter, int DestinationTransition);
