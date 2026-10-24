using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoomLoadTileEventsRom()
    {
        // Execute native layout decoding and the complete substitution caller;
        // neither reference inputs nor expectations come from the new rules.
        void Compare(int group, int id)
        {
            OracleRoomData actual = _rooms.GetRoom(group, id);
            var native = new FrontendRom();
            for (int address = 0xc5b0; address < 0xcb00; address++)
                native[address] = _saveData.ReadWramByte(address);
            native[0xcc01] = _saveData.ReadWramByte(0xc612);
            native[0xcc2d] = (byte)group;
            native[0xcc30] = native[0xcc2f] = (byte)id;
            native[0xcc2e] = (byte)(actual.WidthInTiles == 15 ? 1 : 0);
            native[0xcc05] = 0xff; native[0xcd00] = 1;
            byte switches = _runtimeState.ReadWramByte(0xcdd3);
            native[0xcdd3] = switches;
            native.LoadRoomTileset();
            native.Call(0x3712, 0); // loadTilesetLayout includes w3TileCollisions for the withered-vine gate.
            native.Call(0x38dc, 0); // bank0.loadRoomLayout
            native.ApplyRoomTileSubstitutions();
            for (int y = 0; y < actual.HeightInTiles; y++)
            for (int x = 0; x < actual.WidthInTiles; x++)
            {
                int packed = y * 16 + x;
                byte tile = actual.GetMetatile(new(x * 16 + 8, y * 16 + 8));
                FailIf(tile != native[0xcf00 + packed],
                    $"Room-load {group:x1}:{id:x2}, switch=${switches:x2}, vine=${native[0xc8f0]:x2}, " +
                    $"linked=${native[0xcc01]:x2}, progress flags=${native[0xc790]:x2}/${native[0xc7ba]:x2}, " +
                    $"tile=${packed:x2}: runtime=${tile:x2}, ROM=${native[0xcf00 + packed]:x2}.");
            }
            FailIf(native[0xcdd3] != switches || native[0xc8f0] != _saveData.ReadWramByte(0xc8f0) ||
                native.RandomCalls != 0 || native.Sounds.Count != 0,
                $"Room-load {group:x1}:{id:x2} must preserve switch/sprout state without RNG or cues.");
        }

        // Both polarities, independent bits, unrelated high bits, and cached
        // return to the initial branch. Include the already-supported TILE-03.
        foreach (byte switches in new byte[] { 0, 1, 2, 3, 0x80, 0x81, 0x82, 0x83, 0 })
        {
            _runtimeState.SetWramByte(0xcdd3, switches);
            foreach (int id in new[] { 0x4c, 0x4e, 0xc7 }) Compare(4, id);
        }

        // Check the actual loader -> bridge state-zero handoff. A stale
        // layout would initialize the complementary bridge controllers wrong.
        foreach (bool batched in new[] { false, true })
        foreach (byte switches in new byte[] { 0, 3, 0x80 })
        foreach (int id in new[] { 0x4c, 0x4e })
        {
            _runtimeState.SetWramByte(0xcdd3, switches);
            LoadValidationRoom(4, id);
            var bridges = _entities.Entities<ExtendableBridgeRoomEntity>().ToArray();
            FailIf(bridges.Length != (id == 0x4c ? 2 : 3),
                $"Room 4:{id:x2} lost its source-placed INTERAC_EXTENDABLE_BRIDGE $23 controllers.");
            var sounds = _sound.AttachPlayRequestAudit();
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            foreach (var bridge in bridges)
            {
                bool expected = id == 0x4c
                    ? bridge.PackedPosition == 0x43 ? (switches & 1) == 0 : (switches & 1) != 0
                    : bridge.PackedPosition == 0x39 ? (switches & 2) != 0 : (switches & 2) == 0;
                FailIf(bridge.BridgePresent != expected || bridge.UpdatingTiles,
                    $"Room 4:{id:x2}, switch=${switches:x2}: bridge at ${bridge.PackedPosition:x2} " +
                    $"must initialize present={expected} without replaying a toggle (batch={batched}).");
            }
            FailIf(sounds.Requests.Contains(SoundId.SndDoorClose),
                $"Room 4:{id:x2} replayed its bridge sound on initialization.");
        }

        foreach (byte seed in new byte[] { 0x41, 0x17, 0x27, 0x00, 0x17 })
        {
            _saveData.WriteWramByte(0xc8f0, seed);
            Compare(0, 0x2c); Compare(0, 0x1c);
        }

        // Stage 2 wins even when stage 1 is also set. Any nonzero linked
        // byte is accepted by checkIsLinkedGame, independently of finished-game.
        foreach (byte linked in new byte[] { 0, 1, 0x80 })
        foreach (int flags in new[] { 0, 1, 2, 3, 0 })
        {
            _saveData.WriteWramByte(0xc612, linked);
            _saveData.SetRoomFlag(0, 0xba, 0x40, (flags & 1) != 0);
            _saveData.SetRoomFlag(0, 0x90, 0x40, (flags & 2) != 0);
            Compare(0, 0x76);
            OracleRoomData room = _rooms.GetRoom(0, 0x76);
            foreach (int packed in new[] { 0x54, 0x55 })
            {
                Vector2 point = new((packed & 15) * 16 + 8, (packed >> 4) * 16 + 8);
                byte expected = linked != 0 && flags == 1 ? (byte)0xa7 : room.GetOriginalMetatile(point);
                FailIf(room.GetMetatile(point) != expected,
                    $"Black Tower 0:76/${packed:x2} requires linked stage 1 for tile $a7.");
            }
        }
        GD.Print("Validated clean-US room-load TILE-01..06 layouts, bit gates, linked progress precedence, " +
            "cache restoration and actual D3 bridge initialization through split/batched updates.");
    }

    private void ValidateRollingRidgeVines()
    {
        static Vector2 Point(int packed) => new((packed & 15) * 16 + 8, (packed >> 4) * 16 + 8);
        void CheckLayout(int id, byte seed)
        {
            OracleRoomData room = _rooms.GetRoom(0, id);
            // Independent roomSpecificTileChanges.s $19/$1a edge writes,
            // global vine replacements and the tile below the lowest $d6.
            Dictionary<int, byte> writes = seed == 0x17
                ? id == 0x2c ? new() { [0x06] = 0x5d, [0x07] = 0xd6, [0x08] = 0x60, [0x17] = 0x8d }
                    : new() { [0x66] = 0x5b, [0x67] = 0xd4, [0x68] = 0x45,
                        [0x76] = 0x4d, [0x77] = 0xd5, [0x78] = 0x55 }
                : id == 0x2c && seed is 0x27 or 0x41 ? new() { [seed] = 0x8c } : new();
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
            {
                int packed = y * 16 + x;
                byte expected = writes.GetValueOrDefault(packed, room.GetOriginalMetatile(Point(packed)));
                FailIf(room.GetMetatile(Point(packed)) != expected,
                    $"Rolling Ridge 0:{id:x2}, sprout=${seed:x2}: tile ${packed:x2} expected ${expected:x2}, " +
                    $"got ${room.GetMetatile(Point(packed)):x2}.");
                if (expected is 0xd4 or 0xd5 or 0xd6)
                    FailIf(room.GetTerrainInfo(Point(packed)).Type != TerrainType.Vines,
                        $"Rolling Ridge 0:{id:x2}/${packed:x2} must be climbable.");
            }
            FailIf(_saveData.ReadWramByte(0xc8f0) != seed,
                "Rolling Ridge growth must not rewrite wVinePositions+$00.");
        }

        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            void Step(int count, Vector2 move = default) => StepGameplayUpdates(count, move,
                move == Vector2.Up ? ["move_up"] : [], batched: batched);
            foreach (byte seed in new byte[] { 0x41, 0x17, 0x27, 0x00, 0x17 })
            { _saveData.WriteWramByte(0xc8f0, seed); CheckLayout(0x2c, seed); CheckLayout(0x1c, seed); }

            // Exercise the original $62:$00 producer, through actual terrain
            // contact, cancellation, completion and a repeat after re-entry.
            _saveData.WriteWramByte(0xc8f0, 0x27);
            LoadValidationRoom(1, 0x2c);
            var sprout = _entities.Entities<VineSproutRoomEntity>().Single();
            F1Retain(sprout);
            _player.WarpTo(Point(0x37));
            FailIf(_collision.Collides(_player.Position), "Rolling Ridge sprout approach must begin on real clear floor.");
            Step(1); Step(10, Vector2.Up);
            FailIf(sprout.Moving || sprout.PushCounter == 20, "Rolling Ridge approach must start an incomplete push.");
            Step(1);
            FailIf(sprout.Moving || sprout.PushCounter != 20 || sprout.PersistedPosition != 0x27,
                "Rolling Ridge cancelled push must preserve position $27.");
            for (int wait = 0; !sprout.Moving && wait < 40; wait++) Step(1, Vector2.Up);
            FailIf(!sprout.Moving || sprout.PersistedPosition != 0x27, "Rolling Ridge push must start before persisting its destination.");
            Step(sprout.MoveCounter - 1);
            FailIf(!sprout.Moving || sprout.PersistedPosition != 0x27, "Rolling Ridge sprout persisted before its final movement update.");
            Step(1);
            FailIf(sprout.Moving || sprout.PersistedPosition != 0x17, "Rolling Ridge sprout must complete at $17.");
            LoadValidationRoom(1, 0x2c); Step(1);
            FailIf(_entities.Entities<VineSproutRoomEntity>().Single().Position != Point(0x17),
                "Rolling Ridge sprout must retain $17 on re-entry.");
            _player.WarpTo(Point(0x27));
            _transitions.ApplyHarpTimeWarp(_player, _player.Position);
            for (int wait = 0; IsTransitioning && wait < 1000; wait += 4) Step(4);
            FailIf(IsTransitioning || _activeGroup != 0 || _currentRoom.Id != 0x2c,
                "Rolling Ridge sprout timewarp must arrive in present 0:2c.");
            for (int visit = 0; visit < 2; visit++)
            {
                if (visit != 0) { LoadValidationRoom(0, 0x2c); _player.WarpTo(Point(0x27)); }
                CheckLayout(0x2c, 0x17);
                Step(160, Vector2.Up);
                FailIf(IsTransitioning || _activeGroup != 0 || _currentRoom.Id != 0x1c || _player.Position.Y >= 120,
                    $"Rolling Ridge repeated vine climb failed (visit={visit}, batch={batched}, room={_activeGroup:x1}:{_currentRoom.Id:x2}, Link={_player.Position}).");
                CheckLayout(0x1c, 0x17);
            }
        }
    }
}
