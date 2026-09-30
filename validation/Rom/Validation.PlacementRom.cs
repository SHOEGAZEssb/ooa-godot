using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private static void ValidatePlacementBufferRom()
    {
        var rom = new PlacementRom();
        var random = new OracleRandom();
        var memory = new OracleRuntimeState();
        random.BindPlacementMemory(memory);
        OracleRandomState initial = random.CaptureState();
        // Sweep each seed byte independently and together, including zero,
        // carry boundaries and all-ones. Repeat without reseeding to test lifetime.
        for (int value = 0; value < 256; value++)
        foreach (int seed in new[] { value, value << 8, value * 0x101, value | ((255 - value) << 8) })
        {
            rom[0xff94] = (byte)seed;
            rom[0xff95] = (byte)(seed >> 8);
            rom[0xcec0] = (byte)value;
            random.RestoreState(initial with { Rng1 = (byte)seed, Rng2 = (byte)(seed >> 8), PlacementIndex = (byte)value });
            for (int repeat = 0; repeat < 2; repeat++)
            {
                int beforeCalls = rom.RandomCalls;
                byte[] actual = random.GeneratePermutation();
                rom.Generate();
                OracleRandomState state = random.CaptureState();
                byte[] expected = rom.Buffer;
                int mismatch = Array.FindIndex(Enumerable.Range(0, 256).ToArray(), index => actual[index] != expected[index]);
                FailIf(mismatch >= 0 || !state.PlacementBuffer.AsSpan().SequenceEqual(expected) ||
                    state.Rng1 != rom[0xff94] || state.Rng2 != rom[0xff95] ||
                    state.PlacementIndex != rom[0xcec0] || !state.PlacementBufferReady ||
                    rom.RandomCalls - beforeCalls != 256 || state.Calls != (repeat + 1) * 256,
                    $"generateRandomBuffer $02:$7823 seed=${seed:x4}, repeat={repeat}, mismatch=${mismatch:x}: " +
                    $"ROM RNG=${rom[0xff95]:x2}{rom[0xff94]:x2}, runtime=${state.Rng2:x2}{state.Rng1:x2}, calls={state.Calls}.");
                // Two complete cursor cycles prove increment-before-read, wrap,
                // no extra RNG, and preservation of the cursor on regeneration.
                for (int read = 0; read < 512; read++)
                {
                    rom.NextBufferedValue();
                    byte next = random.NextPlacementValue();
                    FailIf(next != rom.Result || memory.ReadWramByte(0xcec0) != rom[0xcec0],
                        $"getNextValueFromRandomBuffer $02:$7959 seed=${seed:x4}, read={read} diverged.");
                }
                FailIf(random.Calls != state.Calls || rom.RandomCalls - beforeCalls != 256,
                    "Buffered placement reads consumed global RNG.");
            }
        }
        GD.Print("Validated 2048 executed clean-ROM buffer generations, all 256 bytes/RNG aftermath, cursor retention and 1048576 buffered reads.");
    }

    private void ValidateRoomPlacementRom()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var memory = new OracleRuntimeState();
        var random = new OracleRandom();
        using var fixture = RoomEntityValidationFixture.Attach(this, "RoomPlacementRom", new()
        { Random = random, RuntimeState = memory });
        var room = _world.LoadRoom(0, 0x33);
        var history = (RecentEnemyDefeats)typeof(RoomEntityManager)
            .GetField("_recentEnemyDefeats", flags)!.GetValue(fixture.Manager)!;
        var reserve = typeof(RoomEntityManager).GetMethod("RegisterEnemySlot", flags)!;
        var transition = typeof(RoomEntityManager).GetMethod("BeginScreenTransition", flags, null,
            [typeof(int), typeof(OracleRoomData), typeof(Vector2), typeof(EnemyPlacementContext), typeof(Player)], null)!;
        byte[] tiles = new byte[80], collisions = new byte[80];
        for (int index = 0; index < 80; index++)
        {
            Vector2 point = new(index % 10 * 16 + 8, index / 10 * 16 + 8);
            tiles[index] = room.GetMetatile(point);
            collisions[index] = (byte)room.GetTerrainInfo(point).Collision;
        }
        EnemyPlacementContext[] contexts =
        [
            EnemyPlacementContext.Scrolling(Vector2I.Up),
            EnemyPlacementContext.Scrolling(Vector2I.Right),
            EnemyPlacementContext.Scrolling(Vector2I.Down),
            EnemyPlacementContext.Scrolling(Vector2I.Left),
            EnemyPlacementContext.Warp(0x23),
            EnemyPlacementContext.Warp(0x45),
            EnemyPlacementContext.FromWarpDestination(0xf0)
        ];
        int cases = 0;
        foreach (int seed in new[] { 0, 0x0d37, 0x80ff, 0xffff })
        for (int contextIndex = 0; contextIndex < contexts.Length; contextIndex++)
        for (int terrain = 0; terrain < 3; terrain++)
        foreach (int initialKilled in new[] { 0, 2, 0x0e })
        foreach (int occupied in new[] { 0, 0xfffb, 0xffff })
        {
            var rom = new PlacementRom();
            rom.AssertRoom33Stream();
            history.Clear();
            history.BeginRoom(0x33);
            rom[0xcdc0] = 0x33;
            rom[0xcdd0] = 2;
            random.RestoreState(random.CaptureState() with
            { Rng1 = (byte)seed, Rng2 = (byte)(seed >> 8), Calls = 0 });
            rom[0xff94] = (byte)seed;
            rom[0xff95] = (byte)(seed >> 8);
            int preloadCalls = 0;
            for (int repeat = 0; repeat < 2; repeat++)
            {
                // Re-entry keeps RNG history and newly defeated index 2. It
                // must regenerate the permutation even if no enemy can spawn.
                int killed = initialKilled | (repeat == 0 ? 0 : 4);
                for (int index = 1; index <= 3; index++)
                    if ((killed & (1 << index)) != 0) history.MarkKilled(index);
                rom[0xcdc1] = (byte)killed;
                rom[0xcc05] = 2; // checkSkipPointer admits enemy pointers.
                rom[0xcc2d] = 0;
                rom[0xcc30] = 0x33;
                rom[0xcc33] = (byte)room.ActiveCollisions;
                rom[0xcd00] = (byte)(contextIndex < 4 ? 8 : 0);
                rom[0xcd02] = (byte)(contextIndex < 4 ? contextIndex : 3);
                rom[0xcc4a] = (byte)(contextIndex == 4 ? 0x23 : contextIndex == 5 ? 0x45 : 0xf0);
                for (int index = 0; index < 80; index++)
                {
                    int packed = index / 10 * 16 + index % 10;
                    Vector2 point = new(index % 10 * 16 + 8, index / 10 * 16 + 8);
                    byte tile = terrain == 0 ? tiles[index] : terrain == 1 ? (byte)0x2c : (byte)0xf3;
                    byte collision = terrain == 0 ? collisions[index] : terrain == 1 ? (byte)0x0f : (byte)0;
                    room.SetPositionTileAndCollision(point, tile, collision, 0);
                    rom[0xcf00 + packed] = tile;
                    rom[0xce00 + packed] = collision;
                }
                // Dirty scratch proves full parse clearing, not just a clean
                // fixture that happens to have the correct zero values.
                for (int address = 0xcec0; address < 0xcee0; address++)
                {
                    rom[address] = 0xa5;
                    memory.SetWramByte(address, 0xa5);
                }
                rom[0xcfc0] = 0xa5;
                memory.SetWramByte(0xcfc0, 0xa5);
                fixture.Manager.Clear();
                for (int slot = 0; slot < 16; slot++)
                {
                    int address = 0xd080 + slot * 0x100;
                    for (int offset = 0; offset < 0x40; offset++) rom[address + offset] = 0;
                    if ((occupied & (1 << slot)) == 0) continue;
                    rom[address] = 2; // outgoing allocation survives the parse
                    reserve.Invoke(fixture.Manager, [null, slot]);
                }
                rom.ParseRoom();
                if (occupied != 0)
                {
                    // The manager's transition entry includes the first frozen
                    // enemy pass after parsing. Execute native state zero for
                    // incoming slots; reserved outgoing slots have no handler.
                    var ai = new EnemyAiRom();
                    ai[0xff94] = rom[0xff94];
                    ai[0xff95] = rom[0xff95];
                    ai[0xcd00] = 8;
                    for (int slot = 0; slot < 16; slot++)
                        if ((occupied & (1 << slot)) == 0)
                            for (int offset = 0; offset < 0x40; offset++)
                                ai[0xd080 + slot * 0x100 + offset] = rom[0xd080 + slot * 0x100 + offset];
                    ai.Update();
                    preloadCalls += ai.RandomCalls;
                    rom[0xff94] = ai[0xff94];
                    rom[0xff95] = ai[0xff95];
                }
                if (occupied == 0)
                    fixture.Manager.LoadRoom(0, room, contexts[contextIndex]);
                else
                {
                    // Exercise the real destination-parse path, which retains
                    // outgoing slots. A direct LoadRoom would clear the pool.
                    transition.Invoke(fixture.Manager, [0, room, Vector2.Zero, contexts[contextIndex], null]);
                }
                string context = $"room $0:$33 seed=${seed:x4}, entry={contextIndex}, terrain={terrain}, killed=${killed:x2}, occupied=${occupied:x4}, repeat={repeat}";
                OracleRandomState after = random.CaptureState();
                FailIf(after.Rng1 != rom[0xff94] || after.Rng2 != rom[0xff95] ||
                    !after.PlacementBuffer.AsSpan().SequenceEqual(rom.Buffer) ||
                    after.Calls != rom.RandomCalls + preloadCalls || rom.RandomCalls != 256 * (repeat + 1),
                    $"ROM placement RNG/buffer mismatch: {context}.");
                for (int address = 0xcec0; address < 0xcee0; address++)
                    FailIf(memory.ReadWramByte(address) != rom[address],
                        $"Placement scratch ${address:x4}: ROM=${rom[address]:x2}, runtime=${memory.ReadWramByte(address):x2}; {context}.");
                FailIf(memory.ReadWramByte(0xcfc0) != rom[0xcfc0],
                    $"parseObjectData failed to clear $cfc0: {context}.");
                var slots = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager)
                    .GetField("_enemySlots", flags)!.GetValue(fixture.Manager)!;
                int expectedCount = 0;
                for (int slot = 0; slot < 16; slot++)
                {
                    int address = 0xd080 + slot * 0x100;
                    if ((occupied & (1 << slot)) != 0)
                    {
                        FailIf(rom[address] != 2 || slots.ContainsValue(slot),
                            $"Outgoing slot ${slot:x2} overwritten: {context}.");
                        continue;
                    }
                    IRoomEntity? entity = slots.FirstOrDefault(pair => pair.Value == slot).Key;
                    if (rom[address] == 0)
                    {
                        FailIf(entity is not null, $"Unexpected allocation at slot ${slot:x2}: {context}.");
                        continue;
                    }
                    expectedCount++;
                    FailIf(entity is null, $"Missing allocation at slot ${slot:x2}: {context}.");
                    ImportedEnemyDefinition record = entity.Node switch
                    {
                        RiverZoraCharacter zora => zora.Record,
                        BuzzBlobCharacter blob => blob.Record,
                        _ => throw new InvalidOperationException($"Unexpected room $0:$33 enemy {entity.Node.GetType().Name}.")
                    };
                    int index = (int)entity.GetType().GetProperty("KillableEnemyIndex", flags)!.GetValue(entity)!;
                    FailIf(record.Id != rom[address + 1] || record.SubId != rom[address + 2] ||
                        ((index << 4) | 1) != rom[address] ||
                        entity.Node.Position != new Vector2(rom[address + 0x0d], rom[address + 0x0b]),
                        $"Slot ${slot:x2}: ROM enabled/id/subid/YX=${rom[address]:x2}/${rom[address + 1]:x2}/${rom[address + 2]:x2}/${rom[address + 0x0b]:x2}/${rom[address + 0x0d]:x2}, " +
                        $"runtime index={index}, id/subid=${record.Id:x2}/${record.SubId:x2}, position={entity.Node.Position}; {context}.");
                }
                FailIf(slots.Count != expectedCount || fixture.Manager.RoomEnemyCount != rom[0xcdd1],
                    $"Room enemy count: ROM={rom[0xcdd1]}, runtime={fixture.Manager.RoomEnemyCount}; {context}.");
                if (terrain == 0 && occupied == 0 && killed == 0)
                    FailIf(expectedCount != 3 || rom[0xd081] != 0x08 || rom[0xd181] != 0x08 || rom[0xd281] != 0x18,
                        $"Source group0Map33EnemyObjectData lost its ordered two River Zoras and Buzz Blob: {context}.");
                if (occupied != 0) fixture.Manager.FinishScreenTransition();
                cases++;
            }
        }
        GD.Print($"Validated {cases} clean-ROM room $0:$33 parses: original object stream, slots/IDs/kill indices/positions, full scratch, RNG, all entry directions, warp exclusion, re-entry, terrain rejection and capacity failure.");
    }
}
