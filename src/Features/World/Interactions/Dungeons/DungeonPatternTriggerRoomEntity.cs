using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_DUNGEON_EVENTS $21:$0f, verifyTiles -> wActiveTriggers.</summary>
internal sealed partial class DungeonPatternTriggerRoomEntity : Node2D, IRoomEntity, IFixedRoomEntity,
    IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity, IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly OracleRoomData _room;
    private readonly int _firstTile;
    private readonly IReadOnlyList<byte>[] _patterns;
    private readonly Action<int, bool> _setTrigger;
    private readonly Func<IRoomEntity, bool> _isOutgoing;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    // $21:$0f never increments Interaction.state. The state-zero dispatcher
    // therefore continues to execute it under text and DISABLE_INTERACTIONS.

    internal DungeonPatternTriggerRoomEntity(OracleRoomData room, int firstTile,
        IReadOnlyList<byte>[] patterns, Action<int, bool> setTrigger, Func<IRoomEntity, bool> isOutgoing)
    {
        _room = room;
        _firstTile = firstTile;
        _patterns = patterns;
        _setTrigger = setTrigger;
        _isOutgoing = isOutgoing;
        Name = "DungeonPatternTrigger";
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
        => Advance();

    public void UpdateDuringScreenTransition(RoomEntityFrame frame) => Advance();

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Advance();
        return ScreenTransitionPresentation.Visible;
    }

    private void Advance()
    {
        // interactionDeleteAndRetIfEnabled02 precedes verifyTiles. An outgoing
        // state-$00 controller deletes without replacing the incoming trigger.
        if (_isOutgoing(this)) Finished = true;
        if (Finished) return;
        bool matches = DungeonTilePattern.Matches(_room, _firstTile, _patterns);
        // The native event replaces the entire trigger byte, including bits
        // published by earlier objects, on every dispatch.
        for (int bit = 0; bit < 8; bit++)
            _setTrigger(bit, bit == 0 && matches);
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }
}
