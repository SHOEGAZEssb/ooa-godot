using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
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
            OracleRoomData loaded = data.LoadRoom(room, season);
            FailIf(loaded.Width != 160 || loaded.Height != 128 || loaded.Texture.GetSize() != new Vector2(160,128),
                $"Seasons opening room $00:${room:x2} season ${season:x2} lost its source geometry.");
        }
        foreach (var (id, subid) in new[] { (0x4e,0),(0x4e,4),(0x4e,5),(0x4e,6),(0x4e,7),(0x4f,0),(0x4f,1),(0x4f,2),(0x4f,3),(0x4f,4),(0x9d,0),(0x9f,0),(0xa5,9),(0x27,0) })
        {
            var actor = data.CreateActor(id, subid, Vector2.Zero);
            try { FailIf(actor.CurrentAnimationOpaquePixels == 0, $"Seasons opening actor ${id:x2}:${subid:x2} has no visible source pixels."); }
            finally { actor.Free(); }
        }
        data.LoadRoom(0x98, 0);
        FailIf(data.Palettes.Resolve(2, 0) != new Color(1,1,0x0e / 31f) || data.Palettes.Resolve(2, 1) != new Color(0x0e / 31f,0x1c / 31f,2 / 31f) ||
            data.SpritePalettes[0, 3] != new Color(1,0x1a / 31f,0x11 / 31f),
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
            CanvasLayer? captureLayer = null;
            if (!string.IsNullOrEmpty(OS.GetEnvironment("OOA_SEASONS_SCREENSHOT_OUTPUT")))
            { captureLayer = new() { Layer = 100 }; AddChild(captureLayer); captureLayer.AddChild(root); }
            else AddChild(root);
            try
            {
                GameSelectScreen selector = root.InitializeGameSelect();
                selector.RequestPointerSelection(OracleGame.Seasons, true); root.Step(1, false);
                root.CompleteBootLoading();
                StartSeasonsFileForValidation(root, batched);
                var session = root._seasonsOpening ?? throw new InvalidOperationException("Seasons selection did not start its new game.");
                FailIf(selector.IsActive || session.Phase != SeasonsOpeningPhase.Pregame || root.FileSlotExists(0, OracleGame.Ages),
                    "Seasons must begin its own pregame without touching an Ages file slot.");
                var application = new ApplicationValidationFixture(root);
                int frontendRng = session.Random.Calls;
                FailIf(frontendRng != 34, "Seasons title must consume its shared RNG before the opening; boot preparation must not.");
                int chunk = batched ? 4 : 1;
                for (int update = 0; update < 2400; update += chunk)
                {
                    application.Step(chunk, Vector2.Zero, ["attack"], update % 4 == 0 ? ["attack"] : [], batched);
                    if (!batched && update == 400) await CaptureSeasonsOpeningScreen("pregame");
                    if (!batched && session.Phase == SeasonsOpeningPhase.Forest && session.Actors.Any(actor => actor is { Id: 0xa5 } && actor.Node.Position.Y is > 20 and < 48))
                        await CaptureSeasonsOpeningScreen("forest");
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
                if (!batched) await CaptureSeasonsOpeningScreen("troupe");
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
                SeasonsOpeningPhase preceding = session.Phase;
                void ObservePhase()
                {
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
                        var screen = session.GetNode<SeasonsOpeningScreen>("Cutscene");
                        if (screen.Visible && screen.Dance && update > 40) await CaptureSeasonsOpeningScreen("dance");
                        if (session.Phase == SeasonsOpeningPhase.Capture && session.Signal(0) == 5) await CaptureSeasonsOpeningScreen("tornado");
                        if (session.Phase == SeasonsOpeningPhase.Imprisonment && session.Signal(0) == 0x0a && session.Dialogue.IsPageComplete) await CaptureSeasonsOpeningScreen("onox");
                        if (session.Phase == SeasonsOpeningPhase.Imprisonment && session.Signal(0) == 0x0d && !screen.CastleCleared && !session.Dialogue.IsOpen && !session.Fading) await CaptureSeasonsOpeningScreen("crystal");
                        if (session.Phase == SeasonsOpeningPhase.TempleSinking && session.Actors.Any(actor => actor is { Id: 0x86 }) && !session.Fading) await CaptureSeasonsOpeningScreen("temple");
                        if (session.Phase == SeasonsOpeningPhase.Aftermath && session.Dialogue.IsPageComplete) await CaptureSeasonsOpeningScreen("impa");
                    }
                }
                FailIf(danceUpdates is < 360 or > 363, $"Din's $0258 close-up must reject A until its counter is below $00f0; observed {danceUpdates} active updates.");
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
                if (!batched) await CaptureSeasonsOpeningScreen("complete");
                Vector2 after = session.Link.PrecisePosition;
                application.Step(3, Vector2.Right);
                FailIf(session.Link.PrecisePosition.X <= after.X || root.FileSlotExists(0, OracleGame.Ages), "After the intro, normal input must move Link without writing an Ages slot.");
                TalkSeasons(application, session, 0, 0x9d);
                FailIf(session.LastText != 0x2503 || session.DisabledObjects != 0 || !session.Complete,
                    "Impa must remain reachable and use TX_2503 after releasing control.");
            }
            finally { root.Free(); captureLayer?.Free(); ResetValidationInput(); }
        }
    }

    private readonly HashSet<string> _seasonsCapturedScreens = [];
    private async Task CaptureSeasonsOpeningScreen(string name)
    {
        string directory = OS.GetEnvironment("OOA_SEASONS_SCREENSHOT_OUTPUT");
        if (string.IsNullOrEmpty(directory) || DisplayServer.GetName() == "headless" || !_seasonsCapturedScreens.Add(name)) return;
        System.IO.Directory.CreateDirectory(directory);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using Image image = GetViewport().GetTexture().GetImage();
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
                    if (next.X is < 6 or > 154 || next.Y is < 6 or > 120 || previous.ContainsKey(next) || session.Collision.Collides(next)) continue;
                    Vector2 delta = session.Collision.ResolveNativeMovement(current, 0x28, index * 8, true);
                    if (delta != (Vector2)directions[index]) continue;
                    previous.Add(next, current); queue.Enqueue(next);
                }
            }
        }
        finally { for (int index = 0; index < 4; index++) session.Memory.SetWramByte(0xcec0 + index, scratch[index]); }
        FailIf(destination is null, $"No collision-valid Seasons path from {start} in room $00:${session.RoomId:x2}.");
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
