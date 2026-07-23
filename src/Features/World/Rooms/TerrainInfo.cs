using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
public readonly record struct TerrainInfo(byte Tile, byte Collision, TerrainType Type, HazardType Hazard);
