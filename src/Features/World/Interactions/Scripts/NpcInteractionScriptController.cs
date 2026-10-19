using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Schedules conversation lanes and binds Postman's entity-owned lane.
/// InteractionController delegates matching and cancellation here.
/// </summary>
internal sealed class NpcInteractionScriptController
{
    private readonly NpcInteractionCommandHost[] _hosts;
    private readonly NpcInteractionHandler[] _handlers;
    private readonly LinkedGameNpcScriptHost _linked;
    private readonly PastBipinScriptHost _pastBipin;
    private readonly HardhatShovelScriptHost _hardhat;
    private readonly PostmanScriptHost _postman;
    private readonly OldManRupeesScriptHost _oldMan;
    private double _frameAccumulator;
    private KnowItAllBirdScriptHost? _birdTalk;
    private readonly List<SharedNpcScriptHost> _shared = new();
    private readonly List<BlackTowerBlockerScriptHost> _blockers = new();
    private readonly List<BlackTowerPatrolScriptHost> _patrollers = new();
    private readonly SharedNpcDatabase _sharedData = new();

    public NpcInteractionScriptController(
        RoomSession rooms,
        RoomEntityManager entities,
        DialogueBox dialogue,
        TreasureDatabase treasures,
        BipinBlossomFamilyStateResolver family,
        InventoryState inventory,
        Func<bool> rupeeDisplayUpdated)
    {
        var scripts = new NpcInteractionScriptDatabase();
        _linked = new LinkedGameNpcScriptHost(
            rooms,
            entities,
            dialogue,
            scripts.LinkedGameNpc,
            new LinkedGameNpcDatabase());
        _pastBipin = new PastBipinScriptHost(
            rooms,
            entities,
            dialogue,
            scripts.PastBipin,
            treasures,
            family);
        var blackTower = new BlackTowerWorkerDatabase();
        _hardhat = new HardhatShovelScriptHost(
            rooms,
            entities,
            dialogue,
            scripts.HardhatShovel,
            treasures,
            blackTower);
        _postman = new PostmanScriptHost(
            rooms,
            entities,
            dialogue,
            scripts.Postman,
            treasures,
            inventory);
        _oldMan = new OldManRupeesScriptHost(rooms, entities, dialogue,
            scripts.OldManRupees, inventory, rupeeDisplayUpdated);
        _hosts = [_linked, _pastBipin, _hardhat, _postman, _oldMan];
        _handlers =
        [
            NpcInteractionHandler.ForNpc("sharedNpcScript", (target, player) =>
            {
                foreach (var host in _shared)
                    if (host.TryInteract(target, player)) return true;
                return false;
            }),
            NpcInteractionHandler.ForNpc("knowItAllBirdScript", (target, player) =>
            {
                if (target.Npc is not KnowItAllBirdCharacter bird) return false;
                if (!bird.Script.TryInteract(target, player, dialogue, entities.WorldToScreen)) return false;
                _birdTalk = bird.Script;
                return true;
            }),
            NpcInteractionHandler.ForNpc("oldManScript_takesRupees", _oldMan.TryInteract),
            NpcInteractionHandler.ForNpc("villagerSubid02Script_part1", (target, player) =>
            {
                foreach (var host in _blockers)
                    if (host.TryInteract(target, player)) return true;
                return false;
            }),
            NpcInteractionHandler.ForNpc("hardhatWorkerSubid03Script", (target, player) =>
            {
                foreach (var host in _patrollers)
                    if (host.TryInteract(target, player)) return true;
                return false;
            }),
            NpcInteractionHandler.ForNpc(
                "linkedGameNpcScript",
                _linked.TryInteract),
            NpcInteractionHandler.ForNpc(
                "bipinScript3",
                _pastBipin.TryInteract),
            NpcInteractionHandler.ForNpc(
                "hardhatWorkerSubid00Script",
                _hardhat.TryInteract),
            NpcInteractionHandler.ForNpc(
                "postmanScript",
                _postman.TryInteract)
        ];
        rooms.RoomChanged += OnRoomChanged;
        entities.RoomEntitiesLoaded += (_, _) =>
        {
            foreach (var host in _shared) host.Cancel();
            _shared.Clear();
            foreach (var host in _blockers) host.Cancel();
            _blockers.Clear();
            foreach (var host in _patrollers) host.Cancel();
            _patrollers.Clear();
            foreach (var slot in entities.EntityAdapters<BlackTowerBlockingVillagerRoomEntity>())
            {
                var host = new BlackTowerBlockerScriptHost(slot, blackTower, rooms, entities, dialogue);
                slot.Script = host;
                _blockers.Add(host);
            }
            foreach (var slot in entities.EntityAdapters<BlackTowerPatrollingWorkerRoomEntity>())
            {
                var host = new BlackTowerPatrolScriptHost(slot, blackTower, rooms, entities, dialogue);
                slot.Script = host;
                _patrollers.Add(host);
            }
            foreach (var slot in entities.EntityAdapters<SharedScriptNpcRoomEntity>())
            {
                var host = new SharedNpcScriptHost(slot, _sharedData,
                    rooms, entities, dialogue, inventory, rupeeDisplayUpdated);
                slot.Script = host;
                _shared.Add(host);
            }
            _postman.Cancel();
            foreach (PostmanCharacter postman in entities.Entities<PostmanCharacter>())
                postman.BindScript(_postman);
        };
    }

    public bool BlocksGameplay
    {
        get
        {
            foreach (NpcInteractionCommandHost host in _hosts)
            {
                if (host.BlocksGameplay)
                    return true;
            }
            foreach (var host in _shared)
                if (host.BlocksGameplay) return true;
            return false;
        }
    }

    internal GroundTreasurePickup? PastBipinTreasure =>
        _pastBipin.Treasure;
    internal GroundTreasurePickup? PostmanTreasure =>
        _postman.Treasure;
    internal HardhatShovelScriptHost Hardhat => _hardhat;
    internal PostmanScriptHost Postman => _postman;
    internal OldManRupeesScriptHost OldMan => _oldMan;
    internal IReadOnlyList<NpcInteractionHandler> Handlers => _handlers;
    internal ICutsceneCommandTraceSink? TraceSink
    {
        set
        {
            foreach (NpcInteractionCommandHost host in _hosts)
                host.SetTraceSink(value);
            foreach (var host in _blockers) host.SetTraceSink(value);
            foreach (var host in _patrollers) host.SetTraceSink(value);
        }
    }

    public void Update(double delta)
    {
        _frameAccumulator += delta * 60.0;
        while (_frameAccumulator >= 1.0)
        {
            _frameAccumulator -= 1.0;
            foreach (NpcInteractionCommandHost host in _hosts)
                if (!ReferenceEquals(host, _postman)) host.AdvanceFrame();
        }
    }

    private void OnRoomChanged(int group, OracleRoomData room)
    {
        _birdTalk?.Cancel();
        _birdTalk = null;
        _ = group;
        _ = room;
        foreach (NpcInteractionCommandHost host in _hosts)
            host.Cancel();
        foreach (var host in _shared) host.Cancel();
        _shared.Clear();
        foreach (var host in _blockers) host.Cancel();
        _blockers.Clear();
        foreach (var host in _patrollers) host.Cancel();
        _patrollers.Clear();
        _frameAccumulator = 0.0;
    }
}
