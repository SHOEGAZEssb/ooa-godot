using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ComparePlacedPartBehaviorRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (var test in new[] { (Group: 3, Room: 0x4f), (Group: 5, Room: 0x01), (Group: 5, Room: 0xc9) })
        foreach (int freeSlots in test.Room == 0x01 ? new[] { -1, 0, 1 } : new[] { -1 })
        {
            ReinitializeGameplayForValidation();
            var seed = _random.CaptureState();
            LoadValidationRoom(test.Group, test.Room);
            // Keep the real placed PARTs while isolating their update/RNG
            // contract from unrelated room interactions and Link behavior.
            var parts = Enumerable.Range(0, 16).Select(_entities.ResolvePartSlot).Where(entity => entity is not null).ToArray();
            foreach (var entity in _entities.EntityAdapters<IRoomEntity>().Where(entity => !parts.Contains(entity)).ToArray())
                _entities.RetireEntity(entity);
            _player.WarpTo(new(8, 8)); _player.BeginCutsceneControl();
            _inventory.EquipA(0); _inventory.EquipB(0);
            var native = new SomariaRom(_saveData, seed, _currentRoom, 0, 8, 8);
            var frontend = SomariaPrivate<FrontendRom>(native, "_rom");
            native[0xcc05] = 2; native[0xcd00] = 1;
            frontend.Call(0x55b7, 0x12);
            native[0xd024] = 0; // No declared Link collision during roaming.
            if (freeSlots >= 0)
            {
                for (int index = 0; index < 14 - freeSlots; index++)
                {
                    _entities.TryCreatePart(new ZoraFireSpawn(new(56, 104)));
                    int address = 0xd2c0 + index * 0x100;
                    native[address] = 1; native[address + 1] = 0x19;
                    native[address + 11] = 104; native[address + 13] = 56;
                }
            }
            int updates = 0;
            void Compare()
            {
                updates++;
                native.AdvanceParts(_entities.FrameCounter);
                var rng = _random.CaptureState();
                FailIf(rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95],
                    $"Placed PART RNG differs in ${test.Group:x1}:${test.Room:x2}, update {updates}, batch={batched}.");
                for (int slot = 0; slot < 16; slot++)
                {
                    int address = 0xd0c0 + slot * 0x100;
                    var actor = _entities.ResolvePartSlot(slot);
                    FailIf((actor is null) != (native[address] == 0),
                        $"Placed PART slot${slot:x2} lifetime differs at update {updates}.");
                    if (actor is FallingFireRoomEntity fire)
                    {
                        FailIf(native[address + 1] != 0x23 || fire.SubId != native[address + 2] ||
                            fire.State != native[address + 4] || fire.Counter != native[address + 6] ||
                            fire.YFixed != native.Word(address + 10) || fire.SpeedYFixed != native.Word(address + 16) ||
                            fire.Visible != ((native[address + 26] & 0x80) != 0),
                            $"PART$23 slot${slot:x2} state/timer/fixed gravity/visibility differs at update {updates}.");
                        // Independent clean-US OAM pointer table for the
                        // three-frame fire loop, including invisible emitters.
                        int table = 0x5bc52 + fire.CurrentFrame * 2;
                        var rom = ValidationRom.LoadCleanUs().Span;
                        FailIf(native.Word(address + 30) != (rom[table] | rom[table + 1] << 8),
                            "PART$23 original OAM frame timing differs.");
                    }
                    else if (actor is SmallFairyRoomEntity fairy)
                    {
                        FailIf(native[address + 1] != 0x28 || fairy.State != native[address + 4] ||
                            fairy.Counter != native[address + 6] || fairy.Speed != native[address + 16] ||
                            fairy.Angle != native[address + 9] || fairy.CollisionZ != unchecked((sbyte)native[address + 15]) ||
                            fairy.PrecisePosition != new Vector2(native.Word(address + 12) / 256f, native.Word(address + 10) / 256f),
                            $"PART$28 slot${slot:x2} state/timer/angle/fixed XY/Z differs at update {updates}.");
                        // Clean-US part28OamDataPointers at ROM $5bcb2;
                        // both animations are held poses, differing in X flip.
                        int table = 0x5bcb2 + fairy.CurrentAnimation * 2;
                        var rom = ValidationRom.LoadCleanUs().Span;
                        FailIf(native.Word(address + 30) != (rom[table] | rom[table + 1] << 8),
                            "PART$28 source OAM pointer/held facing differs.");
                    }
                }
                for (int slot = 0; slot < 2; slot++)
                    FailIf(_runtimeState.ReadWramByte(0xd087 + slot * 0x100) != native[0xd087 + slot * 0x100],
                        "PART$23 must retain the source ENEMY.counter2 alias, not a private PART cycle.");
            }
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: Compare);
            var orphans = ValidationNodeLifetime.OrphanIds().ToHashSet();
            _dialogue.ShowGameplayMessage("Placed PART initialization gate", 100); native[0xcba0] = 1;
            Step(1); // State zero is admitted even under text.
            Step(2); // Initialized handlers now freeze without flight/emission.
            _dialogue.Close(); native[0xcba0] = 0;
            Step(1); // First flight selection or child allocation on release.
            if (freeSlots >= 0)
            {
                FailIf(_entities.Entities<FallingFireRoomEntity>().Count != 2 + freeSlots ||
                    ValidationNodeLifetime.OrphanIds().Any(id => !orphans.Contains(id)),
                    "PART$23 full/one-free admission must drop failed children before constructing a node.");
                _entities.RetireEntity(_entities.ResolvePartSlot(2)!); native[0xd2c0] = 0;
                Step(1);
                FailIf(_entities.Entities<FallingFireRoomEntity>().Count != 2 + freeSlots,
                    "PART$23 allocation failure consumes its interval; freeing capacity must not retry immediately.");
                continue;
            }
            _dialogue.ShowGameplayMessage("Placed PART gate", 100); native[0xcba0] = 1;
            Step(3);
            _dialogue.Close(); native[0xcba0] = 0;
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0x88);
            native[WramAddress.wDisabledObjects] = 0x88;
            Step(3);
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0);
            native[WramAddress.wDisabledObjects] = 0;
            Step(320); // Repeated emission/deletion and multiple bounded flight selections.
            LoadValidationRoom(test.Group, test.Room);
            for (int slot = 0; slot < 2; slot++)
                FailIf(_runtimeState.ReadWramByte(0xd087 + slot * 0x100) != 0,
                    "clearEnemies must clear the inactive counter2 alias on ordinary room re-entry.");
        }
    }
}
