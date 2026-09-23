using Godot;
using System;

namespace oracleofages;

internal sealed class SwitchHookController(Node worldRoot, RoomSession rooms, RoomEntityManager entities,
    Action<int> sound, Func<long> animationTick, Action<Vector2, int> breakEffect, Func<bool> pushBlockActive)
{
    private readonly SwitchHookDatabase _database = new();
    private readonly BreakableTileDatabase _breakables = new();
    private Player? _player;
    internal SwitchHookItem? Item { get; private set; }
    internal bool Active { get; private set; }
    internal SwitchHookHelper? Helper { get; private set; }
    internal bool ExchangeActive => Item is { Finished: false, State: 3 };
    internal int ExchangeState => ExchangeActive ? Item!.Substate == 0 ? 1 : 2 : 0;
    internal Vector2? CameraFocus => Helper is { Initialized: true } helper ? helper.Position : null;
    internal Player Player => _player ?? throw new InvalidOperationException("Switch Hook has no active Link parent.");

    internal bool TryBegin(Player player, Vector2 input)
    {
        // switchHookParent @state0 tests wLinkObjectIndex bit 0 (mounted
        // companion/cart/raft), not wLinkRidingObject's platform support.
        if (Active || player.Inventory.SwitchHookLevel == 0 || player.TopDownAirborne || player.SideScrollAirborne ||
            player.IsFallingInHole || player.IsPullingIntoHole || player.CompanionRideActive ||
            player.MinecartRideActive || player.RaftRideActive) return false;
        if ((rooms.CurrentRoom.TilesetFlags & 0x40) != 0)
            throw new NotSupportedException("Switch Hook underwater LINK_ANIM_MODE_2e presentation is not implemented.");
        if (Item is not null) { Item.Free(); Item = null; }
        player.SelectCarriedObjectReleaseDirection(input);
        int direction = player.FacingVector == Vector2I.Up ? 0 : player.FacingVector == Vector2I.Right ? 1 :
            player.FacingVector == Vector2I.Down ? 2 : 3;
        _player = player;
        Item = new(_database, player.Inventory.SwitchHookLevel, player.Position, direction, sound, this);
        worldRoot.AddChild(Item);
        Active = true;
        player.BeginSwitchHookPose();
        player.NotifyParentItemAnimationStarted(InventoryState.ItemSwitchHook);
        return true;
    }

    internal void UpdateParent(Player player)
    {
        if (!Active) return;
        if (Item is null || Item.Finished) { Active = false; return; }
        CompanionRuntimeState.SetMountingLock(entities.RuntimeState, 1);
        if (player.KnockbackFrames > 0) Item.RequestCancellation();
    }
    internal void UpdateItem(Player player, bool frozen) =>
        Item?.UpdateItem(rooms.CurrentRoom, player.Position, frozen, () => entities.TryAllocateSwitchHookChain(Item));
    internal void UpdateHelper(bool frozen)
    {
        if (Helper is null || frozen && Helper.Initialized) return;
        if (!Helper.Initialized) Helper.Initialize(Player);
        else if (Item is null || Item.Finished) Helper = null;
    }
    internal void BeginHelper(Vector2 position)
    {
        Helper = new(position);
        Player.ClearInteractionKnockback(clearInvincibility: true);
    }
    internal void CreateClink(Vector2 point, bool initializeOnUpdate = false) =>
        entities.Spawn(new EnemyClinkSpawn(point, initializeOnUpdate));
    internal bool CanLiftEnemy(Vector2 position) => !pushBlockActive() &&
        !LinkWallProbe.Shared.SurroundedByWalls(position,
            (rooms.CurrentRoom.TilesetFlags & 0x20) != 0, rooms.CurrentRoom.IsSolid);
    internal bool TryLiftTile(Vector2 position, out BreakableTileBreak result, out Texture2D texture)
    {
        texture = rooms.CurrentRoom.BuildMimickedMetatileTexture(position);
        if (_breakables.TryBreak(rooms.CurrentRoom, BreakableTileDatabase.SourceSwitchHook, position,
            rooms.SaveData, rooms.ActiveGroup, animationTick, null, out result) != BreakableTileBreakStatus.Broken)
            return false;
        result.ApplyCommonEffects(sound, entities.SpawnBreakableDrop);
        return true;
    }
    internal bool CanPlaceDiamond(Vector2 point) =>
        rooms.CurrentRoom.GetTerrainInfo(point).Collision == 0 || rooms.CurrentRoom.GetMetatile(point) == 0xda;
    internal void CompleteTile(BreakableTileBreak tile, Vector2 point)
    {
        if (tile.FormerTile == 0xdb && CanPlaceDiamond(point))
        {
            if (!rooms.CurrentRoom.ReplaceMetatile(point, rooms.CurrentRoom.GetMetatile(point), tile.FormerTile, animationTick()))
                throw new InvalidOperationException($"Switch Hook diamond placement failed at {point} in {rooms.ActiveGroup:x1}:{rooms.CurrentRoom.Id:x2}.");
        }
        else breakEffect(point, tile.Record.Effect);
    }
    internal void UpdatePost(Player player) => Item?.UpdatePost(player.Position, Active);
    internal void ClearParent() => Active = false;
    internal void Interrupt(bool discard)
    {
        if (discard) Cancel();
        else Item?.RequestCancellation();
    }
    internal void Cancel()
    {
        bool exchanging = ExchangeActive || Helper is not null;
        Active = false;
        Helper = null;
        if (exchanging && _player is not null && GodotObject.IsInstanceValid(_player)) _player.ClearSwitchHookZ();
        _player = null;
        if (Item is not null) { Item.Delete(); Item.Free(); Item = null; }
    }
}
