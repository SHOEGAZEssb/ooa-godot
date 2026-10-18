using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMoonlitEnemyAdmissionRom()
    {
        foreach (bool orbCase in new[] { true, false })
        foreach (bool batched in new[] { false, true })
        foreach (int free in new[] { 0, 1 })
        {
            ReinitializeGameplayForValidation(); int room = orbCase ? 0x56 : 0x5e;
            _saveData.SetRoomFlag(4, room, OracleSaveData.RoomFlagItem, false); LoadValidationRoom(4, room);
            _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(orbCase ? new(88, 136) : new(152, 40)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position), "Moonlit admission regression must approach over the original room floor.");
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            var parent = _entities.Entities<MoonlitGrottoArmosEventRoomEntity>().Single();
            if (orbCase)
            {
                _inventory.GiveTreasure(TreasureId.Sword, 1); _inventory.EquipA(TreasureId.Sword);
                var orb = _entities.Entities<DungeonOrbRoomEntity>().Single();
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
                for (int update = 0; update < 12 && !orb.PendingHit; update++) StepGameplayUpdates(1, Vector2.Zero, ["attack"], [], batched);
                FailIf(!orb.PendingHit || orb.IsOn, "The real Sword must publish the orb's pending hit before the next PART pass.");
            }
            else
            {
                while (_player.Position.Y > 33) StepGameplayUpdates(1, Vector2.Up, ["move_up"], [], batched);
                FailIf(_entities.ActiveTriggers != 0, "The button approach must stop before contact to stage capacity.");
            }
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 4);
            var seed = _random.CaptureState();
            var native = new SomariaRom(_saveData, seed, _currentRoom, 0, (int)_player.Position.X, (int)_player.Position.Y);
            native[0xcc8a] = 4; native[0xd240] = 1; native[0xd241] = 0x21; native[0xd242] = (byte)(orbCase ? 0x0a : 0x0c); native[0xd244] = 1;
            foreach (int slot in F8Enemies.Values) { native[0xd080 + slot * 256] = 1; native[0xd084 + slot * 256] = 8; }
            native[0xcec1] = native[0xcec9] = native[0xceca] = 0; native[0xced0] = 0xaa;
            F8Fill(native, free);
            if (orbCase) StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            else for (int update = 0; update < 8 && _runtimeState.ReadWramByte(OracleRuntimeState.ArmosTriggerAddress) == 0; update++)
                StepGameplayUpdates(1, Vector2.Up, ["move_up"], [], batched);
            if (orbCase) native[0xcdd2] = 0x10; else native[0xcca0] = 1;
            native.AdvanceEnemies(_entities.FrameCounter); native.AdvanceInteractions(_entities.FrameCounter);
            int SourceCount() => Enumerable.Range(0, 16).Count(slot => native[0xd080 + slot * 256] != 0 && native[0xd081 + slot * 256] == 0x1d);
            FailIf(!parent.Finished || _entities.Entities<ArmosSpawnerRoomEntity>().Count != free || SourceCount() != free ||
                _entities.Entities<ArmosSpawnerRoomEntity>().Any(scanner => scanner.State != 0) ||
                _runtimeState.ReadWramByte(OracleRuntimeState.ArmosTriggerAddress) != 1 || _random.Calls != seed.Calls,
                "Nested object opcode7 must skip a full ENEMY pool without initialization RNG, preserving the controller's trigger/deletion continuation.");
            StepGameplayUpdates(1, Vector2.Zero, batched: batched); native.AdvanceEnemies(_entities.FrameCounter); native.AdvanceInteractions(_entities.FrameCounter);
            FailIf(_entities.Entities<ArmosSpawnerRoomEntity>().Any(scanner => scanner.State != 1) || _random.Calls - seed.Calls != native.RandomCalls,
                "An admitted Moonlit ENEMY$1d must wait for the next ENEMY phase before state0 RNG.");
            if (free == 0)
            {
                _entities.RetireEntity(F8Enemies.First(pair => pair.Key is EnemyAiSlotReservation).Key);
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                FailIf(_entities.Entities<ArmosSpawnerRoomEntity>().Count != 0, "Released capacity must not replay the consumed nested object list.");
            }
            _entities.Clear();
        }
        ReinitializeGameplayForValidation();
        GD.Print("Compared reachable Moonlit orb/button opcode7 ENEMY admission and next-pass RNG against the original controller/parser.");
    }
}
