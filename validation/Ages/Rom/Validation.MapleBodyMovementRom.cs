using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private (MapleEncounter Maple, SomariaRom Native) MapleBodyBoundary()
    {
        ReinitializeGameplayForValidation(); _saveData.SetGlobalFlag(AgesGlobalFlag.TuniNutPlaced);
        _saveData.SetMapleKillCounter(30); LoadValidationRoom(1, 2);
        _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(new(24, 112)); _player.BeginCutsceneControl();
        var maple = _entities.Entities<MapleEncounter>().Single();
        F1Retain(_entities.EntityAdapters<MapleEncounterRoomEntity>().Single());
        StepGameplayUpdates(1, Vector2.Zero); // Admit the runtime's source state0.
        FillValidationRoom(0x2c, width: 10, height: 8);
        var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 24, 112)
            { CompanionDispatchEnabled = true, HostilePartsEnabled = true, PostObjectCollisionsEnabled = false };
        native.InitializeLinkGameplay(); native.InitializeLinkWalkingAnimation();
        // Active-state entries below are declared independently of the room
        // loader. Native animation is held unless a source transition resets it.
        native[0xd100] = 1; native[0xd101] = 0x0e; native[0xd120] = 0x7f;
        native[0xd126] = native[0xd127] = 8; native[0xd11a] = 0x80;
        return (maple, native);
    }

    private void ValidateMapleMainFlightHandoffRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int seed in new[] { 0x0d37, 0x1234, 0x80ff, 0xffff })
        {
            var (maple, native) = MapleBodyBoundary();
            var path = new MapleEventDatabase().Path(MaplePathKind.Shadow, 1);
            F1Set(maple, "<Stage>k__BackingField", MapleEncounterStage.Flying);
            F1Set(maple, "_path", path); F1Set(maple, "_pathStep", path.Steps.Count - 1); F1Set(maple, "_pathCounter", 1);
            F1Set(maple, "_precisePosition", new Vector2(80.25f, 72.5f)); maple.Position = new(80, 72);
            F1Set(maple, "_counter", 0); F1Set(maple, "_angle", 0); F1Set(maple, "_targetAngle", 0);
            native[0xd104] = 2; native[0xd106] = 1;
            native.Word(0xd10a, 0x4880); native.Word(0xd10c, 0x5040);
            // A source terminal path record is a declared input. The native
            // transition selects its following ROM table with the shared RNG.
            native[0xd118] = 0; native[0xd119] = 0xd5; native[0xd500] = 0xff;
            _random.RestoreState(_random.CaptureState() with { Rng1 = (byte)seed, Rng2 = (byte)(seed >> 8) });
            var initial = _random.CaptureState(); native[0xff94] = initial.Rng1; native[0xff95] = initial.Rng2;
            StepGameplayUpdates(3, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
                var rng = _random.CaptureState();
                FailIf(!maple.MainFlight || native[0xd13e] != 1 ||
                    SomariaPrivate<Vector2>(maple, "_precisePosition") != new Vector2(native.Word(0xd10c) / 256f, native.Word(0xd10a) / 256f) ||
                    SomariaPrivate<int>(maple, "_counter") != native[0xd107] || maple.Angle != native[0xd109] ||
                    rng.Calls - initial.Calls != native.RandomCalls || rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95],
                    $"Maple $0e shadow/main flight handoff seed=${seed:x4}, batch={batched}: " +
                    $"port={SomariaPrivate<Vector2>(maple, "_precisePosition")}/angle{maple.Angle:x2}; source=({native.Word(0xd10c):x4},{native.Word(0xd10a):x4})/angle{native[0xd109]:x2}.");
            });
            _entities.Clear();
        }
    }

    private void ValidateMapleRecoilMovementRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int vehicle in new[] { 0, 1, 2 })
        foreach (int angle in new[] { 0, 0x18 })
        {
            var (maple, native) = MapleBodyBoundary();
            F1Set(maple, "<Stage>k__BackingField", MapleEncounterStage.Recoiling);
            F1Set(maple, "_vehicle", vehicle); F1Set(maple, "_precisePosition", new Vector2(8.5f, 32.75f)); maple.Position = new(8, 32);
            F1Set(maple, "_zFixed", -0x480); F1Set(maple, "_speedZ", 0x40); F1Set(maple, "_recoilBounce", -4);
            F1Set(maple, "_angle", angle); F1Set(maple, "_speedRaw", 0x28 + vehicle * 10);
            native[0xd104] = 3; native[0xd128] = (byte)vehicle; native[0xd13a] = 0xfc;
            native[0xd109] = (byte)angle; native[0xd110] = (byte)(0x28 + vehicle * 10);
            native.Word(0xd10a, 0x20c0); native.Word(0xd10c, 0x0880); native.Word(0xd10e, -0x480); native.Word(0xd114, 0x40);
            byte[] observed = new byte[4], expected = new byte[4];
            var probe = new PartMovementScratchProbe(() =>
            {
                for (int i = 0; i < 4; i++) observed[i] = _runtimeState.ReadWramByte(0xcec0 + i);
            });
            _entities.RegisterEnemySlot(probe, 15); _entities.AddEntity(probe);
            int update = 0;
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter, beforeInteractions: () =>
                {
                    for (int i = 0; i < 4; i++) expected[i] = native[0xcec0 + i];
                }); update++;
                FailIf(SomariaPrivate<Vector2>(maple, "_precisePosition") != new Vector2(native.Word(0xd10c) / 256f, native.Word(0xd10a) / 256f) ||
                    (SomariaPrivate<int>(maple, "_zFixed") & 0xffff) != native.Word(0xd10e) ||
                    (SomariaPrivate<int>(maple, "_speedZ") & 0xffff) != native.Word(0xd114) ||
                    (SomariaPrivate<int>(maple, "_recoilBounce") & 0xff) != native[0xd13a],
                    $"Maple recoil vehicle={vehicle} angle=${angle:x2} update={update}, batch={batched}: " +
                    $"port={SomariaPrivate<Vector2>(maple, "_precisePosition")}/Z{SomariaPrivate<int>(maple, "_zFixed") & 0xffff:x4}/v{SomariaPrivate<int>(maple, "_speedZ") & 0xffff:x4}; " +
                    $"source=({native.Word(0xd10c):x4},{native.Word(0xd10a):x4})/Z{native.Word(0xd10e):x4}/v{native.Word(0xd114):x4}.");
                FailIf(!observed.SequenceEqual(expected), "Maple recoil must publish movement scratch before the later interaction and warp consumers.");
            });
            Step(8);
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0x20); native[0xcc8a] = 0x20;
            Step(3);
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0); native[0xcc8a] = 0;
            Step(8); _entities.Clear();
        }
    }

    private void ValidateMapleCollisionHeightRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            var (maple, native) = MapleBodyBoundary();
            _player.WarpTo(new(78, 72)); native[0xd00b] = 72; native[0xd00d] = 78;
            F1Set(maple, "<Stage>k__BackingField", MapleEncounterStage.Flying); F1Set(maple, "_mainFlight", true);
            F1Set(maple, "_pathCounter", 60); F1Set(maple, "_counter", 0); F1Set(maple, "_angle", 0x18); F1Set(maple, "_targetAngle", 0x18);
            F1Set(maple, "_speedRaw", 0x50); F1Set(maple, "_precisePosition", new Vector2(80.25f, 72.5f)); maple.Position = new(80, 72);
            F1Set(maple, "_zFixed", -0x101); F1Set(maple, "_speedZ", 0x40); F1Set(maple, "_oscillationCounter", 22); F1Set(maple, "_dropPattern", 1);
            native[0xd103] = 1; native[0xd104] = 2; native[0xd106] = 60; native[0xd109] = native[0xd13d] = 0x18;
            native[0xd110] = 0x50; native[0xd13e] = 1; native[0xd13c] = 22;
            native.Word(0xd10a, 0x4880); native.Word(0xd10c, 0x5040); native.Word(0xd10e, -0x101); native.Word(0xd114, 0x40);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
            foreach (int address in new[] { 0xcc02, 0xcc8a, 0xcc91 })
                FailIf(_runtimeState.ReadWramByte(address) != native[address],
                    $"Maple contact must publish the shared source control byte at ${address:x4}.");
            var drops = _entities.Entities<MapleDroppedItem>();
            var slots = Enumerable.Range(0, 16).Select(slot => 0xd0c0 + slot * 256)
                .Where(at => native[at] != 0 && native[at + 1] is 0x14 or 0x15).ToArray();
            FailIf(maple.Stage != MapleEncounterStage.Recoiling || native[0xd104] != 3 || drops.Count == 0 || drops.Count != slots.Length,
                "The declared ordinary-Link overlap must collide through the source/runtime companion passes and create corresponding checked drops.");
            foreach (var drop in drops)
            {
                int at = 0xd0c0 + drop.Slot * 256;
                FailIf(drop.ItemIndex != native[at + 2] || (drop.ZFixed & 0xffff) != native.Word(at + 14) ||
                    SomariaPrivate<Vector2>(drop, "_precisePosition") != new Vector2(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f),
                    $"Maple collision must scatter at pre-oscillation high XYZ: slot=${drop.Slot:x2}, port Z={drop.ZFixed & 0xffff:x4}, source Z={native.Word(at + 14):x4}.");
            }
            FailIf(SomariaPrivate<int>(maple, "_zFixed") != -0x101 || native.Word(0xd10e) != 0xfeff,
                "Maple's collision update must return before oscillation alters the producer height.");
            _entities.Clear();
        }
    }

    private void ValidateMapleContactGatesRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (string gate in new[] { "none", "menu", "collision-menu", "invincible", "shield-recoil", "knockback" })
        foreach (Vector2 link in new[] { new Vector2(80, 72), new(66, 72), new(94, 72), new(80, 58), new(80, 86) })
        {
            var (maple, native) = MapleBodyBoundary();
            _inventory.GiveTreasure(TreasureId.TradeItem, 8);
            for (int address = 0xc5b0; address < 0xcb00; address++) native[address] = _saveData.ReadWramByte(address);
            _player.WarpTo(link); native[0xd00b] = (byte)link.Y; native[0xd00d] = (byte)link.X;
            F1Set(maple, "<Stage>k__BackingField", MapleEncounterStage.Flying); F1Set(maple, "_mainFlight", true);
            F1Set(maple, "_pathCounter", 60); F1Set(maple, "_counter", 0); F1Set(maple, "_angle", 0); F1Set(maple, "_targetAngle", 0);
            F1Set(maple, "_speedRaw", 0); F1Set(maple, "_precisePosition", new Vector2(80, 72)); maple.Position = new(80, 72);
            F1Set(maple, "_zFixed", -0x800); F1Set(maple, "_speedZ", 0x40); F1Set(maple, "_oscillationCounter", 22);
            native[0xd104] = 2; native[0xd106] = 60; native[0xd13e] = 1; native[0xd13c] = 22;
            native[0xd10b] = 72; native[0xd10d] = 80; native.Word(0xd10e, -0x800); native.Word(0xd114, 0x40);
            if (gate == "menu") { _runtimeState.SetWramByte(AgesWramAddress.wMenuDisabled, 0x80); native[0xcc02] = 0x80; }
            if (gate == "collision-menu") { _runtimeState.SetWramByte(AgesWramAddress.wDisableLinkCollisionsAndMenu, 1); native[0xcbca] = 1; }
            if (gate is "invincible" or "shield-recoil")
            {
                F1Set(_player, "_enemyInvincibilityFrames", gate == "invincible" ? 1f : -1f);
                native[0xd02b] = gate == "invincible" ? (byte)1 : (byte)0xff;
            }
            if (gate == "knockback") { F1Set(_player, "_enemyKnockbackFrames", 1f); native[0xd02d] = 1; }
            StepGameplayUpdates(1, Vector2.Zero, batched: batched); native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
            bool sourceHit = native[0xd104] == 3;
            bool expected = gate == "none" && (int)link.X - 80 >= -14 && (int)link.X - 80 < 14 &&
                (int)link.Y - 72 >= -14 && (int)link.Y - 72 < 14;
            FailIf(sourceHit != expected || (maple.Stage == MapleEncounterStage.Recoiling) != sourceHit ||
                (maple.TouchingBook is not null) != sourceHit,
                $"Maple native Link vulnerability/byte collision gate={gate}, Link={link}, batch={batched}: " +
                $"port={maple.Stage}, source=${native[0xd104]:x2}, traced hit={expected}.");
            _runtimeState.SetWramByte(AgesWramAddress.wMenuDisabled, 0);
            _runtimeState.SetWramByte(AgesWramAddress.wDisableLinkCollisionsAndMenu, 0);
            _entities.Clear();
        }
    }

    private void ValidateMapleRaceMovementRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int vehicle in new[] { 0, 1, 2 })
        foreach (Vector2 start in new[] { new Vector2(8.5f, 32.75f), new(152.5f, 120.75f) })
        {
            var (maple, native) = MapleBodyBoundary();
            F1Set(maple, "<Stage>k__BackingField", MapleEncounterStage.Racing);
            F1Set(maple, "_vehicle", vehicle); F1Set(maple, "_precisePosition", start); maple.Position = start.Floor();
            F1Set(maple, "_zFixed", -0x800); F1Set(maple, "_speedZ", 0x40); F1Set(maple, "_oscillationCounter", 22);
            F1Set(maple, "_angle", 0); F1Set(maple, "_targetAngle", 0); F1Set(maple, "_speedRaw", 0x50);
            F1Set(maple, "_turnCounter", 1); F1Set(maple, "_turnDelay", 1); F1Set(maple, "_raceMusicStarted", true);
            var encounter = SomariaPrivate<MapleEncounterState>(maple, "_encounter");
            var drop = _entities.Spawn<MapleDroppedItem>(new MapleDroppedItemSpawn(new MapleEventDatabase().Item(13), encounter, 0, new(80, 72), 0));
            F1Set(drop, "_state", MapleDroppedItemState.Grounded); drop.Visible = true;
            native[0xd104] = 6; native[0xd128] = (byte)vehicle; native[0xd13a] = native[0xd13b] = 1; native[0xd13c] = 22;
            native[0xd110] = 0x50; native[0xd126] = native[0xd127] = vehicle == 2 ? (byte)4 : (byte)2;
            native.Word(0xd10a, (int)(start.Y * 256)); native.Word(0xd10c, (int)(start.X * 256));
            native.Word(0xd10e, -0x800); native.Word(0xd114, 0x40);
            native[0xd0c0] = 1; native[0xd0c1] = 0x14; native[0xd0c2] = native[0xd0c3] = 13; native[0xd0c4] = 2;
            native[0xd0cb] = 72; native[0xd0cd] = 80; native[0xd0e4] = 0x94; native[0xd0e9] = 1;
            byte[] observed = new byte[4], expected = new byte[4];
            var probe = new PartMovementScratchProbe(() =>
            {
                for (int i = 0; i < 4; i++) observed[i] = _runtimeState.ReadWramByte(0xcec0 + i);
            });
            _entities.RegisterEnemySlot(probe, 15); _entities.AddEntity(probe);
            int update = 0;
            StepGameplayUpdates(12, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter, beforeInteractions: () =>
                {
                    for (int i = 0; i < 4; i++) expected[i] = native[0xcec0 + i];
                }); update++;
                FailIf((int)maple.Stage != native[0xd104] || maple.Angle != native[0xd109] || maple.TargetAngle != native[0xd13d] ||
                    SomariaPrivate<Vector2>(maple, "_precisePosition") != new Vector2(native.Word(0xd10c) / 256f, native.Word(0xd10a) / 256f) ||
                    (SomariaPrivate<int>(maple, "_zFixed") & 0xffff) != native.Word(0xd10e) || !observed.SequenceEqual(expected),
                    $"Maple race vehicle={vehicle}, start={start}, update={update}, batch={batched}: " +
                    $"port={maple.Stage}/{SomariaPrivate<Vector2>(maple, "_precisePosition")}/{maple.Angle:x2}->{maple.TargetAngle:x2}; " +
                    $"source={native[0xd104]:x2}/({native.Word(0xd10c):x4},{native.Word(0xd10a):x4})/{native[0xd109]:x2}->{native[0xd13d]:x2}.");
            });
            _entities.Clear();
        }
    }

    private void ValidateMapleDepartureMovementRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool book in new[] { false, true })
        {
            var (maple, native) = MapleBodyBoundary();
            F1Set(maple, "<Stage>k__BackingField", book ? MapleEncounterStage.BookDeparture : MapleEncounterStage.Outcome);
            F1Set(maple, "_substate", book ? 0 : 2); F1Set(maple, "_precisePosition", new Vector2(0.5f, 72.75f)); maple.Position = new(0, 72);
            F1Set(maple, "_zFixed", -0x800); F1Set(maple, "_speedZ", 0x40); F1Set(maple, "_oscillationCounter", 22);
            F1Set(maple, "_angle", 0x18); F1Set(maple, "_speedRaw", 0x78);
            native[0xd104] = book ? (byte)12 : (byte)9; native[0xd105] = book ? (byte)0 : (byte)2;
            native[0xd109] = 0x18; native[0xd110] = 0x78; native[0xd13c] = 22;
            native.Word(0xd10a, 0x48c0); native.Word(0xd10c, 0x0080); native.Word(0xd10e, -0x800); native.Word(0xd114, 0x40);
            int update = 0;
            StepGameplayUpdates(4, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); update++;
                FailIf(maple.Finished != (native[0xd100] == 0) || _saveData.MapleState != native[0xc644],
                    $"Maple {(book ? "book" : "outcome")} departure update={update}, batch={batched}: " +
                    $"port finished={maple.Finished}/state${_saveData.MapleState:x2}, source enabled={native[0xd100]}/state${native[0xc644]:x2}.");
                if (!maple.Finished)
                    FailIf(SomariaPrivate<Vector2>(maple, "_precisePosition") != new Vector2(native.Word(0xd10c) / 256f, native.Word(0xd10a) / 256f) ||
                        (SomariaPrivate<int>(maple, "_zFixed") & 0xffff) != native.Word(0xd10e),
                        "Maple departure must preserve wrapped fixed coordinates and oscillation until the native off-screen deletion visit.");
            });
            FailIf(!maple.Finished, "Maple must finish departure after traversing the original wrapped screen boundary.");
            _entities.Clear();
        }
    }

    private void ValidateMapleRiseHandoffRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int vehicle in new[] { 0, 1, 2 })
        {
            var (maple, native) = MapleBodyBoundary();
            foreach (int address in new[] { 0xcc02, 0xcc8a, 0xcc91 })
            {
                _runtimeState.SetWramByte(address, 1); native[address] = 1;
            }
            F1Set(maple, "<Stage>k__BackingField", MapleEncounterStage.Rising);
            F1Set(maple, "_vehicle", vehicle); F1Set(maple, "_mainFlight", true);
            F1Set(maple, "_precisePosition", new Vector2(80, 72)); maple.Position = new(80, 72);
            F1Set(maple, "_zFixed", 0); F1Set(maple, "_angle", 8);
            native[0xd104] = 5; native[0xd128] = (byte)vehicle; native[0xd13e] = 1;
            native[0xd10b] = 72; native[0xd10d] = 80; native[0xd109] = 8;
            int update = 0;
            StepGameplayUpdates(vehicle == 0 ? 7 : 23, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); update++;
                foreach (int address in new[] { 0xcc02, 0xcc8a, 0xcc91 })
                    FailIf(_runtimeState.ReadWramByte(address) != native[address],
                        $"Maple rising handoff update={update}: shared control byte ${address:x4} differs.");
                FailIf((int)maple.Stage != native[0xd104] ||
                    (SomariaPrivate<int>(maple, "_zFixed") & 0xffff) != native.Word(0xd10e) || maple.Angle != native[0xd109],
                    $"Maple rise vehicle={vehicle}, update={update}, batch={batched}: " +
                    $"port={maple.Stage}/Z{SomariaPrivate<int>(maple, "_zFixed") & 0xffff:x4}, source=${native[0xd104]:x2}/Z{native.Word(0xd10e):x4}.");
            });
            FailIf(maple.Stage != MapleEncounterStage.Outcome || native[0xd104] != 9,
                "An empty declared PART stream must select the outcome on the exact source rising-height visit.");
            _entities.Clear();
        }
    }
}
