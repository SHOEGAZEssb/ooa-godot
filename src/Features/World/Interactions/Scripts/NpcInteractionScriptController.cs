using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Schedules the independent linked-NPC, past-Bipin, and hardhat script lanes.
/// InteractionController delegates matching and fixed-update advancement here
/// without retaining their talk graphs.
/// </summary>
internal sealed class NpcInteractionScriptController
{
    private readonly NpcInteractionCommandHost[] _hosts;
    private readonly NpcInteractionHandler[] _handlers;
    private readonly LinkedGameNpcScriptHost _linked;
    private readonly PastBipinScriptHost _pastBipin;
    private readonly HardhatShovelScriptHost _hardhat;
    private double _frameAccumulator;

    public NpcInteractionScriptController(
        RoomSession rooms,
        RoomEntityManager entities,
        DialogueBox dialogue,
        TreasureDatabase treasures,
        BipinBlossomFamilyStateResolver family)
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
        _hardhat = new HardhatShovelScriptHost(
            rooms,
            entities,
            dialogue,
            scripts.HardhatShovel,
            treasures,
            new BlackTowerWorkerDatabase());
        _hosts = [_linked, _pastBipin, _hardhat];
        _handlers =
        [
            NpcInteractionHandler.ForNpc(
                "linkedGameNpcScript",
                _linked.TryInteract),
            NpcInteractionHandler.ForNpc(
                "bipinScript3",
                _pastBipin.TryInteract),
            NpcInteractionHandler.ForNpc(
                "hardhatWorkerSubid00Script",
                _hardhat.TryInteract)
        ];
        rooms.RoomChanged += OnRoomChanged;
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
            return false;
        }
    }

    internal GroundTreasurePickup? PastBipinTreasure =>
        _pastBipin.Treasure;
    internal LinkedGameNpcScriptHost Linked => _linked;
    internal PastBipinScriptHost PastBipin => _pastBipin;
    internal HardhatShovelScriptHost Hardhat => _hardhat;
    internal IReadOnlyList<NpcInteractionHandler> Handlers => _handlers;
    internal ICutsceneCommandTraceSink? TraceSink
    {
        set
        {
            foreach (NpcInteractionCommandHost host in _hosts)
                host.SetTraceSink(value);
        }
    }

    public void Update(double delta)
    {
        _frameAccumulator += delta * 60.0;
        while (_frameAccumulator >= 1.0)
        {
            _frameAccumulator -= 1.0;
            foreach (NpcInteractionCommandHost host in _hosts)
                host.AdvanceFrame();
        }
    }

    private void OnRoomChanged(int group, OracleRoomData room)
    {
        _ = group;
        _ = room;
        foreach (NpcInteractionCommandHost host in _hosts)
            host.Cancel();
        _frameAccumulator = 0.0;
    }
}
