using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_DUNGEON_EVENTS $21:$0f, verifyTiles -> wActiveTriggers.</summary>
internal sealed partial class DungeonPatternTriggerRoomEntity : Node2D, IRoomEntity, IFixedRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly OracleRoomData _room;
    private readonly int _firstTile;
    private readonly IReadOnlyList<byte>[] _patterns;
    private readonly Action<int, bool> _setTrigger;

    public Node2D Node => this;
    // $21:$0f never increments Interaction.state. The state-zero dispatcher
    // therefore continues to execute it under text and DISABLE_INTERACTIONS.

    internal DungeonPatternTriggerRoomEntity(OracleRoomData room, int firstTile,
        IReadOnlyList<byte>[] patterns, Action<int, bool> setTrigger)
    {
        _room = room;
        _firstTile = firstTile;
        _patterns = patterns;
        _setTrigger = setTrigger;
        Name = "DungeonPatternTrigger";
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        bool matches = DungeonTilePattern.Matches(_room, _firstTile, _patterns);
        // The native event replaces the entire trigger byte, including bits
        // published by earlier objects, on every dispatch.
        for (int bit = 0; bit < 8; bit++)
            _setTrigger(bit, bit == 0 && matches);
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }
}
