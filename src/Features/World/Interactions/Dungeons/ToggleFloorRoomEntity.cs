using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_TOGGLE_FLOOR $15:$00, the placed child allocator.</summary>
internal sealed partial class ToggleFloorRoomEntity : Node2D, IRoomEntity, IFixedRoomEntity,
    IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity, IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly OracleRoomData _room;
    private readonly int _firstTile;
    private readonly Func<byte> _activeTile;
    private readonly Func<byte, byte, bool> _createChild;
    private readonly Func<int> _pendingCount;
    private readonly Func<IRoomEntity, bool> _isOutgoing;
    private int _lastTilePosition;
    private bool _initialized;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;
    internal int PendingCount => _pendingCount();

    internal ToggleFloorRoomEntity(OracleRoomData room, DungeonInteractionDatabase data,
        Func<byte> activeTile, Func<byte, byte, bool> createChild, Func<int> pendingCount,
        Func<IRoomEntity, bool> isOutgoing, Action roomTileChanged, Func<long> animationTick)
    {
        _room = room;
        _firstTile = data.Constant("red-toggle-floor");
        _activeTile = activeTile;
        _createChild = createChild;
        _pendingCount = pendingCount;
        _isOutgoing = isOutgoing;
        Name = "ToggleFloorRoomEntity";
        // Restore visible colored floors from the room's underlying buffer,
        // including an ordinary screen round trip after a Somaria overlay.
        if (room.RestoreUnderlyingMetatileRange((byte)_firstTile,3,animationTick()))
            roomTileChanged();
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Initialize()) return;
        if (!LinkInAir(frame.Player)) { _lastTilePosition = _activeTile(); return; }
        int x = Mathf.FloorToInt(frame.Player.Position.X)&15;
        int y = (Mathf.FloorToInt(frame.Player.Position.Y)+5)&15;
        if (x is < 4 or > 12 || y is < 4 or > 12) return;
        byte current = LinkTilePosition(frame.Player);
        if (current == _lastTilePosition) return;
        // var30 changes before the colored-tile and capacity tests. A failed
        // allocation is not retried while Link remains over this same tile.
        _lastTilePosition = current;
        int tile = _room.GetPackedStorageMetatile(current);
        if (unchecked((byte)(tile-_firstTile)) >= 3) return;
        _createChild(current,_activeTile());
    }

    private bool Initialize()
    {
        if (_isOutgoing(this)) Finished = true;
        if (Finished) return true;
        if (_initialized) return false;
        _initialized = true;
        _lastTilePosition = _activeTile();
        return true;
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Initialize();
        return ScreenTransitionPresentation.Visible;
    }

    public void UpdateDuringScreenTransition(RoomEntityFrame frame)
    {
        // An initialized parent has no always-update bit. Only state zero is
        // admitted by the native scroll dispatcher, even before its enabled02 gate.
        if (!_initialized) Initialize();
    }

    internal static bool LinkInAir(Player player)
    {
        try { return player.TopDownAirborne || player.NativeInAirForInteraction; }
        catch (NotSupportedException error)
        {
            throw new NotSupportedException($"INTERAC$15 wLinkInAir input: {error.Message}",error);
        }
    }

    internal static byte LinkTilePosition(Player player) => unchecked((byte)(
        ((Mathf.FloorToInt(player.Position.Y)+5)&0xf0) |
        ((Mathf.FloorToInt(player.Position.X)>>4)&15)));

    public void SetTransitionDrawOffset(Vector2 offset) { }
}
