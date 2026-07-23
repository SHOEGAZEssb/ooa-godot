using Godot;
using System;
using static oracleofages.OracleGraphicsData;
using static oracleofages.OracleTileRenderer;

namespace oracleofages;
internal enum InventoryTextPhase
{
    Hidden,
    NamePause,
    Description,
    TrailingSpaces,
    NameReplay,
    NamePadding,
    FullNameLeadWait,
    FullNamePause
}
