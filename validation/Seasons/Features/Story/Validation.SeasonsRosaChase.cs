namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsRosaChase()
    {
        foreach (bool batched in new[] { false, true })
        {
            var root = StartSeasonsAfterImpa(batched);
            try
            {
                var s = root._seasonsOpening!;
                var app = new ApplicationValidationFixture(root);
                var data = new SeasonsTalkNpcDatabase();
                FailIf(!data.NativeTable("rosa-rooms").SequenceEqual(new byte[] { 0xcb, 0xbb, 0xab, 0x9b, 0x9a }) ||
                    !data.NativeTable("rosa-start-positions").SequenceEqual(new byte[] { 0x28, 0x50, 0x58, 0x68, 0x58, 0x58, 0x58, 0x58, 0x58, 0x58 }),
                    "Rosa's parent route and Y/X spawn rows must match subrosiansHiding.s tables $686c/$67f1.");
                s.DebugTravel(0, 0xcb);
                app.Step(1, Vector2.Zero);
                FailIf(s.Entities.EntityAdapters<SeasonsRosaChaseRoomEntity>().Any() || s.DisabledObjects != 0,
                    "Rosa's source controller must delete before the Fertile Soil Essence is obtained.");
                s.Inventory.GiveTreasure(TreasureId.Essence, 0);
                s.Save.SetRoomFlag(0, 0xcb, 0x80);
                s.DebugTravel(0, 0xcb); app.Step(1, Vector2.Zero);
                FailIf(s.Entities.EntityAdapters<SeasonsRosaChaseRoomEntity>().Any(),
                    "Room $cb flag $80 must suppress an already completed Rosa chase.");
                s.Save.SetRoomFlag(0, 0xcb, 0x80, false);
                for (int visit = 0; visit < 2; visit++)
                {
                    s.DebugTravel(0, 0xcb);
                    Vector2 held = s.Link.PrecisePosition;
                    app.Step(1, Vector2.Zero);
                    var controller = s.Entities.EntityAdapters<SeasonsRosaChaseRoomEntity>().Single(actor => actor.ReadByte(0x42) == 0);
                    var rosa = s.Entities.EntityAdapters<SeasonsRosaChaseRoomEntity>().Single(actor => actor.ReadByte(0x42) == 1);
                    FailIf(controller.ReadByte(0x40) != 0x83 || rosa.ReadByte(0x40) != 0x81 ||
                        s.DisabledObjects != 1 || s.Memory.ReadField("wMenuDisabled") != 1 ||
                        s.Memory.ReadField("wDisableScreenTransitions") != 1 || s.Sound.NativeActiveMusic != 0,
                        $"Rosa's controller and child must use distinct always-updating slots and the native opening locks/music gate: enabled=${controller.ReadByte(0x40):x2}/${rosa.ReadByte(0x40):x2}, mask=${s.DisabledObjects:x2}, menu=${s.Memory.ReadField("wMenuDisabled"):x2}, scroll=${s.Memory.ReadField("wDisableScreenTransitions"):x2}, music=${s.Sound.NativeActiveMusic:x2}.");
                    FailIf(rosa.Npc.Position != (visit == 0 ? new Vector2(176, 72) : new Vector2(80, 40)),
                        "First and repeated chase introductions must retain their different source coordinates.");
                    app.Step(4, Vector2.Down, batched: batched);
                    FailIf(s.Link.PrecisePosition != held, "The Rosa introduction must freeze Link through the real object mask.");
                    int updates = 0;
                    while (!s.Dialogue.IsOpen && updates++ < 600) app.Step(1, Vector2.Zero);
                    FailIf(!s.Dialogue.IsOpen || s.Dialogue.SourceTextId != (visit == 0 ? 0x2600 : 0x2602) ||
                        !s.Save.HasRoomFlag(0, 0xcb, 0x40),
                        "The first/repeated introduction must select TX_2600/TX_2602 and retain room flag $40.");
                    int speed = rosa.ReadByte(0x54) | rosa.ReadByte(0x55) << 8;
                    app.Step(8, Vector2.Right, batched: batched);
                    FailIf(s.Link.PrecisePosition != held || speed == (rosa.ReadByte(0x54) | rosa.ReadByte(0x55) << 8),
                        "Rosa must continue her source bounce while text and Link's input remain frozen.");
                    s.Dialogue.Close();
                    updates = 0;
                    while (s.Entities.EntityAdapters<SeasonsRosaChaseRoomEntity>().Any(actor => actor.ReadByte(0x42) == 1) && updates++ < 400)
                        app.Step(1, Vector2.Zero);
                    FailIf(updates >= 400 || s.DisabledObjects != 0 || s.Memory.ReadField("wMenuDisabled") != 0 ||
                        s.Memory.ReadField("wcc84") != 2 || s.Memory.ReadField("wDisableScreenTransitions") != 0 ||
                        controller.ReadByte(0x7a) != 1 || s.Memory.ReadWramByte(0xcfc0) != 1,
                        "After Rosa leaves $cb, the retained controller must arm scrolling using the live completion signal.");
                }
                // A short actual scroll proves the held controller hands off to a new room's hider.
                s.Link.WarpTo(SeasonsRosaNorthApproach(s));
                int scrollUpdates = 0;
                while (!s.Scrolling && scrollUpdates++ < 90) app.Step(1, Vector2.Up);
                FailIf(!s.Scrolling, "The real $cb collision geometry must allow following Rosa north.");
                app.Step(96, Vector2.Zero, batched: batched);
                FailIf(s.RoomId != 0xbb || s.Scrolling ||
                    !s.Entities.EntityAdapters<SeasonsRosaChaseRoomEntity>().Any(actor => actor.ReadByte(0x42) == 0 && actor.ReadByte(0x46) == 1) ||
                    !s.Entities.EntityAdapters<SeasonsRosaChaseRoomEntity>().Any(actor => actor.ReadByte(0x42) == 1 && actor.ReadByte(0x43) == 1),
                    "The source controller must survive scrolling $cb->$bb and allocate Rosa's second-screen actor.");
                s.DebugTravel(0, 0xb6); app.Step(2, Vector2.Zero, batched: batched);
                FailIf(s.DisabledObjects != 0 || s.Memory.ReadField("wDisableScreenTransitions") != 0 || s.Dialogue.IsOpen,
                    "Cancelling the chase must release its owned control and dialogue.");
                s.DebugTravel(0, 0x9b); app.Step(1, Vector2.Zero);
                FailIf(s.Entities.EntityAdapters<NativeNpcRoomEntity>().Any(actor => !actor.Finished && actor.Npc.Record.Id == 0x27) ||
                    s.Entities.RoomEnemyCount != 2 || !s.Entities.Entities<OctorokCharacter>().Any(actor => actor.Record.SubId == 3),
                    "Before autumn power, Sokra $27:$01 must parse objectData7e4a's Octorok $09:$03 and arrow Moblin, then delete.");
                CheckSeasonsRosaPortalEntry(root, batched);
            }
            finally { root.Free(); }
        }
    }
    private void CheckSeasonsRosaPortalEntry(FrontendValidationRoot root, bool batched)
    {
        var s = root._seasonsOpening!;
        var app = new ApplicationValidationFixture(root);
        s.DebugTravel(0, 0x9b);
        var data = new SeasonsTalkNpcDatabase();
        var stream = NativeObjectStreamDatabase.For(SeasonsGameContent.AssetRoot);
        var source = new NativeObjectStreamReader(stream, s.Memory, () => 0)
            .Read(stream.Root(0, 0xcb)).First(row => row.Id == 0x6c);
        var controller = s.Entities.TryAllocateInteraction(() => new SeasonsRosaChaseRoomEntity(s, data, source, s.Room))!;
        // Native cursor after the fourth screen's final departure signal.
        controller.WriteByte(0x40, 0x83); controller.WriteByte(0x45, 1);
        controller.WriteByte(0x46, 3); controller.WriteByte(0x78, 1); controller.WriteByte(0x79, 0x9b);
        s.Memory.SetField("wcc84", 2); s.Memory.SetField("wcc85", 0x80);
        s.Memory.SetWramByte(0xcfc0, 1);
        var destination = s.Rooms.GetRoom(0, 0x9a);
        Vector2 approach = (from y in Enumerable.Range(1, 15).Select(index => index * 8)
                            let point = new Vector2(5, y)
                            where !s.Collision.Collides(point) && !s.Collision.Collides(new(12, y)) &&
                                destination.GetTerrainInfo(new(152, y)).Collision == 0
                            select point).First();
        app.Step(1, Vector2.Zero);
        s.Link.WarpTo(approach + new Vector2(7, 0));
        int updates = 0;
        while (!s.Scrolling && updates++ < 90) app.Step(1, Vector2.Left);
        FailIf(!s.Scrolling || !s.Transitions.RoomPackTransitionActive,
            "The native fourth-screen west exit must begin $0:9a's room-pack fade.");
        int slot = s.Entities.NativeInteractionSlot(controller);
        bool retainedAtLoad = false;
        app.Step(96, Vector2.Zero, batched: batched, afterUpdate: () =>
        {
            if (controller.Finished) return;
            FailIf(s.Entities.NativeInteractionSlot(controller) != slot || controller.ReadByte(0x40) != 0x83,
                "CUTSCENE_05 must retain Rosa's enabled $83 allocation without recreating its native cursor.");
            if (s.RoomId == 0x9a) retainedAtLoad = true;
        });
        var rosa = s.Entities.EntityAdapters<SeasonsRosaChaseRoomEntity>().SingleOrDefault(actor => actor.ReadByte(0x42) == 1);
        FailIf(!retainedAtLoad || s.RoomId != 0x9a || rosa is null || rosa.ReadByte(0x43) != 4 || !controller.Finished ||
            s.DisabledObjects != 0x81 || s.Memory.ReadField("wMenuDisabled") != 0x80,
            "The $0:9b->$0:9a room-pack fade must retain the controller until it spawns portal-screen Rosa.");
        Vector2 held = s.Link.PrecisePosition;
        app.Step(4, Vector2.Right, batched: batched);
        FailIf(s.Link.PrecisePosition != held, "Rosa's portal-entry script must hold Link through the real input mask.");
        while (s.Entities.EntityAdapters<SeasonsRosaChaseRoomEntity>().Any() && updates++ < 400)
            app.Step(1, Vector2.Zero);
        FailIf(updates >= 400 || !s.Save.HasRoomFlag(0, 0xcb, 0x80) || !s.Save.HasRoomFlag(0, 0x9a, 0x40) ||
            s.Room.GetPackedStorageMetatile(0x33) != 0xc5 || s.DisabledObjects != 0 || s.Memory.ReadField("wMenuDisabled") != 0,
            "Rosa must enter the covered portal at $0:9a:$33, complete the chase and restore input after the actual scroll.");
        s.DebugTravel(0, 0xcb); app.Step(1, Vector2.Zero);
        FailIf(s.Entities.EntityAdapters<SeasonsRosaChaseRoomEntity>().Any(),
            "Completing the portal entry must suppress a new chase on returning to $0:cb.");
    }
    private void ValidateSeasonsRosaFullPoolRom()
    {
        foreach (int step in new[] { 0, 4 })
        {
            var root = StartSeasonsAfterImpa(false);
            try
            {
                var s = root._seasonsOpening!;
                int room = step == 0 ? 0xcb : 0x9a;
                s.Inventory.GiveTreasure(TreasureId.Essence, 0);
                s.DebugTravel(0, room); s.Entities.Clear();
                s.DisabledObjects = 1;
                var data = new SeasonsTalkNpcDatabase();
                var stream = NativeObjectStreamDatabase.For(SeasonsGameContent.AssetRoot);
                var source = new NativeObjectStreamReader(stream, s.Memory, () => 0)
                    .Read(stream.Root(0, 0xcb)).First(row => row.Id == 0x6c);
                var native = new BraceletRom(s.Room, s.Link.Position, 0, s.Inventory.ActiveRing, game: OracleGame.Seasons);
                native.LoadSave(s.Save);
                native.SetNativeByte(0xcc49, 0); native.SetNativeByte(0xcc4c, (byte)room);
                native.SetNativeByte(s.Memory.FieldAddress("wDisabledObjects"), 1);
                SeasonsRosaChaseRoomEntity? controller = null;
                for (int index = 0; index < 14; index++)
                {
                    var actor = s.Entities.TryAllocateInteraction(() => new SeasonsRosaChaseRoomEntity(s, data, source, s.Room))!;
                    int page = (0xd0 + s.Entities.NativeInteractionSlot(actor)) << 8;
                    if (index == 0)
                    {
                        controller = actor;
                        actor.WriteByte(0x40, 0x81);
                        if (step == 4)
                        {
                            actor.WriteByte(0x45, 1); actor.WriteByte(0x78, 1);
                            actor.WriteByte(0x79, 0x9b); actor.WriteByte(0x7a, 1); actor.WriteByte(0x46, 3);
                        }
                    }
                    else { actor.WriteByte(0x40, 1); actor.WriteByte(0x44, 8); actor.WriteByte(0x45, 1); }
                    for (int offset = 0x40; offset < 0x80; offset++) native.SetNativeByte(page + offset, (byte)actor.ReadByte(offset));
                }
                s.Sound.WriteNativeAllocationAlias(0xc03f, 0x12); native.SetNativeByte(0xc03f, 0x12);
                var writes = new List<(int Address, byte Value)>();
                s.Entities.NativeSoundAllocationAliasWritten += (address, value) => writes.Add((address, value));
                native.AdvanceInteractions(s.FrameCounter + 1);
                new ApplicationValidationFixture(root).Step(1, Vector2.Zero);
                int[] addresses = [0xc03f, 0xc040, 0xc041, 0xc042, 0xc04b, 0xc04d];
                FailIf(!writes.SequenceEqual(addresses.Select(address => (address, native.ReadNativeByte(address)))) ||
                    writes[0].Value != 0x92 || writes[1].Value != 0x6c || writes[2].Value != 1 || writes[3].Value != step ||
                    controller!.Finished != (step == 4) || s.Entities.EntityAdapters<SeasonsRosaChaseRoomEntity>().Any(actor => actor.ReadByte(0x42) != 0),
                    $"Rosa step${step:x2}: saturated interaction allocation must preserve the native ordered $e0 sound aliases without creating a hider.");
                foreach (string field in new[] { "wDisabledObjects", "wMenuDisabled", "wcc85", "wDisableScreenTransitions" })
                    FailIf(s.Memory.ReadField(field) != native.ReadNativeByte(s.Memory.FieldAddress(field)),
                        $"Rosa step${step:x2}: saturated allocation changed native {field} ownership.");
            }
            finally { root.Free(); }
        }
    }
    private Vector2 SeasonsRosaNorthApproach(SeasonsOpeningSession session)
    {
        for (int x = 8; x < 160; x += 8)
            if (!session.Collision.Collides(new(x, 12)) && !session.Collision.Collides(new(x, 5))) return new(x, 12);
        throw new InvalidOperationException($"Rosa room $0:{session.RoomId:x2} has no collision-valid northern approach.");
    }
    private void ValidateSeasonsRosaChaseRom()
    {
        foreach (int step in Enumerable.Range(0, 6))
        foreach (int mode in Enumerable.Range(0, step == 1 ? 3 : 2))
        {
            bool alternate = mode == 1, detect = mode == 2;
            var root = StartSeasonsAfterImpa(alternate);
            try
            {
                var s = root._seasonsOpening!;
                int room = new[] { 0xcb, 0xbb, 0xab, 0x9b, 0x9a, 0xbb }[step];
                s.Inventory.GiveTreasure(TreasureId.Essence, 0);
                s.Save.SetRoomFlag(0, 0xcb, 0x40, alternate);
                s.DebugTravel(0, room); s.Entities.Clear();
                s.DisabledObjects = detect ? (byte)0 : (byte)8;
                s.Sound.SelectRoomMusic(0, room, s.Save); s.Sound.CheckPlayRoomMusic();
                var data = new SeasonsTalkNpcDatabase();
                var stream = NativeObjectStreamDatabase.For(SeasonsGameContent.AssetRoot);
                var source = new NativeObjectStreamReader(stream, s.Memory, () => s.Save.GetRoomFlags(0, room))
                    .Read(stream.Root(0, 0xcb)).First(row => row.Id == 0x6c);
                int x = step == 0 ? 80 : step == 1 ? 104 : 88;
                int y = step == 0 ? 40 : 88;
                if (detect)
                {
                    var floor = (from py in Enumerable.Range(0, 16).Select(index => 24 + index * 6)
                                 from px in Enumerable.Range(0, 24).Select(index => 8 + index * 6)
                                 let point = new Vector2(px, py)
                                 let distance = point.DistanceTo(new(x, y))
                                 where distance is >= 12 and <= 28 && !s.Collision.Collides(point)
                                 select point).First();
                    s.Link.WarpTo(floor);
                }
                var rosa = s.Entities.TryAllocateInteraction(() => new SeasonsRosaChaseRoomEntity(s, data,
                    source with { SubId = 1, Var03 = step, X = x, Y = y }, s.Room))!;
                rosa.WriteByte(0x40, 0x81);
                int page = (0xd0 + s.Entities.NativeInteractionSlot(rosa)) << 8;
                var seed = s.Random.CaptureState();
                var probe = new OracleRandom();
                for (int candidate = 0; candidate < 256; candidate++)
                {
                    var selected = seed with { Rng1 = (byte)candidate, Rng2 = 0x0d };
                    probe.RestoreState(selected);
                    if ((probe.Next().Value & 1) != (alternate ? 1 : 0)) continue;
                    s.Random.RestoreState(selected); break;
                }
                seed = s.Random.CaptureState();
                var native = new BraceletRom(s.Room, s.Link.Position, 0, s.Inventory.ActiveRing, game: OracleGame.Seasons);
                native.LoadSave(s.Save);
                native.SetNativeByte(0xcc49, (byte)s.Rooms.ActiveGroup); native.SetNativeByte(0xcc4c, (byte)s.RoomId);
                native.SetNativeByte(0xcc55, unchecked((byte)s.Rooms.CurrentDungeonIndex));
                foreach (string field in new[] { "wScrollMode", "wDisabledObjects", "wMenuDisabled" })
                    native.SetNativeByte(s.Memory.FieldAddress(field), s.Memory.ReadField(field));
                native.SetNativeByte(0xcc62, (byte)s.Sound.NativeRoomMusic);
                native.SetNativeByte(0xff94, seed.Rng1); native.SetNativeByte(0xff95, seed.Rng2);
                native.SetNativeByte(page + 0x40, 0x81); native.SetNativeByte(page + 0x41, 0x6c);
                native.SetNativeByte(page + 0x42, 1); native.SetNativeByte(page + 0x43, (byte)step);
                native.SetNativeByte(page + 0x4b, (byte)y); native.SetNativeByte(page + 0x4d, (byte)x);
                native.ClearSounds(); s.Sound.AttachPlayRequestAudit(); s.Sound.ClearPlayRequestAudit();
                int updates = 0;
                bool sawText = false;
                var app = new ApplicationValidationFixture(root);
                void Compare()
                {
                    updates++;
                    native.Word(0xd00a, (int)(s.Link.PrecisePosition.Y * 256)); native.Word(0xd00c, (int)(s.Link.PrecisePosition.X * 256));
                    native.AdvanceParts(); native.AdvanceInteractions(s.FrameCounter);
                    foreach (int offset in new[] { 0x40, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49, 0x4a, 0x4b, 0x4c, 0x4d, 0x4e, 0x4f, 0x50, 0x54, 0x55, 0x6d, 0x7b })
                        FailIf(s.Memory.ReadWramByte(page + offset) != native.ReadNativeByte(page + offset),
                            $"Rosa step${step:x2}/alternate{alternate}/update{updates}/byte${offset:x2}: port${s.Memory.ReadWramByte(page + offset):x2}, ROM${native.ReadNativeByte(page + offset):x2}; command={rosa.Script?.CurrentCommandIndex}, flag${s.Save.GetRoomFlags(0,0xcb):x2}/${native.ReadNativeByte(0xc7cb):x2}, group${native.ReadNativeByte(0xcc49):x2}, room${native.ReadNativeByte(0xcc4c):x2}.");
                    foreach (string field in new[] { "wDisabledObjects", "wMenuDisabled", "wcc84", "wcc85" })
                        FailIf(s.Memory.ReadField(field) != native.ReadNativeByte(s.Memory.FieldAddress(field)),
                            $"Rosa step${step:x2}/update{updates}: shared {field} differs.");
                    FailIf(s.Memory.ReadWramByte(0xcfc0) != native.ReadNativeByte(0xcfc0) ||
                        s.Random.Calls - seed.Calls != native.RandomCalls,
                        $"Rosa step${step:x2}/update{updates}: completion signal or ordered RNG consumption differs.");
                    if (!rosa.Finished)
                        FailIf(rosa.Npc.CurrentAnimationCounter != native.ReadNativeByte(page + 0x60) ||
                            rosa.Npc.CurrentAnimationParameter != native.ReadNativeByte(page + 0x61) ||
                            rosa.Npc.ZIndex != ObjectDrawPriority.BehindLinkZIndex,
                            $"Rosa step${step:x2}/update{updates}: native animation cursor/parameter or fixed draw priority differs.");
                    foreach (int cue in new[] { 0x4d, 0x8d })
                        FailIf(s.Sound.PlayRequestsFor(cue) != native.Sounds.Count(sound => sound == cue),
                            $"Rosa step${step:x2}/update{updates}: cue ${cue:x2} differs.");
                    if (s.Dialogue.IsOpen)
                    {
                        sawText = true;
                        int expectedText = step == 0 ? alternate ? 0x2602 : 0x2600 : 0x2601;
                        FailIf(s.Dialogue.SourceTextId != expectedText || native.ReadNativeByte(0xcba2) != (expectedText & 255),
                            $"Rosa step${step:x2}: expected source TX_{expectedText:x4}.");
                    }
                }
                while (!rosa.Finished && updates < 3000)
                {
                    if (s.Dialogue.IsOpen)
                    { app.Step(4, Vector2.Zero, batched: alternate, afterUpdate: Compare); s.Dialogue.Close(); native.SetNativeByte(0xcba0, 0); }
                    else app.Step(alternate ? 2 : 1, Vector2.Zero, batched: alternate, afterUpdate: Compare);
                }
                FailIf(!rosa.Finished || updates >= 3000 || sawText != (step is 0 or 5 || detect),
                    $"Rosa step${step:x2}/alternate{alternate}: native script did not reach its expected completion/text boundary.");
                if (step == 4)
                {
                    FailIf(!s.Save.HasRoomFlag(0, 0xcb, 0x80) || !s.Save.HasRoomFlag(0, 0x9a, 0x40) ||
                        s.Room.GetPackedStorageMetatile(0x33) != 0xc5 || s.Memory.ReadField("wcc84") != 0,
                        "Rosa's portal-screen script must complete $cb, write the covered portal at $9a:$33, and clear the chase mode.");
                    s.DebugTravel(0, 0x9a); app.Step(1, Vector2.Zero);
                    var portal = s.Entities.EntityAdapters<SeasonsPortalSpawnerRoomEntity>().Single();
                    FailIf(portal.Npc.Visible || portal.ReadByte(0x44) != 1,
                        "The real $9a portal producer must remain hidden while tile $c5 covers its warp.");
                    s.Room.SetPositionTileAndCollision(new(56, 56), 0xe6, null, (long)s.AnimationTicks);
                    app.Step(1, Vector2.Zero);
                    FailIf(!portal.Npc.Visible || portal.ReadByte(0x44) != 2 || !s.Save.HasRoomFlag(0, 0x9a, 8),
                        "Uncovering tile $e6 must activate the placed portal and publish its native discovered flag.");
                }
            }
            finally { root.Free(); }
        }
        ValidateSeasonsRosaFullPoolRom();
    }
}
