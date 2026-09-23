using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCrownEyeChest()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput",flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates",flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate",flags)!.CreateDelegate(typeof(Action),this);
        var setTrigger = (Action<int,bool>)typeof(RoomEntityManager).GetMethod("SetTrigger",flags)!.CreateDelegate(typeof(Action<int,bool>),_entities);
        var data = new CrownDungeonDatabase();
        FailIf(data.TriggerChestValue != 7 || data.TriggerChestWait != 15 ||
            data.GetRoomRecords(4,0xba).Single().Order != 0,"Crown eye chest must retain source trigger07, wait15 and order0.");
        foreach (bool batch in new[] { false,true })
        {
            void Step(int count = 1,Vector2 move = default)
            {
                input.CaptureForValidation([],[],move);
                if (batch) scheduler.Advance(count / 60.0,update);
                else for (int i = 0; i < count; i++) scheduler.Advance(1.0 / 60.0,update);
            }
            _saveData.SetRoomFlag(4,0xba,0x20,false);
            LoadValidationRoom(4,0xba); _player.WarpTo(new(120,120));
            var script = _entities.Entities<DungeonTriggerChestScriptRoomEntity>().Single();
            FailIf(_entities.InteractionSlot(script) != 2,"Crown eye chest must occupy the first source interaction slot.");
            Step(); _sound.ClearPlayRequestAudit();
            setTrigger(7,true);
            var seeds = new SeedSatchelDatabase();
            FailIf(!seeds.TryGet(0x20,out var seed),"Missing Ember Seed fixture.");
            foreach (var eye in _entities.Entities<SeedShooterEyeStatueRoomEntity>())
            {
                Vector2 origin = eye.Position + Vector2.Down * 32;
                FailIf(_currentRoom.IsSolid(origin),"Each Crown eye shot must start on open floor.");
                _entities.Spawn<EmberSeedEffect>(new EmberSeedSpawn(origin,Vector2I.Up,seed,4,SeedLaunchKind.Shooter));
            }
            for (int i = 0; (_entities.ActiveTriggers & 7) != 7 && i < 24; i++) Step();
            FailIf(_entities.ActiveTriggers != 0x87 || script.Counter != -1 ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 0,
                "Crown chest must reject trigger87 even when all three eyes are active.");
            setTrigger(7,false); Step();
            FailIf(script.Counter != 15 || _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 1,
                "Exact trigger07 must begin the source solve/puff/wait sequence.");
            setTrigger(2,false); // A lost signal does not cancel an already-started script.
            Step(14);
            FailIf(script.Counter != 1 || _currentRoom.GetMetatile(new(120,88)) == 0xf1,
                "Crown chest must wait through counter01 before installing tilef1.");
            Step();
            FailIf(!script.Finished || _currentRoom.GetMetatile(new(120,88)) != 0xf1,
                "Crown chest must install tilef1 on the fifteenth update despite lost trigger2.");
            // Approach the completed chest along its actual south floor.
            for (int i = 0; _player.Position.Y > 100 && i < 30; i++) Step(move:Vector2.Up);
            FailIf(_currentRoom.IsSolid(_player.Position),"Crown chest approach entered solid geometry.");
            int keys = _inventory.GetDungeonSmallKeys(5);
            FailIf(!TryInteract(_player) || !_interactions.ChestRewardActive,"Crown chest must open from its reachable south side.");
            Step(32);
            FailIf(_inventory.GetDungeonSmallKeys(5) != keys + 1 || !_saveData.HasRoomFlag(4,0xba,0x20),
                "Crown chest must grant its source small key and persist the collected-item flag.");
            _dialogue.Close(); Step();
            LoadValidationRoom(4,0xba); Step();
            FailIf(_entities.Entities<DungeonTriggerChestScriptRoomEntity>().Count != 0,
                "Collected Crown eye chest script must stop on re-entry.");
        }
        LoadValidationRoom(0,0x60);
        GD.Print("Validated Crown eye chest exact trigger gate, projectile handoff, 15-update delay, real approach, key collection and re-entry.");
    }
}
