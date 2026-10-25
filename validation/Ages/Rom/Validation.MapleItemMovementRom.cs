using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void MapleMovementRoom()
    {
        ReinitializeGameplayForValidation(); LoadValidationRoom(1, 2); _entities.Clear();
        _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(new(24, 112)); _player.BeginCutsceneControl();
        FillValidationRoom(0x2c, width: 10, height: 8);
    }

    private void ValidateMapleScatterMovementRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int seed in new[] { 0x0d37, 0x1234, 0x80ff, 0xffff })
        foreach (Vector2 start in new[] { new Vector2(8, 32), new(152, 120) })
        {
            MapleMovementRoom();
            _random.RestoreState(_random.CaptureState() with { Rng1 = (byte)seed, Rng2 = (byte)(seed >> 8) });
            var initial = _random.CaptureState();
            var encounter = new MapleEncounterState(_runtimeState) { ObjectsDisabled = true };
            var drop = _entities.Spawn<MapleDroppedItem>(new MapleDroppedItemSpawn(new MapleEventDatabase().Item(13), encounter, 0, start, -0x800));
            var native = new SomariaRom(_saveData, initial, _currentRoom, 0, 24, 112);
            const int at = 0xd0c0;
            native[at] = 1; native[at + 1] = 0x14; native[at + 2] = native[at + 3] = 13;
            native.Word(at + 10, (int)(start.Y * 256)); native.Word(at + 12, (int)(start.X * 256)); native.Word(at + 14, -0x800);
            byte[] sentinel = [0xa5, 0x5a, 0xc3, 0x3c], observed = new byte[4];
            var stage = new PartMovementScratchProbe(() =>
            {
                for (int i = 0; i < 4; i++) _runtimeState.SetWramByte(0xcec0 + i, sentinel[i]);
            });
            _entities.RegisterEnemySlot(stage, 15); _entities.AddEntity(stage);
            _entities.AddEntity(new PartMovementScratchProbe(() =>
            {
                for (int i = 0; i < 4; i++) observed[i] = _runtimeState.ReadWramByte(0xcec0 + i);
            }));
            int update = 0;
            bool held = false;
            var priorText = _entities.TextActiveSource;
            _entities.TextActiveSource = () => held;
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                for (int i = 0; i < 4; i++) native[0xcec0 + i] = sentinel[i];
                native[0xcba0] = held ? (byte)1 : (byte)0;
                native.AdvanceParts(_entities.FrameCounter); update++;
                var rng = _random.CaptureState();
                FailIf(SomariaPrivate<Vector2>(drop, "_precisePosition") != new Vector2(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f) ||
                    (drop.ZFixed & 0xffff) != native.Word(at + 14) ||
                    (SomariaPrivate<int>(drop, "_speedZ") & 0xffff) != native.Word(at + 20) ||
                    (int)drop.State != native[at + 4] || rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95] || rng.Calls - initial.Calls != native.RandomCalls,
                    $"Maple scatter $14 seed=${seed:x4}, start={start}, update={update}, batch={batched}: " +
                    $"port={drop.State}/{SomariaPrivate<Vector2>(drop, "_precisePosition")}/Z{drop.ZFixed & 0xffff:x4}; " +
                    $"source={native[at + 4]}/({native.Word(at + 12):x4},{native.Word(at + 10):x4})/Z{native.Word(at + 14):x4}.");
                FailIf(Enumerable.Range(0, 4).Any(i => observed[i] != native[0xcec0 + i]),
                    $"Maple scatter $14 update={update} must preserve native phase-local movement writes, including no-motion/frozen visits.");
            });
            try
            {
                Step(1); Step(3); held = true; Step(3); held = false; Step(120);
                FailIf(drop.State != MapleDroppedItemState.Grounded, "Maple scatter must reach its stationary state after the source bounces.");
            }
            finally { _entities.TextActiveSource = priorText; _entities.Clear(); }
        }
    }

    private void ValidateMapleItemPullRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int vehicle in new[] { 0, 1, 2 })
        foreach (Vector2 start in new[] { new Vector2(79, 72), new(80.25f, 72.5f), new(80, 72) })
        foreach (int height in new[] { 0, -0x580 })
        {
            MapleMovementRoom();
            var encounter = new MapleEncounterState(_runtimeState) { ObjectsDisabled = true };
            var drop = _entities.Spawn<MapleDroppedItem>(new MapleDroppedItemSpawn(new MapleEventDatabase().Item(13), encounter, 0, start, height));
            StepGameplayUpdates(1, Vector2.Zero); // Source state0 is separately checked by scatter.
            // Declared state4 acquisition: both grounded and airborne parts
            // can be selected by Maple. Retain deliberately nonzero fractions.
            F1Set(drop, "_precisePosition", start); drop.Position = start.Floor(); F1Set(drop, "_zFixed", height);
            drop.BeginMapleCollection(vehicle, () => new(80, 72));
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 24, 112);
            const int at = 0xd0c0;
            native[at] = 1; native[at + 1] = 0x14; native[at + 2] = native[at + 3] = 13;
            native[at + 4] = 4; native[at + 0x24] = 0x94; native[at + 0x29] = 1;
            native.Word(at + 10, (int)(start.Y * 256)); native.Word(at + 12, (int)(start.X * 256)); native.Word(at + 14, height);
            native[0xd128] = (byte)vehicle; native[0xd10b] = 72; native[0xd10d] = 80;
            byte[] sentinel = [0xa5, 0x5a, 0xc3, 0x3c], observed = new byte[4];
            var stage = new PartMovementScratchProbe(() =>
            {
                for (int i = 0; i < 4; i++) _runtimeState.SetWramByte(0xcec0 + i, sentinel[i]);
            });
            _entities.RegisterEnemySlot(stage, 15); _entities.AddEntity(stage);
            _entities.AddEntity(new PartMovementScratchProbe(() =>
            {
                for (int i = 0; i < 4; i++) observed[i] = _runtimeState.ReadWramByte(0xcec0 + i);
            }));
            int update = 0;
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                for (int i = 0; i < 4; i++) native[0xcec0 + i] = sentinel[i];
                native.AdvanceParts(_entities.FrameCounter); update++;
                int substate = drop.State switch { MapleDroppedItemState.MaplePulling => 1, MapleDroppedItemState.MapleRising => 2, _ => 3 };
                FailIf(native[at + 4] != 4 || substate != native[at + 5] ||
                    drop.MapleCollectionReady != (native[0xd125] != 0) ||
                    SomariaPrivate<Vector2>(drop, "_precisePosition") != new Vector2(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f) ||
                    (drop.ZFixed & 0xffff) != native.Word(at + 14),
                    $"Maple pull $14 vehicle={vehicle}, start={start}, height={height}, visit={update}, batch={batched}: " +
                    $"port={drop.State}/{SomariaPrivate<Vector2>(drop, "_precisePosition")}/Z{drop.ZFixed & 0xffff:x4}; " +
                    $"source={native[at + 5]}/({native.Word(at + 12):x4},{native.Word(at + 10):x4})/Z{native.Word(at + 14):x4}/ready{native[0xd125]}.");
                FailIf(Enumerable.Range(0, 4).Any(i => observed[i] != native[0xcec0 + i]),
                    "Maple pull must publish only the native movement visits, preserving scratch while rising/ready.");
            });
            Step(1);
            for (int wait = 0; !drop.MapleCollectionReady && wait < 48; wait++) Step(1);
            FailIf(!drop.MapleCollectionReady, "Maple's acquired item must publish its ready handshake at the source height boundary.");
            Step(2); _entities.Clear();
        }
    }

    private void ValidateMapleItemHandoffRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int vehicle in new[] { 0, 1, 2 })
        for (int repeat = 0; repeat < 2; repeat++)
        {
            ReinitializeGameplayForValidation(); _saveData.SetGlobalFlag(AgesGlobalFlag.TuniNutPlaced);
            _saveData.SetMapleKillCounter(30); LoadValidationRoom(1, 2);
            _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(new(24, 112)); _player.BeginCutsceneControl();
            var maple = _entities.Entities<MapleEncounter>().Single();
            F1Retain(_entities.EntityAdapters<MapleEncounterRoomEntity>().Single());
            FillValidationRoom(0x2c, width: 10, height: 8);
            var encounter = SomariaPrivate<MapleEncounterState>(maple, "_encounter"); encounter.ObjectsDisabled = true;
            var drop = _entities.Spawn<MapleDroppedItem>(new MapleDroppedItemSpawn(new MapleEventDatabase().Item(13), encounter, 0, new(79, 72), 0));
            F1Set(drop, "_state", MapleDroppedItemState.Grounded); drop.Visible = true;
            drop.BeginMapleCollection(vehicle, () => new(80, 72));
            // Declare the acquired-item boundary, then run both actual
            // SPECIALOBJECT-before-Link and later PART passes. Animations
            // are held at a declared nonterminal frame in the native actor.
            F1Set(maple, "<Stage>k__BackingField", MapleEncounterStage.Collecting);
            F1Set(maple, "_vehicle", vehicle); F1Set(maple, "_precisePosition", new Vector2(80, 72)); maple.Position = new(80, 72);
            F1Set(maple, "_targetItem", drop); F1Set(maple, "_targetItemIndex", 13);
            F1Set(maple, "_angle", 8); F1Set(maple, "_targetAngle", 8);
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 24, 112)
                { CompanionDispatchEnabled = true, HostilePartsEnabled = true, PostObjectCollisionsEnabled = false };
            native.InitializeLinkGameplay(); native.InitializeLinkWalkingAnimation(); native[0xcc8a] = 1;
            native[0xd100] = 1; native[0xd101] = 0x0e; native[0xd104] = 7; native[0xd120] = 0x7f;
            native[0xd10b] = 72; native[0xd10d] = 80; native[0xd128] = (byte)vehicle;
            native[0xd12e] = 13; native[0xd118] = 0xd0; native[0xd119] = 0xc0;
            native[0xd13d] = native[0xd109] = 8;
            const int at = 0xd0c0;
            native[at] = 1; native[at + 1] = 0x14; native[at + 2] = native[at + 3] = 13;
            native[at + 4] = 4; native[at + 0x24] = 0x94; native[at + 0x29] = 1;
            native[at + 11] = 72; native[at + 13] = 79;
            int updates = 0;
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); updates++;
                bool exists = _entities.Entities<MapleDroppedItem>().Count != 0;
                FailIf(exists != (native[at] != 0) || maple.MapleScore != native[0xd129] ||
                    (int)maple.Stage != native[0xd104],
                    $"Maple $0e/$14 handoff vehicle={vehicle} repeat={repeat} update={updates}, batch={batched}: " +
                    $"port={maple.Stage}/score{maple.MapleScore}/item{exists}; source={native[0xd104]:x2}/score{native[0xd129]}/item{native[at]}.");
                if (exists)
                    FailIf(drop.MapleCollectionReady != (native[0xd125] != 0) || (drop.ZFixed & 0xffff) != native.Word(at + 14) ||
                        SomariaPrivate<Vector2>(drop, "_precisePosition") != new Vector2(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f),
                        "Maple handoff must retain exact item XYZ and publish readiness after the companion pass, then consume it on the next companion visit.");
            });
            Step(1);
            for (int wait = 0; _entities.Entities<MapleDroppedItem>().Count != 0 && wait < 48; wait++) Step(1);
            FailIf(maple.MapleScore != 1 || _entities.Entities<MapleDroppedItem>().Count != 0 || F1Parts.Count != 0,
                "Maple ready consumption must grant the source value once and release the physical PART slot on that update.");
            _entities.Clear();
        }
    }
}
