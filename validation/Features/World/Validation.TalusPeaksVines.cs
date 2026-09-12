using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateTalusPeaksVines()
    {
        // roomSpecificTileChanges.s:$21/$22 dispatch to 0:61/0:51.
        // Both inspect wVinePositions+$01, not room flags or two sprouts.
        const int address = 0xc8f1;
        var enemies = new EnemyDatabase();
        FailIf(enemies.GetRoomObjects(1, 0x61) is not
            [{ Kind: RoomObjectKind.FixedEnemy, Id: 0x62, SubId: 0x01, Order: 0 }],
            "Room 1:61 lost its sole source-placed ENEMY_VINE_SPROUT $62:$01.");
        static Vector2 Point(int packed) => new((packed & 15) * 16 + 8, (packed >> 4) * 16 + 8);

        void CheckLayout(OracleRoomData room, int seed)
        {
            // Literal writes traced independently from @vine1/@vine2 and
            // @vines1/@vines2, including each edge and the unselected column.
            Dictionary<int, byte> writes = (room.Id, seed) switch
            {
                (0x61, 0x22) => new()
                {
                    [0x01] = 0x56, [0x02] = 0xd5, [0x03] = 0x4d,
                    [0x11] = 0x61, [0x12] = 0xd6, [0x13] = 0x5d, [0x22] = 0x8d
                },
                (0x61, 0x27) => new()
                {
                    [0x06] = 0x4d, [0x07] = 0xd5, [0x08] = 0x55,
                    [0x16] = 0x5d, [0x17] = 0xd6, [0x18] = 0x60, [0x27] = 0x8d
                },
                (0x51, 0x22) => new() { [0x71] = 0x46, [0x72] = 0xd4, [0x73] = 0x5c },
                (0x51, 0x27) => new() { [0x76] = 0x5b, [0x77] = 0xd4, [0x78] = 0x45 },
                (0x61, 0x32) => new() { [0x32] = 0x8c },
                _ => new()
            };
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
            {
                int packed = y * 16 + x;
                Vector2 point = Point(packed);
                byte expected = writes.GetValueOrDefault(packed, room.GetOriginalMetatile(point));
                FailIf(room.GetMetatile(point) != expected,
                    $"Room 0:{room.Id:x2}, sprout ${seed:x2}, tile ${packed:x2}: " +
                    $"expected ${expected:x2}, got ${room.GetMetatile(point):x2}.");
                if (expected is 0xd4 or 0xd5 or 0xd6)
                {
                    FailIf(room.GetTerrainInfo(point).Type != TerrainType.Vines,
                        $"Room 0:{room.Id:x2} tile ${packed:x2}/${expected:x2} is not climbable.");
                    using Image actualImage = room.BuildMimickedMetatileTexture(point).GetImage();
                    using Image expectedImage = room.BuildMimickedMetatileTexture(expected).GetImage();
                    FailIf(OracleGraphicsCache.PixelHash(actualImage) != OracleGraphicsCache.PixelHash(expectedImage),
                        $"Room 0:{room.Id:x2} tile ${packed:x2} did not render vine ${expected:x2}.");
                }
            }
            FailIf(_saveData.ReadWramByte(address) != seed,
                $"Room 0:{room.Id:x2} growth changed the saved sprout position ${seed:x2}.");
        }

        // fileManagement.initializeFile calls initializeVinePositions before
        // saving: all six defaults exist before any past sprout is parsed.
        byte[] defaults = [0x41, 0x22, 0x16, 0x35, 0x18, 0x53];
        OracleSaveData fresh = OracleSaveData.CreateStandardGame();
        for (int subId = 0; subId < defaults.Length; subId++)
            FailIf(fresh.ReadWramByte(0xc8f0 + subId) != defaults[subId],
                $"New file wVinePositions+${subId:x2} must start at ${defaults[subId]:x2} before visiting the past.");

        // Older port saves may have zero entries, including a mixture of
        // unvisited sprouts and positions already moved by the player.
        fresh.WriteWramByte(0xc8f0, 0);
        fresh.WriteWramByte(address, 0x27);
        fresh.WriteWramByte(0xc8f2, 0);
        byte[] legacy = fresh.Serialize();
        FailIf(!OracleSaveData.TryDeserialize(legacy, out OracleSaveData? restored),
            "Legacy vine-position save failed to load.");
        byte[] repaired = restored!.Serialize();
        for (int offset = 2; offset < legacy.Length; offset++)
        {
            byte expected = offset switch
            {
                0x340 => 0x41, // $c8f0 - $c5b0
                0x342 => 0x16,
                _ => legacy[offset]
            };
            FailIf(repaired[offset] != expected,
                $"Legacy vine repair changed save byte ${offset:x3} incorrectly.");
        }

        foreach (bool legacySave in new[] { false, true })
        foreach (bool batched in new[] { false, true })
        {
            OracleSaveData firstVisit = OracleSaveData.CreateStandardGame();
            if (legacySave)
            {
                firstVisit.WriteWramByte(address, 0);
                FailIf(!OracleSaveData.TryDeserialize(firstVisit.Serialize(), out restored),
                    "Legacy present-first save failed to load.");
                firstVisit = restored!;
            }
            _saveData.RestoreFrom(firstVisit);
            for (int visit = 0; visit < 2; visit++)
            {
                LoadValidationRoom(0, 0x61);
                _player.WarpTo(Point(0x32));
                CheckLayout(_currentRoom, 0x22);
                Input.ActionPress("move_up");
                try
                {
                    if (batched) base._Process(160 / 60.0);
                    else for (int update = 0; update < 160; update++) base._Process(1.0 / 60.0);
                }
                finally { Input.ActionRelease("move_up"); }
                FailIf(IsTransitioning || _activeGroup != 0 || _currentRoom.Id != 0x51 || _player.Position.Y >= 120,
                    $"Present-first vine climb failed on visit {visit + 1}, legacy={legacySave}, batched={batched} " +
                    $"(room {_activeGroup:x1}:{_currentRoom.Id:x2}, Link {_player.Position}).");
                CheckLayout(_currentRoom, 0x22);
                FailIf(_saveData.HasRoomFlag(1, 0x61, OracleSaveData.RoomFlagVisited),
                    "Present-first vine regression unexpectedly visited room 1:61.");
            }
        }

        // Reuse cached rooms across both branches, withered ground and a solid
        // mismatch ($01/$64): only collision-zero terrain receives tile $8c.
        foreach (byte seed in new byte[] { 0x22, 0x27, 0x32, 0x01, 0x22 })
        {
            _saveData.WriteWramByte(address, seed);
            foreach (int room in new[] { 0x61, 0x51 })
                CheckLayout(_rooms.GetRoom(0, room), seed);
        }

        foreach (byte target in new byte[] { 0x22, 0x27 })
        foreach (bool batched in new[] { false, true })
        {
            void Step(int updates)
            {
                if (batched) base._Process(updates / 60.0);
                else for (int update = 0; update < updates; update++) base._Process(1.0 / 60.0);
            }
            void WalkUp(int updates)
            {
                Input.ActionPress("move_up");
                try { Step(updates); }
                finally { Input.ActionRelease("move_up"); }
            }

            byte start = (byte)(target + 0x10);
            _saveData.WriteWramByte(address, start);
            LoadValidationRoom(1, 0x61);
            // Approach from real clear ground below the sprout, preserving all
            // source room geometry. No direct UpdatePushAttempt invocation.
            _player.WarpTo(Point(start + 0x10));
            Step(1);
            VineSproutRoomEntity sprout = _entities.Entities<VineSproutRoomEntity>().Single();
            FailIf(sprout.Position != Point(start) || sprout.PersistedPosition != start,
                $"Room 1:61 did not restore sprout $62:$01 at ${start:x2}.");
            WalkUp(10);
            FailIf(sprout.Moving || sprout.PushCounter == 20 || sprout.PersistedPosition != start,
                "Room 1:61 approach failed to begin an incomplete sprout push.");
            Step(1);
            FailIf(sprout.PushCounter != 20 || sprout.Moving,
                "Room 1:61 releasing direction did not cancel the incomplete push.");
            WalkUp(18);
            FailIf(sprout.Moving || sprout.PersistedPosition != start,
                "Room 1:61 sprout moved before the 20-update push completed.");
            WalkUp(2);
            FailIf(!sprout.Moving || sprout.PersistedPosition != start,
                "Room 1:61 completed push did not start movement with the old saved position.");
            int remaining = sprout.MoveCounter;
            Step(remaining - 1);
            FailIf(!sprout.Moving || sprout.PersistedPosition != start,
                "Room 1:61 sprout persisted before the last movement update.");
            Step(1);
            FailIf(sprout.Moving || sprout.Position != Point(target) || sprout.PersistedPosition != target,
                $"Room 1:61 sprout failed to settle and save position ${target:x2}.");
            Step(3);
            LoadValidationRoom(1, 0x61);
            _player.WarpTo(Point(start));
            Step(1);
            FailIf(_entities.Entities<VineSproutRoomEntity>().Single().Position != Point(target),
                $"Room 1:61 re-entry lost saved position ${target:x2}.");

            // Exercise destination load and room scrolling in the actual
            // gameplay scheduler, including several updates per host frame.
            _transitions.ApplyHarpTimeWarp(_player, _player.Position);
            for (int updates = 0; IsTransitioning && updates < 1000; updates += 4) Step(4);
            FailIf(IsTransitioning || _activeGroup != 0 || _currentRoom.Id != 0x61,
                $"Sprout ${target:x2} time travel failed to reach room 0:61.");
            CheckLayout(_currentRoom, target);
            WalkUp(160);
            FailIf(IsTransitioning || _activeGroup != 0 || _currentRoom.Id != 0x51 || _player.Position.Y >= 120,
                $"Sprout ${target:x2} vine did not carry Link from 0:61 into 0:51 " +
                $"(room {_activeGroup:x1}:{_currentRoom.Id:x2}, Link {_player.Position}, batched={batched}).");
            CheckLayout(_currentRoom, target);
            LoadValidationRoom(0, 0x61);
            _player.WarpTo(Point(start));
            CheckLayout(_currentRoom, target);
            WalkUp(160);
            FailIf(IsTransitioning || _currentRoom.Id != 0x51,
                $"Sprout ${target:x2} vine failed a repeated climb after room re-entry.");
        }
        GD.Print("Validated Talus Peaks $62:$01 push cancellation/completion, saved positions $22/$27, " +
            "new/legacy save defaults, present-first 0:61/0:51 climbing, vine tiles and collision, " +
            "misaligned/solid branches, time travel, repeated climbing, and batched updates.");
    }
}
