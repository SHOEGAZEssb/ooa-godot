using System.Text;
using System.Threading.Tasks;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsOpeningAssets()
    {
        var data = new SeasonsOpeningData();
        var save = OracleSaveData.CreateStandardGame(OracleGame.Seasons);
        byte[] bytes = save.Serialize();
        foreach (int slot in Enumerable.Range(0, 3))
            FailIf(OracleSaveStore.Save(save, AgesGameContent.SaveSlotPath(slot)).Success,
                $"A Seasons image must not overwrite Ages slot ${slot:x2}.");
        FailIf(save.Game != OracleGame.Seasons || save.RespawnRoom != 0xa7 || save.RespawnX != 0x48 || save.RespawnY != 0x38 || save.RespawnFacing != 2 ||
            Encoding.ASCII.GetString(bytes, 2, 8) != "Z11216-0" || OracleSaveData.TryDeserialize(bytes, OracleGame.Ages, out _) ||
            !OracleSaveData.TryDeserialize(bytes, OracleGame.Seasons, out _),
            "Seasons new-file initialization must retain its own checkpoint, signature and save boundary.");
        var inventory = new InventoryState(new TreasureDatabase(SeasonsGameContent.AssetRoot), save, runtimeState: new(OracleGame.Seasons));
        var memory = new OracleRuntimeState(OracleGame.Seasons);
        memory.SetField("wDisabledObjects", 0x81);
        memory.SetField("wActiveTilePos", 0x44);
        FailIf(memory.ReadWramByte(0xcca4) != 0x81 || memory.ReadWramByte(0xcc8a) != 0 || memory.ReadWramByte(0xccb3) != 0x44,
            "Shared typed WRAM fields must bind to Seasons addresses while raw script reads retain physical addresses.");
        FailIf(!inventory.HasTreasure(TreasureId.Punch) || inventory.MaxHealthQuarters != 12 || inventory.HealthQuarters != 12,
            "Seasons inventory must resolve common treasure/health symbols to its own saved bytes.");
        var itemSave = OracleSaveData.CreateStandardGame(OracleGame.Seasons);
        // include/wram.s: Seasons' $c6b0-$c6b4 and shared seed selector $c6bf.
        itemSave.WriteWramByte(0xc6b0, 0x09);
        itemSave.WriteWramByte(0xc6b1, 2);
        itemSave.WriteWramByte(0xc6b2, 1);
        itemSave.WriteWramByte(0xc6b3, 2);
        itemSave.WriteWramByte(0xc6b4, 2);
        itemSave.WriteWramByte(0xc6bf, 3);
        var itemInventory = new InventoryState(TreasureDatabase.For(SeasonsGameContent.AssetRoot), itemSave);
        FailIf(itemInventory.ObtainedSeasons != 0x09 || itemInventory.BoomerangLevel != 2 ||
            itemInventory.MagnetGlovePolarity != 1 || itemInventory.SlingshotLevel != 2 ||
            itemInventory.FeatherLevel != 2 || itemInventory.SlingshotSelectedSeeds != 3,
            "Shared inventory must expose the Seasons scalar fields rather than Ages-only zero placeholders.");
        itemInventory.SelectSlingshotSeeds(4);
        FailIf(itemSave.ReadWramByte(0xc6bf) != 4 || itemSave.ReadWramByte(0xc6b0) != 0x09 ||
            itemSave.ReadWramByte(0xc6b1) != 2 || itemSave.ReadWramByte(0xc6b2) != 1 ||
            itemSave.ReadWramByte(0xc6b3) != 2 || itemSave.ReadWramByte(0xc6b4) != 2,
            "Changing Seasons seed selection must preserve its native weapon levels, season flags and magnet polarity.");
        // include/wram.s and both games' fileManagement.s use distinct fields.
        var fields = OracleSaveData.CreateStandardGame(OracleGame.Seasons);
        fields.NameChild("Pip");
        fields.MarkRingSecretGenerated();
        for (int index = 0; index < 0x12; index++) fields.AddGashaMaturity(0x100 - 1);
        fields.AddGashaMaturity(0x46);
        fields.SetGashaSpotPlanted(15, true);
        FailIf(!fields.ChildNamed || (fields.ReadWramByte(0xc6dc) & 1) == 0 ||
            fields.ReadWramByte(0xc6e2) != 0 || fields.GashaMaturity != 0x1234 ||
            fields.ReadWramByte(0xc65c) != 0x34 || fields.ReadWramByte(0xc65d) != 0x12 ||
            fields.ReadWramByte(0xc64b) != 0x80 || !fields.HasGlobalFlag(0x31) || fields.HasGlobalFlag(0x28),
            "Seasons child and Gasha fields must use $c6dc/$c65c-$c65d/$c64b, without writing the Ages locations.");
        var shadow = TerrainShadow.Load(SeasonsGameContent.AssetRoot);
        var terrain = new LinkTerrainEffectDatabase(SeasonsGameContent.AssetRoot);
        FailIf(shadow.Offset != new Vector2(-4, 3) || shadow.Texture.GetSize() != new Vector2(8, 16) ||
            terrain.FrameFor(0xf9, Vector2.Zero, 0)?.Kind != LinkTerrainEffectKind.Grass ||
            terrain.FrameFor(0xfa, Vector2.Zero, 0)?.Kind != LinkTerrainEffectKind.Puddle ||
            terrain.FrameFor(0xfc, Vector2.Zero, 0)?.Kind != LinkTerrainEffectKind.Puddle ||
            !TopDownAirDatabase.For(SeasonsGameContent.AssetRoot).Parameters.AnimationPhaseDurations.SequenceEqual(new[] { 9, 9, 6 }),
            "Seasons terrain IDs, raw shadow OAM and gameplay Link's jump phases must come from its own presentation data.");
        FailIf(data.LinkSprites.Record.TextId != 0x0c16 || data.LinkSprites.Record.InitialWaitFrames != 300 ||
            data.LinkSprites.Record.LinkSummonedFlag != 0x2a || data.LinkSprites.Record.PregameIntroDoneFlag != 0x21 ||
            !data.Text(0x0c16).Contains("Accept our\nquest, hero!"), "Seasons pregame lost its source text, flags or $012c wait.");
        var troupe = data.TroupePlacements();
        FailIf(data.CastleLightning.Length != 13 || data.CastleLightning[0] != (60,0) ||
            data.CastleLightning[4] != (120,0) || data.CastleLightning[11] != (12,2),
            "INTERAC_88 lost the source flash sequence or its $78 pause.");
        FailIf(troupe.Count != 7 || troupe[0] != (0x4e,0,new Vector2(0x18,0x29)) ||
            troupe[6] != (0x4e,6,new Vector2(0x48,0x10)), "Seasons troupe placement order/coordinates differ from objectData7e4e.");
        foreach (int room in new[] { 0xa7,0x97,0x98,0xb6,0xb8,0xc6,0xc8 })
        foreach (int season in Enumerable.Range(0,4))
        {
            OracleRoomData loaded = data.World.LoadRoom(room, season);
            FailIf(loaded.Width != 160 || loaded.Height != 128 || loaded.Texture.GetSize() != new Vector2(160,128),
                $"Seasons opening room $00:${room:x2} season ${season:x2} lost its source geometry.");
        }
        foreach (int room in Enumerable.Range(0, 256))
        foreach (int season in Enumerable.Range(0, 4))
        {
            OracleRoomData loaded = data.World.LoadRoom(room, season);
            FailIf(loaded.Width != 160 || loaded.Height != 128,
                $"Seasons overworld room $00:${room:x2} must retain its 10-by-8 layout.");
        }
        var content = new SeasonsRoomContent(data.World);
        OracleRoomData camp = content.LoadRoom(0, 0x98, save, memory, 0, 0);
        byte[] campMappings = camp.CaptureBackgroundMappings(true);
        FailIf(camp.GetPackedStorageMetatile(0x14) != 0 || camp.GetPackedStorageMetatile(0x24) != 0 ||
            campMappings[0x10a * 2] != 0x30 || campMappings[0x10a * 2 + 1] != 4 ||
            campMappings[0x12a * 2] != 0x31 || campMappings[0x10b * 2 + 1] != 0x24 ||
            camp.GetTerrainInfo(new(0x58, 0x48)).Collision != 0x0f ||
            camp.GetTerrainInfo(new(0x58, 0x68)).Collision != 5,
            "roomGfxChanges.s must install the campfire wood at $10a/$12a, mirrored half, roadblocks and partial collision $65.");
        save.SetRoomFlag(0, 0x98, 0x40);
        camp = content.LoadRoom(0, 0x98, save, memory, 0, 0);
        FailIf(camp.CaptureBackgroundMappings(true)[0x10a * 2] == 0x30 ||
            camp.GetTerrainInfo(new(0x58, 0x48)).Collision == 0x0f,
            "The destroyed troupe must clear its visual and collision overlays on re-entry after room flag $40.");
        foreach (var (id, subid) in new[] { (0x4e,0),(0x4e,4),(0x4e,5),(0x4e,6),(0x4e,7),(0x4f,0),(0x4f,1),(0x4f,2),(0x4f,3),(0x4f,4),(0x9d,0),(0x9f,0),(0xa5,9),(0x27,0) })
        {
            var actor = data.CreateActor(id, subid, Vector2.Zero);
            try { FailIf(actor.CurrentAnimationOpaquePixels == 0, $"Seasons opening actor ${id:x2}:${subid:x2} has no visible source pixels."); }
            finally { actor.Free(); }
        }
        data.World.LoadRoom(0x98, 0);
        // Counts from the source PNG cells with invert:false, independently of actors.tsv.
        foreach (var (subid, animation, opaque) in new[] { (2, 3, 104), (3, 6, 100), (0, 0x0e, 400) })
        {
            var actor = data.CreateActor(0x4f, subid, Vector2.Zero);
            try
            {
                actor.SetScriptAnimation(data.Animation(0x4f, animation));
                FailIf(actor.CurrentAnimationOpaquePixels != opaque,
                    $"Din imprisonment ${subid:x2} animation ${animation:x2} lost its native shade-zero transparency, including mixed-polarity appended crystal graphics.");
            }
            finally { actor.Free(); }
        }
        FailIf(data.World.Palettes.Resolve(2, 0) != new Color(1,1,0x0e / 31f) || data.World.Palettes.Resolve(2, 1) != new Color(0x0e / 31f,0x1c / 31f,2 / 31f) ||
            data.World.SpritePalettes[0, 3] != new Color(1,0x1a / 31f,0x11 / 31f),
            "Seasons spring PALH_10 and standard OBJ palettes lost their source RGB5 colors.");
    }

    private async Task ValidateSeasonsOpeningFlow()
    {
        byte[]? baseline = null;
        int baselineRng = 0;
        string[]? baselineTrace = null;
        foreach (bool batched in new[] { false, true })
        {
            var root = new FrontendValidationRoot();
            SubViewport? captureViewport = null;
            if (!string.IsNullOrEmpty(OS.GetEnvironment("OOA_SEASONS_SCREENSHOT_OUTPUT")))
            {
                captureViewport=new() {Size=new(160,144),Disable3D=true,
                    RenderTargetUpdateMode=SubViewport.UpdateMode.Always,
                    CanvasItemDefaultTextureFilter=Viewport.DefaultCanvasItemTextureFilter.Nearest};
                AddChild(captureViewport);captureViewport.AddChild(root);
            }
            else AddChild(root);
            try
            {
                GameSelectScreen selector = root.InitializeGameSelect();
                selector.RequestPointerSelection(OracleGame.Seasons, true); root.Step(1, false);
                root.CompleteBootLoading();
                StartSeasonsFileForValidation(root, batched);
                var session = root._seasonsOpening ?? throw new InvalidOperationException("Seasons selection did not start its new game.");
                // Let Godot publish the new viewport/camera before inspecting
                // canvas transforms during many manually scheduled updates.
                var preparedFrame=session.FrameCounter;
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                FailIf(session.FrameCounter!=preparedFrame,"Viewport preparation must not advance controlled Seasons gameplay.");
                FailIf(selector.IsActive || session.Phase != SeasonsOpeningPhase.Pregame || root.FileSlotExists(0, OracleGame.Ages),
                    "Seasons must begin its own pregame without touching an Ages file slot.");
                int previousPlaytime = session.Save.ReadField("wPlaytimeCounter");
                var application = new ApplicationValidationFixture(root, () =>
                {
                    int playtime = session.Save.ReadField("wPlaytimeCounter");
                    if (playtime == previousPlaytime) return;
                    FailIf(playtime != ((previousPlaytime + 1) & 0xff) || session.FrameCounter != playtime,
                        "Seasons mainThread must publish playtime before native intro, scroll and normal object phases, once per original update.");
                    previousPlaytime = playtime;
                });
                int frontendRng = session.Random.Calls;
                FailIf(frontendRng != 34, "Seasons title must consume its shared RNG before the opening; boot preparation must not.");
                int chunk = batched ? 4 : 1;
                for (int update = 0; update < 2400; update += chunk)
                {
                    application.Step(chunk, Vector2.Zero, ["attack"], update % 4 == 0 ? ["attack"] : [], batched);
                    if (!batched && update == 400) await CaptureSeasonsOpeningScreen("pregame",root.GetViewport());
                    if (!batched && session.Phase == SeasonsOpeningPhase.Forest && session.Actors.Any(actor => actor is { Id: 0xa5 } && actor.Node.Position.Y is > 20 and < 48))
                        await CaptureSeasonsOpeningScreen("forest",root.GetViewport());
                }
                FailIf(session.Phase != SeasonsOpeningPhase.Troupe, "Seasons summoning/discovery failed to reach playable room $97.");
                application.Step(40, Vector2.Zero, batched: batched);
                FailIf(session.DisabledObjects != 0 || session.Link.CutsceneControlled || session.Link.Position != new Vector2(0x48,0x48),
                    "Forest warp $83 with packed position $44 must place Link directly, facing up, without an entrance walk.");
                WalkSeasons(application, session, point => point.X == 154 && point.Y == 0x38);
                application.Step(2, Vector2.Right);
                FailIf(!session.Scrolling || session.RoomId != 0x98, "The troupe must be reachable through room $97's actual east boundary.");
                int rngBeforeScroll = session.Random.Calls;
                application.Step(47, Vector2.Zero, batched: batched);
                FailIf(session.Scrolling || session.Random.Calls != rngBeforeScroll, "Destination actors and placement RNG must remain frozen during scrolling.");
                application.Step(4, Vector2.Zero);
                if (!batched) await CaptureSeasonsOpeningScreen("troupe",root.GetViewport());
                foreach (int subid in new[] { 0,1,2,3,4,6 })
                {
                    TalkSeasons(application, session, subid);
                    int bit = subid == 6 ? 0x20 : 1 << subid;
                    FailIf((session.Signal(3) & bit) == 0, $"Troupe actor $4e:${subid:x2} did not publish its source conversation bit.");
                }
                FailIf(session.Signal(3) != 0x3f || session.Phase != SeasonsOpeningPhase.Troupe,
                    "All six initial conversations must unlock a separate final conversation with Din.");
                TalkSeasons(application, session, 6);
                var trace = new List<string>();
                int danceUpdates = 0, previousRng = session.Random.Calls;
                bool quakeWithoutExplosions = false;
                int previousLightningBits = 0, restoredLightningBits = 0;
                SeasonsOpeningPhase preceding = session.Phase;
                void ObservePhase()
                {
                    if (session.Phase == SeasonsOpeningPhase.Capture)
                    {
                        int[][] destruction = [[0x65,0x75],[0x07,0x08,0x18],[0x78],[0x66,0x67,0x76,0x77]];
                        for (int bit = 0; bit < 4; bit++)
                        {
                            if ((previousLightningBits & (1 << bit)) == 0) continue;
                            byte[] mappings = session.Room.CaptureBackgroundMappings(true);
                            foreach (int position in destruction[bit])
                            {
                                byte expectedTile = position is 0x65 or 0x07 or 0x08 or 0x18 ? (byte)4 : (byte)0x12;
                                byte[] expected = expectedTile == 4 ? [0x80,0xa3,0xa3,0x80,0x0d,0x2d,0x0d,0x0d] :
                                    [0xdb,0xdb,0xdb,0xdb,0x4c,0x4c,0x4c,0x4c];
                                FailIf(session.Room.GetPackedStorageMetatile((byte)position) != expectedTile,
                                    $"Lightning destruction at ${position:x2} must retain the source underlying tile ${expectedTile:x2}.");
                                for (int quarter = 0; quarter < 4; quarter++)
                                {
                                    int subtile = (position >> 4) * 64 + (position & 15) * 2 + (quarter >> 1) * 32 + (quarter & 1);
                                    FailIf(mappings[subtile * 2] != expected[quarter] || mappings[subtile * 2 + 1] != expected[quarter + 4],
                                        $"Lightning destruction at ${position:x2} quarter ${quarter:x1} must restore terrain mappings after removing scenery.");
                                }
                                if ((restoredLightningBits & (1 << bit)) == 0)
                                {
                                    using Image image = session.Room.Texture.GetImage();
                                    byte[] pixels = image.GetData();
                                    int reference = expectedTile == 4 ? 0x40 : 0x58;
                                    for (int y = 0; y < 16; y++)
                                    {
                                        int actual = (((position >> 4) * 16 + y) * 160 + (position & 15) * 16) * 4;
                                        int referenceOffset = (((reference >> 4) * 16 + y) * 160 + (reference & 15) * 16) * 4;
                                        FailIf(!pixels.AsSpan(actual, 64).SequenceEqual(pixels.AsSpan(referenceOffset, 64)),
                                            $"Lightning destruction at ${position:x2} must render the same ground pixels as unaffected source terrain ${reference:x2}.");
                                    }
                                }
                            }
                            restoredLightningBits |= 1 << bit;
                        }
                        previousLightningBits = session.Signal(2);
                    }
                    if (session.NativeScreen.Visible && !session.NativeScreen.Dance)
                    foreach (var actor in session.Actors.Where(actor => actor.Id == 0x77 && actor.Initialized))
                    {
                        int minY = actor.Subid == 0 ? 8 : -8;
                        int priority = actor.Subid == 0 ? 9 : 8;
                        FailIf(actor.Node.GetGlobalTransformWithCanvas().Origin.Y + actor.Node.CurrentAnimationOffset.Y !=
                            actor.Node.Position.Y - session.NativeScreen.CameraY + minY || actor.Node.ZIndex != priority,
                            $"INTERAC_77 must retain its $82/$83 buckets and native OAM Y origin: " +
                            $"screenY={actor.Node.GetGlobalTransformWithCanvas().Origin.Y + actor.Node.CurrentAnimationOffset.Y}, " +
                            $"expected={actor.Node.Position.Y - session.NativeScreen.CameraY + minY}, priority={actor.Node.ZIndex}/{priority}, " +
                            $"camera={session.RoomCamera.Position}, current={session.RoomCamera.IsCurrent()}, " +
                            $"enabled={session.RoomCamera.Enabled}, tree={session.RoomCamera.IsInsideTree()}, " +
                            $"visible={session.WorldRoot.IsVisibleInTree()}, batch={batched}.");
                    }
                    if (session.NativeScreen.Visible && session.NativeScreen.Dance && !session.Fading) danceUpdates++;
                    bool quake = session.Phase == SeasonsOpeningPhase.TempleSinking && session.Signal(3) == 0 &&
                        session.Actors.Count(actor => actor is { Id: 0x77 }) == 2 && !session.Actors.Any(actor => actor is { Id: 0x86 });
                    if (quake && quakeWithoutExplosions)
                        FailIf(session.Random.Calls - previousRng != 2, "Temple quake must consume one SCY and one WINY random sample per original update.");
                    previousRng = session.Random.Calls; quakeWithoutExplosions = quake;
                    if (session.Phase == preceding) return;
                    preceding = session.Phase;
                    trace.Add($"{application.UpdateCount}:{session.Phase}:${session.Signal(0):x2}:{session.Random.Calls}");
                }
                for (int update = 0; update < 12000; update += chunk)
                {
                    application.Step(chunk, Vector2.Zero, ["attack"], update % 4 == 0 ? ["attack"] : [], batched, ObservePhase);
                    if (!batched)
                    {
                        var screen = session.NativeScreen;
                        if (screen.Visible && screen.Dance && update > 40) await CaptureSeasonsOpeningScreen("dance",root.GetViewport());
                        if (session.Phase == SeasonsOpeningPhase.Capture && session.Signal(0) == 5) await CaptureSeasonsOpeningScreen("tornado",root.GetViewport());
                        if (session.Phase == SeasonsOpeningPhase.Capture && session.LastText == 0x0c08 && session.Dialogue.IsPageComplete) await CaptureSeasonsOpeningScreen("lightning-cleared",root.GetViewport());
                        if (session.Phase == SeasonsOpeningPhase.Imprisonment && session.Signal(0) == 0x0a && session.Dialogue.IsPageComplete) await CaptureSeasonsOpeningScreen("onox",root.GetViewport());
                        if (session.Phase == SeasonsOpeningPhase.Imprisonment && session.Signal(0) == 0x0d && !screen.CastleCleared && !session.Dialogue.IsOpen && !session.Fading) await CaptureSeasonsOpeningScreen("crystal",root.GetViewport());
                        if (session.Phase == SeasonsOpeningPhase.TempleSinking && session.Actors.Any(actor => actor is { Id: 0x86 }) && !session.Fading) await CaptureSeasonsOpeningScreen("temple",root.GetViewport());
                        if (session.Phase == SeasonsOpeningPhase.TempleSinking && screen.WindowY > 85 && session.Signal(3) == 0) await CaptureSeasonsOpeningScreen("temple-sinking",root.GetViewport());
                        if (session.Phase == SeasonsOpeningPhase.Aftermath && session.Dialogue.IsPageComplete) await CaptureSeasonsOpeningScreen("impa",root.GetViewport());
                    }
                }
                FailIf(danceUpdates is < 360 or > 363, $"Din's $0258 close-up must reject A until its counter is below $00f0; observed {danceUpdates} active updates.");
                FailIf(restoredLightningBits != 0x0f, "The opening must verify all four lightning destruction lists through actual PART updates.");
                FailIf(!session.Complete || session.RoomId != 0xb6 || !session.Save.HasGlobalFlag(0x0a) || session.Save.HasGlobalFlag(0x30) ||
                    !session.Save.HasRoomFlag(0,0x98,0xc0) || !session.Save.HasRoomFlag(0,0xb6,0x40) || session.DisabledObjects != 0 || session.Link.CutsceneControlled,
                    $"Seasons opening failed to release Link after Impa: phase={session.Phase}, signal=${session.Signal(0):x2}.");
                FailIf(session.Save.GetRoomFlags(0, 0xa7) != 0x50 || session.Save.GetRoomFlags(0, 0x97) != 0x10 ||
                    session.Save.GetRoomFlags(0, 0x98) != 0xd0 || session.Save.GetRoomFlags(0, 0xb6) != 0x50 ||
                    session.Save.GetRoomFlags(0, 0xb8) != 0 || session.Save.GetRoomFlags(0, 0xc6) != 0 || session.Save.GetRoomFlags(0, 0xc8) != 0,
                    "Normal entries must mark visited rooms; forceLoadRoom's season-montage pictures must not.");
                if (baseline is null) { baseline = session.Save.Serialize(); baselineRng = session.Random.Calls; baselineTrace = trace.ToArray(); }
                else FailIf(!baseline.SequenceEqual(session.Save.Serialize()) || baselineRng != session.Random.Calls || !baselineTrace!.SequenceEqual(trace),
                    "Seasons cutscene timing, flags, playtime or shared RNG changed when four original updates shared one host frame.");
                if (!batched) await CaptureSeasonsOpeningScreen("complete",root.GetViewport());
                Vector2 after = session.Link.PrecisePosition;
                application.Step(3, Vector2.Right);
                FailIf(session.Link.PrecisePosition.X <= after.X || root.FileSlotExists(0, OracleGame.Ages), "After the intro, normal input must move Link without writing an Ages slot.");
                FailIf(session.Entities.FrameCounter != session.Memory.ReadWramByte(0xcc00) ||
                    !ReferenceEquals(session.Rooms.CurrentRoom, session.Entities.ActiveRoom) ||
                    session.Actors.Any(actor => actor.Slot is < 2 or > 15) ||
                    session.Actors.Count(actor => actor.Id == 0x9d) != 1,
                    "The ordinary Seasons aftermath room must use the shared room identity and native $d2-$df interaction pool; unlinked Impa $9d:$04 deletes after allocation.");
                TalkSeasons(application, session, 1, 0x9d);
                FailIf(session.LastText != 0x2503 || session.DisabledObjects != 0 || !session.Complete,
                    "Impa must remain reachable and use TX_2503 after releasing control.");
                session.LoadRoom(0xb6, 0);
                application.Step(2, Vector2.Zero, batched: batched);
                var impa = session.Actors.Single(actor => actor.Id == 0x9d);
                FailIf(impa.Subid != 1 || impa.Node.Position != new Vector2(0x18, 0x28) ||
                    !ReferenceEquals(session.Rooms.CurrentRoom, session.Entities.ActiveRoom),
                    "impa.s:$9d:$00 must re-enter through the shared room lifecycle at $28,$18 as subid $01 after room flag $40.");
                TalkSeasons(application, session, 1, 0x9d);
                FailIf(session.LastText != 0x2503 || session.DisabledObjects != 0,
                    "Impa's ordinary conversation must remain reachable after shared room reload and completion.");
                WalkSeasons(application, session, point => point.X == 0x48 && point.Y == 120);
                application.Step(2, Vector2.Down);
                FailIf(!session.Scrolling,
                    "The completed opening must allow leaving aftermath room $b6 through its actual south exit.");
                application.Step(70, Vector2.Zero, batched: batched);
                FailIf(session.Scrolling || session.Link.CutsceneControlled || session.RoomId != 0xc6,
                    "The aftermath fade must finish in room $c6 and release normal input.");
                application.Step(20, Vector2.Up, batched: batched);
                application.Step(32, Vector2.Zero, batched: batched);
                // bank1.func_5c18 returns to cutscene00, whose updateAllObjects
                // runs throughout fade-in. Link's own palette gate is separate.
                FailIf(session.RoomId != 0xb6 || !session.Scrolling || !session.Actors.Any(actor => actor.Initialized),
                    "Room-pack fade-in must dispatch destination state zero after the full-load update.");
                application.Step(16, Vector2.Zero, batched: batched);
                FailIf(!session.Actors.Any(actor => actor.Initialized),
                    "Destination actors must retain their native initialization during room-pack fade-in.");
                application.Step(22, Vector2.Zero, batched: batched);
                FailIf(session.RoomId != 0xb6 || session.Scrolling,
                    "The aftermath exit must support returning to room $b6.");
                session.LoadRoom(0xb6, 0); session.Link.WarpTo(new(0x58, 0x48));
                application.Step(2, Vector2.Zero);
                WalkSeasons(application, session, point => point.X == 0x70 && point.Y == 6);
                session.Memory.SetField("wDisableScreenTransitions", 1);
                session.Memory.SetField("wScreenTransitionDelay", 3);
                application.Step(2, Vector2.Up);
                FailIf(session.Scrolling || session.RoomId != 0xb6 || session.Memory.ReadField("wScreenTransitionDelay") != 3,
                    "The native transition-disable byte must gate an open edge before decrementing its shared delay.");
                session.Memory.SetField("wDisableScreenTransitions", 0);
                int previousDelay = 3;
                void ObserveBoundaryDelay()
                {
                    int remaining = session.Memory.ReadField("wScreenTransitionDelay");
                    FailIf(session.Scrolling || remaining > previousDelay || remaining < previousDelay - 1,
                        "Seasons must sample the shared delay once per crossed boundary and wait on its zero update.");
                    previousDelay = remaining;
                }
                application.Step(3, Vector2.Up, batched: batched, afterUpdate: ObserveBoundaryDelay);
                // Retained fractions and slow movement can leave an update at
                // YH=$06. Only a subsequent YH<=$05 probe decrements the byte.
                for (int update = 0; update < 8 && previousDelay != 0; update++)
                    application.Step(1, Vector2.Up, afterUpdate: ObserveBoundaryDelay);
                FailIf(previousDelay != 0, "The live north-edge approach did not exhaust its native delay.");
                for (int update = 0; update < 4 && !session.Scrolling; update++) application.Step(1, Vector2.Up);
                FailIf(!session.Scrolling || session.RoomId != 0xa6,
                    "Same-pack north exits must use ordinary vertical scrolling after the opening.");
                int scrollingRng = session.Random.Calls;
                application.Step(39, Vector2.Zero, batched: batched);
                FailIf(session.Scrolling || session.Random.Calls != scrollingRng || session.Link.CutsceneControlled,
                    "A vertical scroll must finish its native graphics/movement/cleanup updates without advancing destination actors.");
                application.Step(20, Vector2.Down, batched: batched);
                application.Step(39, Vector2.Zero, batched: batched);
                FailIf(session.RoomId != 0xb6 || session.Scrolling,
                    "Same-pack scrolling must allow returning through the original exit.");
                session.LoadRoom(0xa7, 0);
                application.Step(3, Vector2.Zero);
                FailIf(session.Actors.Any(actor => actor.Id == 0xa5) || session.Phase != SeasonsOpeningPhase.Complete,
                    "Revisiting forest $a7 after flag $40 must not replay Din's discovery cutscene.");
                session.LoadRoom(0x98, 0);
                application.Step(3, Vector2.Zero);
                FailIf(session.Actors.Any(actor => actor.Id == 0x4e) || session.Phase != SeasonsOpeningPhase.Complete,
                    "Revisiting the destroyed camp must not respawn the troupe or replay its opening.");
                session.LoadRoom(0xb6, 0); session.Link.WarpTo(new(0x58, 0x48));
                application.Step(2, Vector2.Zero);
                var helpers = new List<IRoomEntity>();
                ValidateSeasonsSharedMenus(root, application, session, batched);
                int allocationRng = session.Random.Calls;
                while (session.Entities.TryCreateExclamation(new Vector2(80,64),0xff) is { } helper) helpers.Add(helper);
                FailIf(helpers.Count != 13 || session.Random.Calls != allocationRng ||
                    helpers.Select(session.Entities.NativeInteractionSlot).Distinct().Count() != 13,
                    "The shared allocator must admit exactly fourteen dynamic interactions including Impa, without construction RNG or unchecked over-allocation.");
                var script=session.Data.Scripts.Values.First(script=>script.OfType<CutsceneNativeCommand>()
                    .Any(command=>command.Handler.Contains("din_createExclamationMark",StringComparison.Ordinal)));
                var command=script.OfType<CutsceneNativeCommand>()
                    .First(command=>command.Handler.Contains("din_createExclamationMark",StringComparison.Ordinal));
                var helperSounds=root._sound.AttachPlayRequestAudit();
                var runner=new CutsceneCommandRunner(new SeasonsOpeningScriptHost(session,session.Actors.Single(actor=>actor.Id==0x9d)));
                runner.Start(script,command.Source.CommandIndex);runner.AdvanceFrame();
                FailIf(helperSounds.Requests.Count!=0 || session.Random.Calls!=allocationRng,
                    "Din's actual imported native helper command must reject a full interaction pool before its SND_CLINK or RNG.");
                int releasedSlot = session.Entities.NativeInteractionSlot(helpers[0]);
                session.Entities.RetireEntity(helpers[0]);
                var replacement = session.Entities.TryCreateExclamation(new Vector2(80,64),0xff);
                FailIf(replacement is null || session.Entities.NativeInteractionSlot(replacement) != releasedSlot,
                    "interactionDelete must release its native slot for the next checked shared allocation.");
            }
            finally { root.Free(); captureViewport?.Free(); ResetValidationInput(); }
        }
    }

    private readonly HashSet<string> _seasonsCapturedScreens = [];
    private async Task CaptureSeasonsOpeningScreen(string name,Viewport viewport)
    {
        string directory = OS.GetEnvironment("OOA_SEASONS_SCREENSHOT_OUTPUT");
        if (string.IsNullOrEmpty(directory) || DisplayServer.GetName() == "headless" || !_seasonsCapturedScreens.Add(name)) return;
        System.IO.Directory.CreateDirectory(directory);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using Image image = viewport.GetTexture().GetImage();
        image.SavePng(System.IO.Path.Combine(directory, name + ".png"));
    }

    private void TalkSeasons(ApplicationValidationFixture application, SeasonsOpeningSession session, int subid, int id = 0x4e)
    {
        var actor = session.Actors.Single(value => value is not null && value.Id == id && value.Subid == subid)!;
        Vector2I[] facings = [Vector2I.Up,Vector2I.Right,Vector2I.Down,Vector2I.Left];
        bool InRange(Vector2I point, Vector2I facing)
        {
            Vector2 probe = (Vector2)point + (Vector2)facing * 10;
            return Math.Abs(actor.Node.Position.X - probe.X) < actor.RadiusX && Math.Abs(actor.Node.Position.Y - probe.Y) < actor.RadiusY;
        }
        WalkSeasons(application, session, point => facings.Any(facing => InRange(point, facing)));
        Vector2I facing = facings.First(value => InRange((Vector2I)session.Link.Position, value));
        application.Step(1, facing);
        application.Step(1, Vector2.Zero, ["attack"], ["attack"]);
        FailIf(session.Memory.ReadField("wDisableRingTransformations") != 8,
            "Native A-sensitive selection must publish its eight-call ring suppression before the actor's script runs.");
        for (int update = 0; update < 16 && !session.Dialogue.IsOpen; update++) application.Step(1, Vector2.Zero);
        FailIf(!session.Dialogue.IsOpen, $"Seasons actor ${id:x2}:${subid:x2} is unreachable through ordinary A input at {session.Link.Position}.");
        for (int update = 0; update < 1800 && session.Dialogue.IsOpen; update++)
            application.Step(1, Vector2.Zero, ["attack"], update % 4 == 0 ? ["attack"] : []);
        FailIf(session.Dialogue.IsOpen, $"Troupe actor $4e:${subid:x2} dialogue did not finish.");
        application.Step(8, Vector2.Zero);
    }

    private void WalkSeasons(ApplicationValidationFixture application, SeasonsOpeningSession session, Func<Vector2I,bool> goal)
    {
        Vector2I start = (Vector2I)session.Link.Position;
        var queue = new Queue<Vector2I>(); queue.Enqueue(start);
        var previous = new Dictionary<Vector2I,Vector2I> { [start] = start };
        Vector2I[] directions = [Vector2I.Up,Vector2I.Right,Vector2I.Down,Vector2I.Left];
        Vector2[] footprint=[new(-6,-6),new(6,-6),new(-6,6),new(6,6)];
        Vector2I? destination = null;
        byte[] scratch = Enumerable.Range(0,4).Select(index => session.Memory.ReadWramByte(0xcec0 + index)).ToArray();
        try
        {
            while (queue.TryDequeue(out var current))
            {
                if (goal(current)) { destination = current; break; }
                for (int index = 0; index < directions.Length; index++)
                {
                    Vector2I next = current + directions[index];
                    if (next.X < 6 || next.X > session.Room.Width-6 || next.Y < 6 || next.Y > session.Room.Height-8 || previous.ContainsKey(next) || session.Collision.Collides(next)) continue;
                    if(footprint.Any(offset=>
                        session.Room.GetTerrainInfo((Vector2)next+offset).Type is TerrainType.Hole or TerrainType.WarpHole or TerrainType.Water or TerrainType.Lava))continue;
                    Vector2 delta = session.Collision.ResolveNativeMovement(current, 0x28, index * 8, true);
                    if (delta != (Vector2)directions[index]) continue;
                    previous.Add(next, current); queue.Enqueue(next);
                }
            }
        }
        finally { for (int index = 0; index < 4; index++) session.Memory.SetWramByte(0xcec0 + index, scratch[index]); }
        FailIf(destination is null, $"No collision-valid Seasons path from {start} in room ${session.Rooms.ActiveGroup:x1}:${session.RoomId:x2}; reachable{previous.Count}, bounds{previous.Keys.Min(p=>p.X)},{previous.Keys.Min(p=>p.Y)}..{previous.Keys.Max(p=>p.X)},{previous.Keys.Max(p=>p.Y)}.");
        var path = new Stack<Vector2I>();
        for (Vector2I point = destination!.Value; point != start; point = previous[point]) path.Push(point);
        while (path.TryPop(out var point))
        {
            Vector2 delta = point - (Vector2I)session.Link.Position;
            for (int update = 0; update < 8 && (Vector2I)session.Link.Position != point; update++) application.Step(1, delta);
            FailIf((Vector2I)session.Link.Position != point, $"Seasons movement failed at {session.Link.Position}, expected {point} through actual room geometry; physics={session.Link.IsPhysicsProcessing()}, disabled=${session.DisabledObjects:x2}, cutscene={session.Link.CutsceneControlled}, fade={session.Fading}.");
        }
    }
}
