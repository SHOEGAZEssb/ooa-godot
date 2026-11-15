namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsDanceRom()
    {
        ValidateSeasonsDanceFinalTurnPixelsRom();
        foreach (bool batched in new[] { false, true })
        foreach (var scenario in new[] { (Difficulty:0, Failure:0), (Difficulty:8, Failure:0),
            (Difficulty:0, Failure:1), (Difficulty:0, Failure:2), (Difficulty:0, Failure:3) })
        {
            var root = StartSeasonsAfterImpa(batched);
            try
            {
                var s = root._seasonsOpening!;
                var app = new ApplicationValidationFixture(root);
                s.DebugTravel(3, 0x95); app.Step(2, Vector2.Zero, batched: batched);
                s.Save.WriteField("wNumTimesPlayedSubrosianDance", (byte)scenario.Difficulty);
                var leader = s.Entities.EntityAdapters<SeasonsDanceRoomEntity>().Single(actor => actor.ReadByte(0x42) == 1);
                var native = new BraceletRom(s.Room, s.Link.Position, 2, s.Inventory.ActiveRing, game: OracleGame.Seasons);
                native.LoadSave(s.Save);
                native.SetNativeByte(0xcc49, 3); native.SetNativeByte(0xcc4c, 0x95);
                native.SetNativeByte(0xd240, 1); native.SetNativeByte(0xd241, 0x6a);
                native.AdvanceInteractions(s.FrameCounter); native.AdvanceInteractions(s.FrameCounter);
                int page = (0xd0 + s.Entities.NativeInteractionSlot(leader)) << 8;
                FailIf(native.ReadNativeByte(page + 0x41) != 0x6a || native.ReadNativeByte(page + 0x42) != 1,
                    "Native $6a:$00 must reserve its slot while objectData7e6c allocates the dance leader.");
                // Declared handoff after the tutorial's white fade: both native Link and leader start state0.
                leader.WriteByte(0x44, 4); leader.WriteByte(0x45, 0);
                native.SetNativeByte(page + 0x44, 4); native.SetNativeByte(page + 0x45, 0);
                var data = new SeasonsTalkNpcDatabase();
                s.Link.RequestSubrosianDance(data.NativeTable("dance-grid"), data.NativeTable("dance-link-animations"));
                for (int offset = 2; offset <= 5; offset++) native.SetNativeByte(0xd000 + offset, 0);
                native.SetNativeByte(s.Memory.FieldAddress("wLinkIDOverride"), 0x81);
                s.Memory.SetWramByte(0xd009, 0); native.SetNativeByte(0xd009, 0);
                s.DisabledObjects = 0x91; native.SetNativeByte(s.Memory.FieldAddress("wDisabledObjects"), 0x91);
                s.Memory.SetWramByte(0xcfda, 1); native.SetNativeByte(0xcfda, 1);
                var seed = s.Random.CaptureState(); native.SetNativeByte(0xff94, seed.Rng1); native.SetNativeByte(0xff95, seed.Rng2);
                var sounds = s.Sound.AttachPlayRequestAudit(); native.ClearSounds();
                native.SetNativeByte(s.Memory.FieldAddress("wActiveMusic"), (byte)s.Sound.NativeActiveMusic);
                int updates = 0;
                void Step(int count, int keys)
                {
                    app.Step(count, DanceDirection(keys), MenuRomActions(keys), MenuRomActions(keys), batched: batched, afterUpdate: () =>
                    {
                        native.UpdateLinkItems(s.FrameCounter, keys, interactions: true);
                        string context = $"Subrosian dance difficulty${scenario.Difficulty:x2}/failure{scenario.Failure}/update{++updates}";
                        foreach (int address in Enumerable.Range(0xcfc0, 32))
                            FailIf(s.Memory.ReadWramByte(address) != native.ReadNativeByte(address),
                                context + $": WRAM${address:x4} port${s.Memory.ReadWramByte(address):x2}/ROM${native.ReadNativeByte(address):x2}.");
                        foreach (var actor in s.Entities.EntityAdapters<SeasonsDanceRoomEntity>())
                        {
                            int at = (0xd0 + s.Entities.NativeInteractionSlot(actor)) << 8;
                            foreach (int offset in new[] { 0x40, 0x42, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49,
                                0x4a, 0x4b, 0x4c, 0x4d, 0x4e, 0x4f, 0x50, 0x54, 0x55, 0x78, 0x79, 0x7a, 0x7b })
                                FailIf(actor.ReadByte(offset) != native.ReadNativeByte(at + offset),
                                    context + $": slot${at >> 8:x2}/byte${offset:x2} port${actor.ReadByte(offset):x2}/ROM${native.ReadNativeByte(at + offset):x2}.");
                            FailIf(actor.Npc.CurrentAnimationCounter != native.ReadNativeByte(at + 0x60) ||
                                actor.Npc.CurrentAnimationParameter != native.ReadNativeByte(at + 0x61),
                                context + $": slot${at >> 8:x2} animation clock/parameter differs.");
                        }
                        foreach (int offset in new[] { 0, 1, 4, 8, 9, 0x10, 0x20, 0x21, 0x30, 0x31, 0x37, 0x38, 0x39 })
                            FailIf(s.Memory.ReadWramByte(0xd000 + offset) != native.ReadNativeByte(0xd000 + offset),
                                context + $": Link byte${offset:x2} port${s.Memory.ReadWramByte(0xd000 + offset):x2}/ROM${native.ReadNativeByte(0xd000 + offset):x2}.");
                        FailIf(s.Link.PrecisePosition != new Vector2(native.Word(0xd00c) / 256f, native.Word(0xd00a) / 256f),
                            context + $": Link fixed position port{s.Link.PrecisePosition}/ROM{native.Word(0xd00c) / 256f},{native.Word(0xd00a) / 256f}.");
                        var now = s.Random.CaptureState();
                        FailIf(now.Rng1 != native.ReadNativeByte(0xff94) || now.Rng2 != native.ReadNativeByte(0xff95) ||
                            now.Calls - seed.Calls != native.RandomCalls, context + ": ordered RNG differs.");
                        FailIf(!sounds.Requests.SequenceEqual(native.Sounds), context + ": ordered music/step/result cues differ.");
                        FailIf(s.Sound.NativeActiveMusic != native.ReadNativeByte(s.Memory.FieldAddress("wActiveMusic")),
                            context + ": playSound must retain the native room-music selection byte.");
                    });
                }
                Step(1, 0);
                if (scenario.Failure == 3)
                {
                    // A physical overlap exercises the source's shared collision signal and next-visit loss.
                    var dancer = s.Entities.EntityAdapters<SeasonsDanceRoomEntity>().First(actor => actor.ReadByte(0x42) == 2);
                    s.Link.SetScriptedPosition(dancer.Npc.Position);
                    native.Word(0xd00c, (int)dancer.Npc.Position.X * 256); native.Word(0xd00a, (int)dancer.Npc.Position.Y * 256);
                }
                for (int guard = 0; !s.Dialogue.IsOpen && guard < 12000; guard++)
                {
                    int count = native.ReadNativeByte(0xcfd9), keys = 0;
                    if (scenario.Failure != 2 && native.ReadNativeByte(page + 0x45) == 3 && count < 3 &&
                        native.ReadNativeByte(0xcfcb) > count && native.ReadNativeByte(0xd004) == 1 &&
                        native.ReadNativeByte(s.Memory.FieldAddress("wGameKeysPressed")) == 0)
                    {
                        int move = native.ReadNativeByte(0xcfc8 + count);
                        if (scenario.Failure == 1) move = (move + 1) % 3;
                        keys = move == 0 ? 0x10 : move == 1 ? 0x20 : 1;
                    }
                    // Neutral demonstration updates can share a host frame; input boundaries retain single visits.
                    int batch = batched && keys == 0 && native.ReadNativeByte(page + 0x45) == 2 &&
                        native.ReadNativeByte(page + 0x46) > 4 ? 3 : 1;
                    Step(batch, keys);
                }
                FailIf(!s.Dialogue.IsOpen || s.Dialogue.SourceTextId != (scenario.Failure == 0 ? 0x010a : 0x0104),
                    "Independent native dance updates must reach their expected win, wrong-step, timeout or collision branch.");
            }
            finally { root.Free(); }
        }
    }

    private void ValidateSeasonsDanceFinalTurnPixelsRom()
    {
        var root = StartSeasonsAfterImpa(false);
        try
        {
            var s = root._seasonsOpening!;
            s.DebugTravel(3, 0x95);
            new ApplicationValidationFixture(root).Step(2, Vector2.Zero);
            var leader = s.Entities.EntityAdapters<SeasonsDanceRoomEntity>().Single(actor => actor.ReadByte(0x42) == 1);
            // interactionAnimation51e0d: five 2-update turns, then OAM $10 held for $7f updates.
            leader.SetAnimation(4); leader.Npc.AdvanceAnimationUpdates(10);
            FailIf(leader.Npc.CurrentAnimationFrame != 5 || leader.Npc.CurrentAnimationOpaquePixels == 0,
                "Dance instructor's held turning pose must show the Subrosian graphics at tiles $30/$32.");
            var native = new BraceletRom(s.Room, s.Link.Position, 2, s.Inventory.ActiveRing, game: OracleGame.Seasons);
            var graphics = new LinkCollisionRom(game: OracleGame.Seasons);
            graphics.Call(0x04e7, bank: 0, accumulator: 0x0f);
            // Normalize native object-GFX headers $07/$08 to adjacent $20-tile slots at $8000/$8200.
            graphics.Call(0x064e, speed: 31, angle: 0x1a, bank: 0, objectPage: 0x80, objectOffset: 0, dataAddress: 0x6480, restoresBank: false);
            graphics.Call(0x064e, speed: 31, angle: 0x69, bank: 0, objectPage: 0x82, objectOffset: 0, dataAddress: 0x6e78, restoresBank: false);
            const int at = 0xd340;
            native.SetNativeByte(at + 0x1c, 2);
            native.SetNativeByte(at + 0x1e, 0x23); native.SetNativeByte(at + 0x1f, 0x42); // interactionOamData4c223.
            var frame = SomariaPrivate<List<NpcCharacterAnimationFrame>>(leader.Npc, "_scriptAnimation")[5];
            CompareSeasonsInteractionPixels(native, graphics, frame.Texture, frame.Offset, at, "INTERAC$6a:$01 animation$04/frame5", tileBase: 0);
        }
        finally { root.Free(); }
    }
}
