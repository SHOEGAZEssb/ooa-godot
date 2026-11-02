
namespace Oracles;

// bank1.s:screenTransitionState2, including native build gates.
internal sealed class ScreenBoundaryController
{
    private readonly OracleRuntimeState _state;
    private readonly Func<bool> _disabled;
    internal int Delay
    {
        get => _state.ReadField("wScreenTransitionDelay");
        private set => _state.SetField("wScreenTransitionDelay", checked((byte)value));
    }
    internal ScreenBoundaryController(OracleRuntimeState state, Func<bool>? disabled = null)
    {
        _state = state;
        _disabled = disabled ?? (static () => false);
    }
    internal void ClearDelay() => Delay = 0;

    internal Vector2I PendingDirection => Direction(_state.ReadField("wScreenTransitionDirection"));

    private static Vector2I Direction(int value) => value switch
    {
        0 => Vector2I.Up, 1 => Vector2I.Right, 2 => Vector2I.Down, 3 => Vector2I.Left,
        _ => throw new NotSupportedException($"bank1.screenTransitionState2: unsupported wScreenTransitionDirection ${value:x2}.")
    };

    internal void Request(Vector2I direction)
    {
        byte value = direction == Vector2I.Up ? (byte)0 : direction == Vector2I.Right ? (byte)1 :
            direction == Vector2I.Down ? (byte)2 : direction == Vector2I.Left ? (byte)3 :
            throw new ArgumentException("bank1.screenTransitionState2 requires a cardinal direction.",nameof(direction));
        _state.SetField("wScrollMode",4);
        _state.SetField("wScreenTransitionState",3);
        _state.SetField("wScreenTransitionDirection",value);
    }

    internal void Sample(Player player, OracleRoomData room, IPlayerScreenTransitionRoomEntity? owner = null)
    {
        if (OracleAssetPack.GameFor(room.AssetRoot) != _state.Game)
            throw new ArgumentException("A screen boundary must use its selected game state and room.");
        if (_state.ReadField("wScrollMode") == 0) return;
        if (player.DelaysOrdinaryScreenTransition)
            Delay = 4;

        int requested = _state.ReadField("wScreenTransitionDirection");
        if ((requested & 0x80) != 0)
        {
            Vector2I forced = Direction(requested & 0x7f);
            Request(forced);
            return;
        }

        Vector2 pixel = OracleObjectMath.ToPixelPosition(
            owner?.ScreenTransitionPosition ?? player.PrecisePosition);
        Vector2I vertical = pixel.Y <= 5 ? Vector2I.Up
            : pixel.Y > room.Height - 7 ? Vector2I.Down : Vector2I.Zero;
        Vector2I horizontal = pixel.X <= 5 ? Vector2I.Left
            : pixel.X > room.Width - 6 ? Vector2I.Right : Vector2I.Zero;

        // screenTransitionState2 always returns from its Y check to check X.
        // Both coordinates clamp, both checks can decrement the delay, and a
        // successful horizontal check overwrites a successful vertical request.
        Vector2I direction = Vector2I.Zero;
        if (vertical != Vector2I.Zero &&
            CheckOrdinaryScreenBoundary(player, owner, room, vertical))
            direction = vertical;
        if (horizontal != Vector2I.Zero &&
            CheckOrdinaryScreenBoundary(player, owner, room, horizontal))
            direction = horizontal;
        if (direction != Vector2I.Zero) Request(direction);
    }

    private bool CheckOrdinaryScreenBoundary(
        Player player, IPlayerScreenTransitionRoomEntity? owner,
        OracleRoomData room, Vector2I direction)
    {
        bool horizontal = direction.X != 0;
        int boundary = direction == Vector2I.Up || direction == Vector2I.Left
            ? 6 : horizontal ? room.Width - 6 : room.Height - 7;
        if (owner is null)
            player.SetScreenTransitionBoundaryCoordinate(horizontal, boundary);
        else
            owner.SetScreenTransitionBoundaryCoordinate(horizontal, boundary, player);
        if (_disabled() ||
            _state.ReadField("wDisableScreenTransitions") != 0)
            return false;
        if (Delay != 0)
        {
            Delay--;
            return false;
        }
        // SPECIALOBJECT_MINECART jumps directly to @startTransition, before
        // angle, overworld boundary, and hazard checks.
        if (owner?.BypassesScreenTransitionInputGate == true)
            return true;
        if (player.RejectsOrdinaryScreenTransition ||
            (!player.ScreenTransitionTerrainMotion &&
             !player.IsMovingTowardScreenEdge(direction)))
            return false;

        // Ages forbids outdoor map wrap except through the top edge.
        bool ages = _state.Game == OracleGame.Ages;
        if (ages && (room.TilesetFlags & (int)TilesetFlags.Outdoors) != 0 &&
            ((direction == Vector2I.Right && (room.Id & 0x0f) == 0x0f) ||
             (direction == Vector2I.Down && room.Id >= 0xf0) ||
             (direction == Vector2I.Left && (room.Id & 0x0f) == 0)))
            return false;

        // TILESETFLAG_UNDERWATER and the raft's wcc92 bit 3 skip hazards.
        // checkLinkIsOverHazard also returns zero for SPECIALOBJECT_DIMITRI.
        if (ages && ((room.TilesetFlags & (int)TilesetFlags.Underwater) != 0 || player.RaftRideActive) ||
            owner is DimitriCompanionRoomEntity)
            return true;
        Vector2 position = OracleObjectMath.ToPixelPosition(
            owner?.ScreenTransitionPosition ?? player.PrecisePosition);
        TerrainInfo terrain = room.GetTerrainInfo(position + new Vector2(0, 5));
        if (terrain.Hazard is HazardType.Hole or HazardType.Lava)
            return false;
        if (terrain.Hazard != HazardType.Water)
            return true;
        // @checkCanTransitionOverWater rejects ridden animals before checking
        // equipment; $fc requires Mermaid Suit even when Flippers are owned.
        return owner is null &&
            (ages && player.Inventory.HasTreasure(TreasureId.MermaidSuit) ||
             ((!ages || terrain.Tile != 0xfc) &&
              player.Inventory.HasTreasure(TreasureId.Flippers)));
    }

}
