using System;
using System.Collections.Generic;

namespace Oracles;

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
    private readonly List<TokayNpcScriptHost> _tokays = new();
    private readonly List<DimitriRescueScriptHost> _dimitriRescue = new();
    private readonly List<BlackTowerBlockerScriptHost> _blockers = new();
    private readonly List<BlackTowerPatrolScriptHost> _patrollers = new();
    private readonly SharedNpcDatabase _sharedData = new();
    private readonly FamilyNpcScriptDatabase _familyData = new();
    private readonly List<FamilyNpcScriptHost> _familyHosts = new();
    private readonly FaroreScriptDatabase _faroreData = new();
    private FaroreScriptHost? _farore;
    private FaroreMemoryScriptHost? _book;
    internal Func<bool>? OpenSecretList { get; set; }
    internal Func<Action<int?>, bool>? OpenFaroreSecretMenu { get; set; }
    internal RoomTransitionController? FaroreTransitions { get; set; }
    internal void AdvanceFarorePaletteBeforeObjects() => _farore?.AdvancePaletteBeforeObjects();

    public NpcInteractionScriptController(
        RoomSession rooms,
        RoomEntityManager entities,
        DialogueBox dialogue,
        TreasureDatabase treasures,
        BipinBlossomFamilyStateResolver family,
        InventoryState inventory,
        Func<bool> rupeeDisplayUpdated,
        Action redrawRoom)
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
            NpcInteractionHandler.ForNpc("tokay.s:tokayHoldingItemScript/genericNpcScript", (target, player) =>
            {
                foreach (var host in _tokays)
                    if (host.TryInteract(target, player)) return true;
                return false;
            }),
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
            NpcInteractionHandler.ForNpc("bipin/blossom/child family scripts", (target, player) =>
            {
                foreach (FamilyNpcScriptHost host in _familyHosts)
                    if (host.TryInteract(target, player)) return true;
                return false;
            }),
            NpcInteractionHandler.ForNpc("commonScripts.s:faroreScript", (target, player) =>
                _farore?.TryInteract(target, player) == true),
            NpcInteractionHandler.ForNpc("commonScripts.s:faroresMemoryScript", (target, player) =>
                _book?.TryInteract(target, player) == true),
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
            _book?.Cancel(); _book = null;
            foreach (var slot in entities.EntityAdapters<FaroreMemoryRoomEntity>())
            {
                _book = new FaroreMemoryScriptHost(slot.Npc, rooms, entities, dialogue,
                    () => OpenSecretList?.Invoke() == true);
                slot.Script = _book;
            }
            _farore?.Cancel();
            _farore = null;
            foreach (FaroreRoomEntity slot in entities.EntityAdapters<FaroreRoomEntity>())
            {
                _farore = new FaroreScriptHost(slot.Npc, _faroreData,
                    rooms, entities, dialogue, inventory, treasures,
                    callback => OpenFaroreSecretMenu?.Invoke(callback) == true,
                    redrawRoom, FaroreTransitions ?? throw new InvalidOperationException("Farore requires the shared palette/camera owner."));
                slot.Script = _farore;
            }
            foreach (FamilyNpcScriptHost host in _familyHosts) host.Cancel();
            _familyHosts.Clear();
            foreach (FamilyNpcRoomEntity slot in entities.EntityAdapters<FamilyNpcRoomEntity>())
            {
                if (!_familyData.HasScript(slot.Npc.Record)) continue;
                var host = new FamilyNpcScriptHost(slot.Npc, _familyData, rooms,
                    entities, dialogue, inventory, treasures);
                slot.Script = host;
                _familyHosts.Add(host);
            }
            foreach (FamilyChildRoomEntity slot in entities.EntityAdapters<FamilyChildRoomEntity>())
            {
                if (!_familyData.HasScript(slot.Npc.Record)) continue;
                var host = new FamilyNpcScriptHost(slot.Npc, _familyData, rooms,
                    entities, dialogue, inventory, treasures);
                slot.Script = host;
                _familyHosts.Add(host);
            }
            foreach (RunningBipinRoomEntity slot in entities.EntityAdapters<RunningBipinRoomEntity>())
            {
                if (slot.Npc.Record.Group != 2 || slot.Npc.Record.Room != 0xea) continue;
                var host = new FamilyNpcScriptHost(slot.Npc, _familyData, rooms,
                    entities, dialogue, inventory, treasures);
                slot.Script = host;
                _familyHosts.Add(host);
            }
            foreach(var host in _dimitriRescue)host.Cancel();_dimitriRescue.Clear();
            foreach(var slot in entities.EntityAdapters<DimitriRescueScriptRoomEntity>())
            {
                var host=new DimitriRescueScriptHost(slot,rooms,entities,dialogue);
                slot.Script=host;_dimitriRescue.Add(host);
            }
            foreach (var host in _tokays) host.Cancel();
            _tokays.Clear();
            foreach (var slot in entities.EntityAdapters<TokayNpcRoomEntity>())
            {
                if (!TokayNpcScriptHost.Matches(slot.Npc)) continue;
                var host = new TokayNpcScriptHost(slot, new TokayInteractionDatabase(), rooms, entities, dialogue, inventory);
                slot.Script = host;
                _tokays.Add(host);
            }
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
            foreach (var host in _familyHosts)
                if (host.BlocksGameplay) return true;
            if (_farore?.BlocksGameplay == true) return true;
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
            foreach (var host in _tokays) host.SetTraceSink(value);
            foreach (var host in _familyHosts) host.SetTraceSink(value);
            _farore?.SetTraceSink(value);
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
        _book?.Cancel(); _book = null;
        _farore?.Cancel();
        _farore = null;
        foreach (FamilyNpcScriptHost host in _familyHosts) host.Cancel();
        _familyHosts.Clear();
        foreach(var host in _dimitriRescue)host.Cancel();_dimitriRescue.Clear();
        _birdTalk?.Cancel();
        _birdTalk = null;
        foreach (var host in _tokays) host.Cancel();
        _tokays.Clear();
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
