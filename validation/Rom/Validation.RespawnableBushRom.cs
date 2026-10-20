using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePlacedRespawnableBushRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int target in new[] { 0x91, 0x9d })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetRoomFlag(5, 0x78, 0xa0);
            LoadValidationRoom(5, 0x78);
            _inventory.GiveTreasure(TreasureId.Sword, 1);
            _inventory.SetScriptedEquippedItems(TreasureId.Sword, 0);
            var bushes = _entities.Entities<RespawnableBushRoomEntity>();
            FailIf(bushes.Select(bush => (bush.PackedPosition, bush.DropSubId)).ToArray() is not
                [(0x91, 0x04), (0x9d, 0x06)],
                "objects/ages/mainData.s: room$5:$78 orders3/4 require PART$0f:$04 at$91 and PART$0f:$06 at$9d.");
            var slots = SomariaPrivate<Dictionary<IRoomEntity, int>>(_entities, "_partSlots");
            FailIf(bushes.Where((bush, index) => !slots.TryGetValue(bush, out int slot) || slot != index).Any(),
                "Room$5:$78 bushes must occupy PART slots$00/$01 before boss-created parts.");
            // Keep both original bush placements; isolate their collision/drop
            // lifecycle from shutters and the already defeated boss's reward.
            var active = SomariaPrivate<List<IRoomEntity>>(_entities, "_activeEntities");
            var free = typeof(RoomEntityManager).GetMethod("FreeEntity", BindingFlags.Instance | BindingFlags.NonPublic)!;
            foreach (var entity in active.Where(entity => entity is not RespawnableBushRoomEntity).ToArray())
            { active.Remove(entity); free.Invoke(_entities, [entity]); }
            var bush = bushes.Single(candidate => candidate.PackedPosition == target);
            Vector2 start = bush.Position + Vector2.Up * 32;
            _player.WarpTo(start); _player.Face(Vector2I.Down);
            FailIf(_collision.Collides(_player.Position), $"PART$0f at${target:x2}: approach must start on original room$5:$78 floor.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 2, (int)start.X, (int)start.Y)
                { HostilePartsEnabled = true };
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcc39] = 8;
            for (int index = 0; index < 2; index++)
            {
                int address = 0xd0c0 + index * 256;
                rom[address] = 1; rom[address + 1] = 0x0f; rom[address + 2] = (byte)bushes[index].DropSubId;
                rom[address + 11] = (byte)bushes[index].Position.Y;
                rom[address + 13] = (byte)bushes[index].Position.X;
            }
            int update = 0, previous = 0;
            void Step(int count = 1, int held = 0, int angle = 0xff)
            {
                int pressed = held & ~previous; previous = held;
                StepGameplayUpdates(count, angle == 16 ? Vector2.Down : Vector2.Zero,
                    (held & 2) != 0 ? ["item"] : [], (pressed & 2) != 0 ? ["item"] : [], batched,
                    afterUpdate: () =>
                    {
                        rom.UpdateGameplay(pressed, held, angle, _entities.FrameCounter); pressed = 0; update++;
                        rom.AdvanceTileGraphics();
                        string context = $"Room$5:$78 bush${target:x2}, batch={batched}, update{update}";
                        FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f),
                            $"{context}: Link runtime={_player.PrecisePosition}, ROM={rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}.");
                        foreach (var candidate in bushes)
                        {
                            int address = 0xd0c0 + slots[candidate] * 256;
                            FailIf((int)candidate.State + 1 != rom[address + 4] || candidate.Counter != rom[address + 6] ||
                                candidate.CollisionEnabled != ((rom[address + 0x24] & 0x80) != 0) ||
                                _currentRoom.GetPackedStorageMetatile((byte)candidate.PackedPosition) != rom[0xcf00 + candidate.PackedPosition] ||
                                _currentRoom.GetTerrainInfo(candidate.Position).Collision != rom[0xce00 + candidate.PackedPosition],
                                $"{context}: PART$0f at${candidate.PackedPosition:x2} runtime={candidate.State}/{candidate.Counter}/{candidate.CollisionEnabled}, " +
                                $"ROM=${rom[address+4]:x2}/${rom[address+6]:x2}/${rom[address+0x24]:x2}.");
                        }
                        var rng = _random.CaptureState();
                        FailIf(rng.Rng1 != rom[0xff94] || rng.Rng2 != rom[0xff95] || rng.Calls - seed.Calls != rom.RandomCalls,
                            $"{context}: shared RNG differs ({rng.Calls-seed.Calls}/{rom.RandomCalls}).");
                        int nativeDrops = Enumerable.Range(0, 16).Count(slot => rom[0xd0c0 + slot * 256] != 0 && rom[0xd0c1 + slot * 256] == 0x05);
                        FailIf(_entities.Entities<ItemDropEffect>().Count != nativeDrops,
                            context + ": PART$05 drop allocation/collection differs.");
                    });
            }
            // State zero initializes under the native PART disable mask;
            // subsequent updates freeze until that category resumes.
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 8); rom[0xcc8a] = 8;
            Step(2);
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0); rom[0xcc8a] = 0;
            Step(16, held: 0x80, angle: 16);
            FailIf(_collision.Collides(_player.Position) || _player.Position != bush.Position + Vector2.Up * 16,
                "Room$5:$78 sword approach must walk through the source floor and stop outside the solid bush.");
            for (int cut = 0; cut < 2; cut++)
            {
                Step(1, held: 2);
                int bushAddress = 0xd0c0 + slots[bush] * 256;
                for (int wait = 0; (rom[bushAddress + 0x2a] & 0x80) == 0 && wait < 24; wait++) Step();
                FailIf((rom[bushAddress + 0x2a] & 0x80) == 0 || bush.State != RespawnableBushState.Ready,
                    "PART$0f must retain its tile until the eligible dispatch after sword collision publication.");
                if (cut == 0)
                {
                    _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 8); rom[0xcc8a] = 8;
                    Step(12);
                    FailIf(bush.State != RespawnableBushState.Ready,
                        "A published sword collision must survive the PART mask without cutting or consuming drop RNG.");
                    _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0); rom[0xcc8a] = 0;
                }
                Step();
                FailIf(bush.State != RespawnableBushState.CutDelay || _currentRoom.GetMetatile(bush.Position) != 0x02,
                    $"PART$0f:${bush.DropSubId:x2} must cut tile$04 to$02 on its next eligible update.");
                for (int wait = 0; bush.State != RespawnableBushState.Ready && wait < 502; wait++) Step();
                FailIf(bush.State != RespawnableBushState.Ready || _currentRoom.GetMetatile(bush.Position) != 0x04,
                    "PART$0f must complete its half-rate $f0 delay and $0c/$08 regeneration before another cut.");
                Step(20);
            }
            LoadValidationRoom(5, 0x78);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(_entities.Entities<RespawnableBushRoomEntity>().Count != 2 ||
                _currentRoom.GetPackedStorageMetatile(0x91) != 0x04 || _currentRoom.GetPackedStorageMetatile(0x9d) != 0x04,
                "Room$5:$78 re-entry must restore both placed regenerating bushes.");
        }
    }
}
