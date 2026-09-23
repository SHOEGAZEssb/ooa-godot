using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSmogReward()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput",flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates",flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate",flags)!.CreateDelegate(typeof(Action),this);
        foreach (bool batch in new[] { false,true })
        {
            void Step(int count = 1, bool approach = false)
            {
                input.CaptureForValidation([],[],approach ? Vector2.Up : Vector2.Zero);
                if (batch) scheduler.Advance(count / 60.0,update);
                else for (int i = 0; i < count; i++) scheduler.Advance(1.0 / 60.0,update);
            }
            void Load(bool bossFlag, bool itemFlag)
            {
                _saveData.SetRoomFlag(4,0xbf,0x80,bossFlag);
                _saveData.SetRoomFlag(4,0xbf,0x20,itemFlag);
                LoadValidationRoom(4,0xbf); _player.WarpTo(new(120,120));
            }
            void CollisionLock() => typeof(RoomEntityManager).GetMethod("DisableLinkCollisionsAndMenu",flags)!.Invoke(_entities,null);

            Load(false,false); Step();
            FailIf(_entities.Entities<GroundTreasurePickup>().Count != 0 || _saveData.HasRoomFlag(4,0xbf,0x80),
                "Smog reward must wait while its retained enemy count is nonzero.");
            var controller = ((List<IRoomEntity>)typeof(RoomEntityManager).GetField("_activeEntities",flags)!.GetValue(_entities)!)
                .OfType<SmogEncounterRoomEntity>().Single();
            // Isolate the terminal controller/reward handoff, not a fight.
            typeof(SmogEncounterController).GetProperty("State",flags)!.SetValue(controller.Controller,9);
            typeof(SmogEncounterController).GetProperty("Phase",flags)!.SetValue(controller.Controller,3);
            typeof(RoomEntityManager).GetMethod("ReleaseSmogLinkAndMenu",flags)!.Invoke(_entities,null);
            CollisionLock(); Step();
            var heart = _entities.Entities<GroundTreasurePickup>().Single();
            FailIf(!controller.Finished || !_saveData.HasRoomFlag(4,0xbf,0x80) ||
                _saveData.HasRoomFlag(4,0xbf,0x20) || _entities.LinkCollisionsAndMenuDisabled ||
                heart.Record.TreasureObject != "TREASURE_OBJECT_HEART_CONTAINER_00" || heart.Position != new Vector2(120,88),
                "Same interaction pass must observe Smog's final decrement, set flag$80, spawn heart at$58,$78 and unlock collisions/menu.");
            int maxHealth = _inventory.MaxHealthQuarters;
            FailIf(_currentRoom.IsSolid(_player.Position),"Heart-container approach must start on actual room floor.");
            for (int i = 0; i < 40 && !_saveData.HasRoomFlag(4,0xbf,0x20); i++) Step(1,true);
            FailIf(!_saveData.HasRoomFlag(4,0xbf,0x20) || _inventory.MaxHealthQuarters != maxHealth + 4,
                "Approaching Smog's heart through real room geometry must grant one container and persist the item flag.");
            _dialogue.Close(); Step(2);
            LoadValidationRoom(4,0xbf); Step(2);
            FailIf(_entities.Entities<GroundTreasurePickup>().Count != 0 || _entities.RoomEnemyCount != 0,
                "Collected Smog heart and defeated boss must stay absent on re-entry.");

            // Source jumps over checknoenemies when room flag$80 is set.
            Load(true,false);
            _entities.Spawn<SmogCharacter>(new SmogEnemySpawn(new(56,104),2));
            CollisionLock(); Step();
            FailIf(_entities.RoomEnemyCount != 1 || _entities.Entities<GroundTreasurePickup>().Count != 1 ||
                _entities.LinkCollisionsAndMenuDisabled,
                "Already-cleared boss room must respawn an uncollected heart even with a live enemy count.");

            // Item flag is checked after checknoenemies/orroomflag, and its
            // early script end must not execute enableLinkAndMenu.
            Load(false,true); Step(); CollisionLock(); Step();
            FailIf(_saveData.HasRoomFlag(4,0xbf,0x80) || !_entities.LinkCollisionsAndMenuDisabled,
                "Item flag alone must not skip the unresolved boss count or unlock Link.");
            _entities.ReleaseSmogSentinelCount(); Step();
            FailIf(!_saveData.HasRoomFlag(4,0xbf,0x80) || _entities.Entities<GroundTreasurePickup>().Count != 0 ||
                !_entities.LinkCollisionsAndMenuDisabled,
                "Zero count must set flag$80 before stopifitemflagset ends without spawning or unlocking.");

            Load(true,false);
            typeof(RoomEntityManager).GetMethod("PrepareIncomingEntitiesForScreenTransition",flags)!.Invoke(_entities,[_player]);
            FailIf(_entities.Entities<GroundTreasurePickup>().Count != 1 || _entities.RoomEnemyCount != 0,
                "State0 reward script must recreate an uncollected heart during cleared-room preload.");
        }
        LoadValidationRoom(0,0x60);
        GD.Print("Validated Smog's isolated controller/reward handoff, real-floor heart collection, re-entry and source-ordered flag/count gates.");
    }
}
