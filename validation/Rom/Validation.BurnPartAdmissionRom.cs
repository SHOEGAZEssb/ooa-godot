using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateBurnPartAdmissionRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (byte mask in new byte[] { 0, 4 })
        foreach (int free in new[] { 0, 1, 2 })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4, 0xa1); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0); _player.WarpTo(new(24, 24));
            // Declare a collision-safe floor to isolate two simultaneous seed
            // contacts, not natural retail-room saturation or enemy placement.
            for (int y = 8; y < 176; y += 16)
            for (int x = 8; x < 240; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), 0x0c, 0, 0);
            var database = EnemyDatabase.Shared;
            var record = database.GetRoomObjects(1, 0x5b).Single(row => row.Id == 0x0d);
            var actors = new List<LynelCharacter>();
            for (int slot = 0; slot < 2; slot++)
            {
                var actor = new LynelCharacter();
                actor.Initialize(database.ImportedEnemy(0x0d), _currentRoom, new(80 + 80 * slot, 80), _random);
                var adapter = new LynelRoomEntity(actor, database.EnemyHandlers.ResolveHandler(record).CombatSource(record, slot + 1), _entities);
                _entities.RegisterEnemySlot(adapter, slot); _entities.AddEntity(adapter); actors.Add(actor);
            }
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 24, 24)
                { HostileEnemiesEnabled = true, HostilePartsEnabled = true };
            native.InitializeLinkGameplay(); native.InitializeLinkWalkingAnimation();
            for (int slot = 0; slot < 2; slot++)
            {
                int at = 0xd080 + slot * 256;
                native[at] = (byte)(0x11 + 16 * slot); native[at + 1] = 0x0d;
                native.Word(at + 10, 80 * 256); native.Word(at + 12, (80 + 80 * slot) * 256);
            }
            StepGameplayUpdates(1, Vector2.Zero);
            native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
            FailIf(actors.Any(actor => !actor.CollisionEnabled || actor.Health != 8) || native[0xd0a9] != 8 || native[0xd1a9] != 8,
                "Burn admission requires two initialized live red Lynels.");
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects, mask); native[0xcc8a] = mask;
            for (int index = 0; index < 16 - free; index++)
            {
                _entities.Spawn<BlueEnergyBeadRoomEntity>(new BlueEnergyBeadSpawn(index % 8, new(120, 40), 0xff));
                int at = 0xd0c0 + index * 256;
                native[at] = 1; native[at + 1] = 0x53; native[at + 3] = (byte)(index % 8);
                native[at + 6] = 0xff; native[at + 0x30] = 40; native[at + 0x31] = 120;
            }
            FailIf(!new SeedSatchelDatabase().TryGet(ItemId.EmberSeed, out var seed), "Missing source Ember Seed record.");
            var seeds = new List<EmberSeedEffect>();
            for (int index = 0; index < 2; index++)
            {
                Vector2 origin = actors[index].Position - (index == 0 ? new Vector2(4, 1) : new Vector2(12, 5));
                seeds.Add(_entities.Spawn<EmberSeedEffect>(new EmberSeedSpawn(origin, Vector2I.Right, seed, 4,
                    index == 0 ? SeedLaunchKind.Satchel : SeedLaunchKind.Shooter, 2)));
                int at = 0xd700 + index * 256;
                native[at] = 1; native[at + 1] = 0x20; native[at + 2] = (byte)index;
                native[at + 8] = 1; native[at + 9] = 8;
                native.Word(at + 10, (int)(origin.Y * 256)); native.Word(at + 12, (int)(origin.X * 256));
            }
            int elapsed = 0;
            void Compare()
            {
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); elapsed++;
                var flames = _entities.Entities<BurningEnemyPart>();
                int nativeCount = Enumerable.Range(0, 16).Count(slot => native[0xd0c0 + slot * 256] != 0 && native[0xd0c1 + slot * 256] == 0x12);
                FailIf(flames.Count != nativeCount,
                    $"Competing burn admission differs: free={free}, mask=${mask:x2}, update={elapsed}, runtime={flames.Count}, ROM={nativeCount}.");
                for (int slot = 0; slot < 16; slot++)
                    if (_entities.ResolvePartSlot(slot)?.Node is BurningEnemyPart flame)
                        FailIf(native[0xd0c1 + slot * 256] != 0x12 ||
                            native[0xd0c4 + slot * 256] != 0 && flame.Counter != native[0xd0c6 + slot * 256],
                            $"Burn PART${slot:x2} lifetime differs at update={elapsed}: runtime={flame.Counter}, ROM={native[0xd0c6 + slot * 256]}, state={native[0xd0c4 + slot * 256]}.");
                for (int slot = 0; slot < 2; slot++)
                    FailIf(actors[slot].Health != native[0xd0a9 + slot * 256],
                        "A rejected burn request must preserve the original seed damage and the surviving target's health.");
                var rng = _random.CaptureState();
                FailIf(rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95], "Competing burn requests must retain shared RNG order.");
            }
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: Compare);
            Step(1);
            FailIf(_entities.Entities<BurningEnemyPart>().Count != System.Math.Min(free, 2) ||
                actors.Any(actor => actor.Health != 6 || !actor.NativeHitPending) || seeds.Any(effect => effect.CollisionEnabled) ||
                native[0xd724] >= 0x80 || native[0xd824] >= 0x80,
                "Full/last-slot burn failure must preserve both seed hits without creating an unpooled flame.");
            Step(58);
            FailIf(_entities.Entities<BurningEnemyPart>().Any(flame => flame.Counter != 1), "Burn lifetime must retain its final nonzero update.");
            Step(1); Step(2);
            FailIf(_entities.Entities<BurningEnemyPart>().Count != 0 || actors.Any(actor => actor.Health != 6),
                "Burn completion must restore source health and leave no frozen excess effect.");
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0);
            _entities.Clear();
            FailIf(Enumerable.Range(0, 16).Any(slot => _entities.ResolvePartSlot(slot) is not null), "Cancellation must release every PART slot.");
        }
        ReinitializeGameplayForValidation();
        GD.Print("Validated clean-US competing burn PART requests through completion, including the final free physical slot.");
    }
}
