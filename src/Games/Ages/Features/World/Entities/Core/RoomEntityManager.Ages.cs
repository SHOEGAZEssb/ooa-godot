using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

public sealed partial class RoomEntityManager
{
    private readonly AgesRoomEntityFactory _factory;

    public RoomEntityManager(
        Node worldRoot,
        NpcDatabase npcs,
        EnemyDatabase enemies,
        OracleSaveData? saveData = null,
        OracleRuntimeState? runtimeState = null)
        : this(worldRoot, npcs, enemies, new ItemDropDatabase(enemies.AssetRoot),
            new TimePortalDatabase(), new OracleRandom(), saveData, runtimeState)
    { }

    internal RoomEntityManager(
        Node worldRoot,
        NpcDatabase npcs,
        EnemyDatabase enemies,
        ItemDropDatabase itemDrops,
        TimePortalDatabase timePortals,
        OracleRandom random,
        OracleSaveData? saveData = null,
        OracleRuntimeState? runtimeState = null,
        InventoryState? inventory = null,
        Func<long>? animationTick = null,
        TreasureDatabase? treasures = null,
        RoomSession? rooms = null)
    {
        _worldRoot = worldRoot;
        AssetRoot = enemies.AssetRoot;
        if (AssetRoot != AgesGameContent.AssetRoot || npcs.AssetRoot != AssetRoot || itemDrops.AssetRoot != AssetRoot ||
            treasures is not null && treasures.AssetRoot != AssetRoot ||
            inventory is not null && inventory.AssetRoot != AssetRoot || rooms is not null && rooms.AssetRoot != AssetRoot ||
            saveData is not null && saveData.Game != OracleGame.Ages ||
            runtimeState is not null && runtimeState.Game != OracleGame.Ages)
            throw new ArgumentException("The Ages entity binding requires matching Ages source packs, save and runtime state.");
        _random = random;
        _itemDrops = itemDrops;
        _saveData = saveData;
        _inventory = inventory;
        _treasures = treasures ?? new TreasureDatabase(AssetRoot);
        _runtimeState = runtimeState ?? OracleRuntimeState.CreateInitialized(OracleAssetPack.GameFor(AssetRoot));
        _random.BindPlacementMemory(_runtimeState);
        _animationTick = animationTick ?? (() => 0);
        _familyState = new BipinBlossomFamilyStateResolver(npcs);
        _factory = new AgesRoomEntityFactory(this, enemies, timePortals, rooms);
        if (_saveData is not null)
            _saveData.Changed += RefreshNpcState;
        _runtimeState.Changed += RefreshNpcState;
    }
}
