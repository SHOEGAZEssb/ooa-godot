using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private sealed class SmogControllerWorld : ISmogEncounterWorld
    {
        public int RoomFlags { get; set; }
        public bool EntryBusy { get; set; }
        public int EnemyCount { get; set; } = 1;
        public Vector2I LinkHighPosition { get; set; }
        public byte LinkZHigh { get; set; }
        public bool LinkNormal { get; set; } = true;
        public bool LinkInAir { get; set; }
        public byte LinkHealth { get; set; } = 12;
        public void ApplyResetPenalty() { if (LinkHealth >= 12) LinkHealth -= 4; }
        internal byte Button = 0x0c;
        internal readonly List<string> Events = [];
        internal readonly List<SmogEnemySpawn> Spawns = [];
        public void LockLinkAndMenu() => Events.Add("lock");
        public void EnableLinkCollisionsAndMenu() => Events.Add("enable");
        public void SetResetFlag(bool enabled) => Events.Add(enabled ? "flag40:on" : "flag40:off");
        public void Spawn(SmogEnemySpawn spawn) { Spawns.Add(spawn); Events.Add($"spawn:{spawn.SubId:x2}"); }
        public void Puff(Vector2I position) => Events.Add($"puff:{position.X},{position.Y}");
        public byte Collision(int packedPosition) => 0;
        public byte Tile(Vector2I position) => Button;
        public bool SetTile(int position,int tile) { Events.Add($"tile:{position:x2}:{tile:x2}"); return true; }
        public void MergeClouds(int phase) => Events.Add($"merge:{phase}");
        public void PlayResetSound() => Events.Add("splash");
        public void DecrementEnemyCount() => Events.Add("decrement");
    }

    private void ValidateSmogController()
    {
        var data = new SmogControllerDatabase();
        // Isolate native handlers without playing through prior phases. These
        // fixture writes stay in validation; production exposes no state setter.
        SmogEncounterController At(int state,int phase = 0,int spawnIndex = 0)
        {
            var controller = new SmogEncounterController(data,new(24,20));
            foreach (var pair in new[] { ("State",state),("Phase",phase),("SpawnIndex",spawnIndex) })
                typeof(SmogEncounterController).GetProperty(pair.Item1,BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(controller,pair.Item2);
            return controller;
        }
        void Field(SmogEncounterController controller,string name,int value) =>
            typeof(SmogEncounterController).GetField(name,BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(controller,value);
        void Step(SmogEncounterController controller,SmogControllerWorld world,int count)
        { for (int i = 0; i < count; i++) controller.Update(world); }

        var world = new SmogControllerWorld { EntryBusy = true };
        var actor = At(0,spawnIndex:0xff);
        Step(actor,world,2);
        FailIf(actor.State != 0 || !world.Events.SequenceEqual(new[] { "lock","lock" }),
            "INTERAC$33 must keep menu/object locks while the boss-entry signal is nonzero.");
        world.EntryBusy = false; actor.Update(world);
        FailIf(actor.State != 1 || actor.SpawnIndex != 0 || world.Spawns.Single() != data.Intro ||
            !world.Events.TakeLast(3).SequenceEqual(new[] { "lock","spawn:00","puff:24,20" }),
            "Cleared entry signal must spawn intro$00 then puff on the same state0 update.");
        world = new() { RoomFlags = 0x80 }; actor = At(0);
        actor.Update(world);
        FailIf(!actor.Finished || world.Events.Count != 0, "Completed-room flag$80 must delete INTERAC$33 before taking locks.");

        world = new() { LinkHighPosition = new(0x77,0x57) }; actor = At(2);
        Step(actor,world,6);
        FailIf(actor.State != 2 || world.LinkZHigh != 0xfa, "Link lift must remain in state2 through six byte decrements.");
        actor.Update(world);
        FailIf(actor.State != 3 || world.LinkZHigh != 0xf9, "Seventh lift update must choose phase0's destination.");
        actor.Update(world);
        FailIf(world.LinkHighPosition != new Vector2I(0x77,0x58), "Smog repositioning must move Y before X, one high-byte unit per update.");
        actor.Update(world);
        FailIf(actor.State != 3 || world.LinkHighPosition != new Vector2I(0x78,0x58), "Arrival movement must not enter lowering until the next update.");
        actor.Update(world); Step(actor,world,6);
        FailIf(actor.State != 4 || world.LinkZHigh != 0xff, "Lowering must wait for the seventh increment's byte wrap.");
        actor.Update(world);
        FailIf(actor.State != 5 || world.LinkZHigh != 0, "Seventh lowering update must enter state5.");

        world = new() { EnemyCount = 2 }; actor = At(5);
        actor.Update(world);
        FailIf(actor.State != 5, "Tile setup requires enemy count exactly1.");
        world.EnemyCount = 1; actor.Update(world);
        FailIf(actor.State != 6 || actor.Counter != 5 || world.Events.Count != 0, "State5 must initialize the tile delay without writing a tile.");
        Step(actor,world,50);
        FailIf(actor.State != 7 || actor.Counter != 5 || world.Spawns.Count != 0, "Phase0's nine writes plus terminator must enter state7 without spawning.");
        Step(actor,world,4);
        FailIf(world.Spawns.Count != 0, "State7 must preserve its own five-update spawn delay.");
        actor.Update(world);
        FailIf(actor.State != 8 || actor.Position != new Vector2I(24,20) || actor.SpawnIndex != 2 ||
            !world.Events.TakeLast(4).SequenceEqual(new[] { "flag40:off","spawn:02","spawn:02","enable" }),
            "State7 must clear reset flag, spawn in source order, enable Link collisions/menu and move to the reset button.");

        foreach (int health in new[] { 11,12,16 })
        foreach (int distance in new[] { 3,4 })
        {
            world = new() { EnemyCount = 3,LinkHealth = (byte)health,LinkHighPosition = new(26,20 + distance - 2) };
            actor = At(8,phase:2,spawnIndex:7); Field(actor,"_savedSpawnIndex",5);
            actor.Update(world);
            if (distance == 4)
                FailIf(actor.State != 8 || !world.Events.SequenceEqual(new[] { "merge:2" }) || world.LinkHealth != health,
                    "Reset button requires Manhattan distance strictly less than4.");
            else
                FailIf(actor.State != 10 || actor.Phase != 2 || actor.SpawnIndex != 5 || actor.Counter != 5 ||
                    world.LinkHealth != (health >= 12 ? health - 4 : health) ||
                    !world.Events.SequenceEqual(new[] { "tile:11:11","splash","flag40:on","lock" }),
                    "Reset must apply native health threshold, restore phase spawn index and enter cleanup without advancing phase.");
        }
        world = new() { EnemyCount = 2,Button = 0x11 }; actor = At(8);
        actor.Update(world);
        FailIf(actor.State != 8 || !world.Events.SequenceEqual(new[] { "merge:0" }), "Count2 must suppress the reset button even when its tile changed.");
        world = new() { EnemyCount = 3,Button = 0x11,LinkInAir = true,LinkNormal = false }; actor = At(8);
        actor.Update(world);
        FailIf(actor.State != 10 || world.Events.Contains("tile:11:11"), "Changed button tile must reset independently of Link's state/distance.");
        foreach (bool airborne in new[] { false,true })
        {
            world = new() { EnemyCount = 3,LinkHighPosition = new(24,20),LinkInAir = airborne,LinkNormal = airborne };
            actor = At(8); actor.Update(world);
            FailIf(actor.State != 8 || !world.Events.SequenceEqual(new[] { "merge:0" }), "Unchanged button must reject airborne or non-normal Link.");
        }
        world = new() { EnemyCount = 1 }; actor = At(8,3);
        actor.Update(world);
        FailIf(actor.State != 9 || actor.Finished || world.Events.Count != 0, "Count1 must defer phase advancement until the next update.");
        actor.Update(world); actor.Update(world);
        FailIf(!actor.Finished || actor.Phase != 4 || !world.Events.SequenceEqual(new[] { "decrement" }),
            "Final state9 must release the retained controller count exactly once.");
        world = new(); actor = At(9,1); actor.Update(world);
        FailIf(actor.State != 10 || actor.Phase != 2 || actor.Counter != 5, "Nonfinal phase completion must advance phase and initialize cleanup.");
        Step(actor,world,5);
        FailIf(actor.State != 1 || actor.Counter != 5 || world.Events.Count != 1, "Empty cleanup must return to state1 on its fifth update with counter1=5 and without starting Link movement.");
        GD.Print("Validated isolated Smog controller entry gate, Link repositioning, setup timing, reset predicates/penalty and phase completion.");
    }
}
