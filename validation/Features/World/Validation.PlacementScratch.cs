using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePlacementScratch()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var memory = new OracleRuntimeState();
        var random = new OracleRandom();
        using var fixture = RoomEntityValidationFixture.Attach(this, "PlacementScratch", new()
        { Random = random, RuntimeState = memory });
        var factory = typeof(RoomEntityManager).GetField("_factory", flags)!.GetValue(fixture.Manager)!;
        var choose = typeof(RoomEntityFactory).GetMethod("TryChooseRandomEnemyPosition", flags)!;
        var room = _world.LoadRoom(4, 0xa1);
        byte[] identity = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
        random.BeginRoomParse();
        var state = random.CaptureState() with { PlacementBuffer = identity, PlacementIndex = 0 };
        random.RestoreState(state);
        memory.SetWramByte(WramAddress.wTmpcec0, 0xff);
        FailIf(random.NextPlacementValue() != 0 || memory.ReadWramByte(WramAddress.wTmpcec0) != 0 ||
            random.CaptureState().PlacementIndex != 0,
            "Placement cursor must increment the shared byte with wrap before reading the permutation.");
        memory.SetWramByte(WramAddress.wTmpcec0, 0x42);
        FailIf(random.NextPlacementValue() != 0x43 || random.Calls != state.Calls,
            "An aliased cursor write must change the next buffered read without consuming RNG.");

        for (int repeat = 0; repeat < 2; repeat++)
        {
            EnemyPlacementReservations.BeginRoomParse(memory);
            random.RestoreState(state);
            var reservations = new EnemyPlacementReservations(memory);
            reservations.Add(0x11);
            object[] args = [room, 4, reservations, EnemyPlacementContext.Unrestricted, Vector2.Zero];
            bool found = (bool)choose.Invoke(factory, args)!;
            // Dungeon source rejects row0/column0; reserved$11 retries inside
            // getCandidatePositionForEnemy. First returned candidate is$12.
            FailIf(!found || (Vector2)args[4] != new Vector2(40, 24) ||
                memory.ReadWramByte(WramAddress.wTmpcec0) != 0x12 || memory.ReadWramByte(0xcec2) != 0x12 ||
                memory.ReadWramByte(0xcecf) != 0x3f || memory.ReadWramByte(0xced1) != 0x12,
                "Boundary/reservation retries must consume cursor bytes but only one placement attempt.");

            EnemyPlacementReservations.BeginRoomParse(memory);
            random.RestoreState(state);
            for (int y = 1; y <= 9; y++)
            for (int x = 1; x <= 13; x++)
                room.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0x2c, 0x0f, 0);
            args = [room, 0, reservations, EnemyPlacementContext.Unrestricted, Vector2.Zero];
            found = (bool)choose.Invoke(factory, args)!;
            // 63 returned candidates: four rows of13, then row5 column11.
            // Attempt64 decrements to zero without reading another candidate.
            FailIf(found || memory.ReadWramByte(0xcecf) != 0 ||
                memory.ReadWramByte(WramAddress.wTmpcec0) != 0x5b || memory.ReadWramByte(0xcec2) != 0x5b ||
                reservations.Count != 0 || random.Calls != state.Calls,
                "Exhaustion must preserve the63rd rejected candidate and cursor while clearing the attempt counter.");
        }

        // group4Mapa2EnemyObjectData has three random Keese with flags00,
        // followed by two uncounted item producers (objectDataOpA).
        // Exhaustion releases each enemy
        // slot but leaves each counted allocation in wNumEnemies (US bug).
        var blockedRoom = _world.LoadRoom(4, 0xa2);
        for (int y = 1; y <= 9; y++)
        for (int x = 1; x <= 13; x++)
            blockedRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0x2c, 0x0f, 0);
        for (int repeat = 0; repeat < 2; repeat++)
        {
            fixture.Manager.LoadRoom(4, blockedRoom);
            FailIf(fixture.Manager.Entities<KeeseCharacter>().Count != 0 ||
                fixture.Manager.RoomEnemyCount != 3,
                $"Failed random placement must retain three counts, restarting on reload; Keese={fixture.Manager.Entities<KeeseCharacter>().Count}, count={fixture.Manager.RoomEnemyCount}.");
            int freeSlot = (int)typeof(RoomEntityManager).GetMethod("FindFreeEnemySlot", flags)!
                .Invoke(fixture.Manager, null)!;
            FailIf(freeSlot != 2,
                "Failed random placement must release its slot for the following item producers.");
            typeof(RoomEntityManager).GetMethod("RetainFailedPlacementCount", flags)!
                .Invoke(fixture.Manager, [2]);
            FailIf(fixture.Manager.RoomEnemyCount != 3,
                "Uncounted flags02 must not retain a count after failed placement.");
        }

        fixture.Manager.BeginScreenTransition(4, _world.LoadRoom(4, 0x9b), Vector2.Zero);
        FailIf(fixture.Manager.RoomEnemyCount != 0,
            "Destination parse must clear failed-placement counts even while retaining outgoing enemy slots.");
        fixture.Manager.FinishScreenTransition();

        // 4:b2 keeps a fixed Beamos in slot0, then fails two subid01 Zols
        // in slot1. On the next parse Smasher's placed ball must overwrite
        // that first-free slot before its uncounted child uses slot2.
        var zolRoom = _world.LoadRoom(4, 0xb2);
        for (int y = 1; y <= 9; y++)
        for (int x = 1; x <= 13; x++)
            zolRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0x2c, 0x0f, 0);
        fixture.Manager.LoadRoom(4, zolRoom);
        FailIf(fixture.Manager.Entities<ZolCharacter>().Count != 0 || fixture.Manager.RoomEnemyCount != 3,
            "4:b2 exhaustion must leave the fixed Beamos and two failed counted Zols.");
        fixture.Manager.BeginScreenTransition(4, _world.LoadRoom(4, 0xb4), Vector2.Zero);
        var smashers = fixture.Manager.Entities<SmasherCharacter>().OrderBy(actor => actor.NativeSlot).ToArray();
        FailIf(smashers.Length != 2 || smashers[0].NativeSlot != 1 || smashers[0].NativeSubId != 0 ||
            smashers[1].NativeSlot != 2 || smashers[1].NativeSubId != 1,
            "The placed Smasher must consume the failed slot before its subid-incrementing child allocation.");
        fixture.Manager.FinishScreenTransition();

        var history = (RecentEnemyDefeats)typeof(RoomEntityManager)
            .GetField("_recentEnemyDefeats", flags)!.GetValue(fixture.Manager)!;
        history.BeginRoom(0x9a);
        history.MarkKilled(1);
        fixture.Manager.LoadRoom(4, _world.LoadRoom(4, 0x9a));
        // group4Map9aEnemyObjectData: two fixed Zols, then two random Keese;
        // killed entries still advance checkEnemyKilled's assigned index.
        FailIf(memory.ReadWramByte(0xcec9) != 2 || memory.ReadWramByte(0xceca) != 4 ||
            fixture.Manager.Entities<ZolCharacter>().Count != 1 || fixture.Manager.Entities<KeeseCharacter>().Count != 2,
            "Room entry must copy the defeated bitset and consume indices for both skipped and spawned enemies.");
        history.MarkKilled(2);
        FailIf(memory.ReadWramByte(0xcec9) != 2 || history.ActiveRoomBitset != 6,
            "Live defeats must not rewrite the already captured placement bitset.");
        fixture.Manager.LoadRoom(4, _world.LoadRoom(4, 0x9a));
        FailIf(memory.ReadWramByte(0xcec9) != 6 || memory.ReadWramByte(0xceca) != 4 ||
            fixture.Manager.Entities<ZolCharacter>().Count != 0,
            "Re-entry must refresh the bitset and restart the killable index from zero.");
        var next = typeof(RoomEntityFactory).GetMethod("NextKillableEnemyIndex", flags)!;
        memory.SetWramByte(0xceca, 6);
        FailIf((int)next.Invoke(factory, [1])! != 0 || memory.ReadWramByte(0xceca) != 6 ||
            (int)next.Invoke(factory, [0])! != 7 || memory.ReadWramByte(0xceca) != 7 ||
            (int)next.Invoke(factory, [0])! != 0 || memory.ReadWramByte(0xceca) != 7,
            "Flag01 must bypass the counter, while the seventh indexed enemy saturates it.");
    }
}
