namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsMapleEncounters()
    {
        var data = new MapleEventDatabase(SeasonsGameContent.AssetRoot);
        FailIf(data.PathStepCount != 61 || data.ItemCount != 14 || data.Visual.Animations.Length != 32 ||
            !data.Text(0x0709).EndsWith("Oh my!", StringComparison.Ordinal) ||
            !data.Text(0x005c).Contains("Ghastly\nDoll", StringComparison.Ordinal) ||
            data.Item(0).Value != 0x3c || data.Item(13).Treasure != 0x28,
            "Seasons Maple must import its own flight, loot, egg dialogue and Ghastly Doll reward.");
        var rings = new RandomRingDatabase(SeasonsGameContent.AssetRoot);
        // treasureAndDrops.s:ringTierTable, independently of Maple's generated items.
        FailIf(rings.SelectRing(2, 0) != 0x0f || rings.SelectRing(2, 7) != 0x22 ||
            rings.SelectRing(4, 0) != 0x09 || rings.SelectRing(4, 1) != 0x29,
            "Seasons Maple ring rewards must use the native ring tiers without Ages Gasha dependencies.");
        foreach (bool batched in new[] { false, true })
        foreach (bool trade in new[] { false, true })
        {
            var root = StartSeasonsAfterImpa(batched);
            try
            {
                var session = root._seasonsOpening!;
                var app = new ApplicationValidationFixture(root);
                session.Inventory.SetScriptedEquippedItems(0, 0);
                session.Inventory.GiveTreasure(TreasureId.Sword, 1);
                session.Inventory.GiveTreasure(TreasureId.EmberSeeds, 0x20);
                session.Inventory.GiveTreasure(TreasureId.TradeItem, trade ? 1 : 0);
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    session.Save.SetMapleKillCounter(30);
                    session.DebugTravel(0, 0x12);
                    var maple = session.Entities.Entities<MapleEncounter>().Single();
                    var floor = Enumerable.Range(0, 80).Select(index => new Vector2(index % 10 * 16 + 8, index / 10 * 16 + 8))
                        .Where(position => position.X is >= 24 and <= 136 && position.Y is >= 24 and <= 104 && !session.Collision.Collides(position))
                        .OrderBy(position => position.DistanceSquaredTo(new Vector2(80, 64))).First();
                    session.Link.WarpTo(floor);
                    int greeting = -1, drops = 0;
                    bool collided = false;
                    for (int update = 0; update < 6000 && !maple.Finished; update++)
                    {
                        Vector2 input = Vector2.Zero;
                        if (maple.Stage == MapleEncounterStage.Flying && maple.MainFlight &&
                            maple.Position.X is >= 16 and <= 144 && maple.Position.Y is >= 16 and <= 112)
                        {
                            Vector2 difference = maple.Position - session.Link.Position;
                            input = Mathf.Abs(difference.X) > Mathf.Abs(difference.Y)
                                ? new Vector2(Mathf.Sign(difference.X), 0) : new Vector2(0, Mathf.Sign(difference.Y));
                        }
                        if (session.Dialogue.IsOpen)
                        {
                            greeting = session.Dialogue.SourceTextId ?? -1;
                            session.Dialogue.Close();
                        }
                        app.Step(batched ? 2 : 1, input, batched: batched);
                        collided |= maple.Stage == MapleEncounterStage.Recoiling;
                        drops = Math.Max(drops, session.Entities.Entities<MapleDroppedItem>().Count);
                    }
                    FailIf(!collided || !maple.Finished || session.Save.MapleKillCounter != 0 ||
                        (session.Save.MapleState & 0x0f) != repeat + 1 || session.Memory.ReadField("wIsMaplePresent") != 0 ||
                        session.Memory.ReadField("wMenuDisabled") != 0 || session.Memory.ReadField("wDisableScreenTransitions") != 0,
                        $"Seasons Maple lifecycle trade={trade}, repeat={repeat}, batch={batched}: contact={collided}, stage={maple.Stage}, XY={maple.Position}, Link={session.Link.Position}, state=${session.Save.MapleState:x2}.");
                    FailIf(repeat == 0 && trade ? session.Inventory.TradeItem != 2 || drops != 0 : drops == 0,
                        "Maple must trade the Lon Lon Egg for the Ghastly Doll once, then return to the ordinary item scramble.");
                    FailIf(greeting < 0, "Seasons Maple must deliver dialogue through the active gameplay interaction controller.");
                    session.Dialogue.Close();
                    app.Step(8, Vector2.Zero, batched: batched);
                    FailIf(session.Entities.Entities<MapleDroppedItem>().Any() || session.Entities.Entities<MapleEncounter>().Any(),
                        "Finished Maple encounters must retire their shared special-object and part presentations.");
                }
                session.Save.SetMapleKillCounter(30);
                session.DebugTravel(0, 0x12);
                app.Step(8, Vector2.Zero, batched: batched);
                session.DebugTravel(0, 0x97);
                FailIf(session.Entities.Entities<MapleEncounter>().Any() || session.Entities.Entities<MapleDroppedItem>().Any() ||
                    session.Entities.EntityAdapters<MapleLonLonEggRoomEntity>().Any(),
                    "Cancelling a Seasons Maple encounter must retire its children before re-entry.");
            }
            finally { root.Free(); }
        }
    }

    private void ValidateSeasonsMapleEggRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            var root = StartSeasonsAfterImpa(batched);
            try
            {
                var session = root._seasonsOpening!;
                var app = new ApplicationValidationFixture(root);
                session.Inventory.SetScriptedEquippedItems(0, 0);
                session.Inventory.GiveTreasure(TreasureId.TradeItem, 1);
                session.Save.SetMapleKillCounter(30);
                session.DebugTravel(0, 0x12);
                var maple = session.Entities.Entities<MapleEncounter>().Single();
                app.Step(1, Vector2.Zero, batched: batched);
                session.Link.WarpTo(new(80.25f, 80.5f));
                session.Link.BeginCutsceneControl();
                // Declared stationary parent isolates INTERAC$94's native states.
                session.Memory.SetField("wDisabledObjects", 0x20);
                var native = new BraceletRom(session.Room, session.Link.PrecisePosition, 2, 0xff, game: OracleGame.Seasons);
                native.LoadSave(session.Save);
                native[0xcc8a] = 0x20;
                native.Word(0xd00e, session.Link.ItemCreationZFixed);
                var egg = (MapleLonLonEggRoomEntity)session.Entities.TryCreateInteractionEntity(
                    new MapleLonLonEggSpawn(maple, new MapleEventDatabase(SeasonsGameContent.AssetRoot).EggVisual))!;
                int slot = session.Entities.NativeInteractionSlot(egg), at = 0xd040 + slot * 256;
                native[at] = 1; native[at + 1] = 0x94;
                int update = 0;
                void Step(int count) => app.Step(count, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    native.AdvanceInteractions(session.FrameCounter); update++;
                    FailIf(egg.Finished != (native[at] == 0) ||
                        !egg.Finished && (egg.State != native[at + 4] || SomariaPrivate<Vector2>(egg, "_precisePosition") !=
                            new Vector2(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f) ||
                            (SomariaPrivate<int>(egg, "_zFixed") & 0xffff) != native.Word(at + 14)),
                        $"Seasons INTERAC$94 visit={update}, batch={batched}: port state={egg.State}, XY={SomariaPrivate<Vector2>(egg, "_precisePosition")}, Z=${SomariaPrivate<int>(egg, "_zFixed") & 0xffff:x4}; native state={native[at + 4]}, XY=(${native.Word(at + 12):x4},${native.Word(at + 10):x4}), Z=${native.Word(at + 14):x4}.");
                });
                Step(1);
                FailIf(egg.State != 1 || session.Memory.ReadField("wMenuDisabled") != 1,
                    "Lon Lon Egg state0 must copy Link's high XYZ and set the menu lock before yielding.");
                session.Dialogue.ShowGameplayMessage("Freeze", 80);
                native[0xcba0] = 1;
                Step(3);
                FailIf(egg.State != 1 || SomariaPrivate<int>(egg, "_speedZ") != -0x100,
                    "Seasons' egg has no always-update bit and must freeze under text.");
                session.Dialogue.Close(); native[0xcba0] = 0;
                for (int wait = 0; wait < 32 && egg.State != 2; wait++) Step(1);
                FailIf(egg.State != 2, "Lon Lon Egg must finish its native $20-gravity flight.");
                var target = SomariaPrivate<Vector2>(egg, "_precisePosition").Floor();
                F1Set(maple, "_precisePosition", target); maple.Position = target;
                session.Save.SetMapleState(session.Save.MapleState | 0x20);
                native.LoadSave(session.Save);
                native[0xd10b] = (byte)target.Y; native[0xd10d] = (byte)target.X;
                Step(1);
                FailIf(egg.State != 3 || !session.Dialogue.IsOpen || session.Dialogue.SourceTextId != 0x070a || egg.Visible,
                    "Maple must pick up the landed egg through TX_070a before granting the doll.");
                Step(3);
                FailIf(session.Inventory.TradeItem != 1, "The egg trade must wait until TX_070a closes.");
                session.Dialogue.Close(); native[0xcba0] = 0;
                Step(1);
                FailIf(egg.State != 4 || session.Inventory.TradeItem != 2 || session.Dialogue.SourceTextId != 0x005c ||
                    session.Memory.ReadField("wLinkForceState") != 4 || session.Memory.ReadField("wcc50") != 1 ||
                    session.Save.ReadField("wTradeItem") != native.ReadNativeByte(0xc6bc),
                    "INTERAC$94 must directly grant the Ghastly Doll, show TX_005c and request native two-hand item-get.");
                session.Dialogue.Close(); native[0xcba0] = 0;
                Step(63);
                FailIf(egg.Finished || SomariaPrivate<int>(egg, "_counter") != 1,
                    "Lon Lon Egg must retain its owner through the first $3f eligible reward visits.");
                Step(1);
                FailIf(!egg.Finished || native[at] != 0 || maple.Substate != 2 ||
                    session.Memory.ReadField("wDisabledObjects") != 0,
                    "The $40th eligible visit must release objects, signal Maple's two-visit departure and delete INTERAC$94.");
                session.Entities.Clear();
            }
            finally { root.Free(); }
        }
    }
}
