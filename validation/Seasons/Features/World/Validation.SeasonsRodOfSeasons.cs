using System.Threading.Tasks;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private async Task ValidateSeasonsRodAcquisition()
    {
        foreach (bool batched in new[] { false, true })
        {
            var viewport = new SubViewport { Size = new(160,144), Disable3D = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                CanvasItemDefaultTextureFilter = Viewport.DefaultCanvasItemTextureFilter.Nearest };
            AddChild(viewport);
            var root = StartSeasonsAfterImpa(batched,viewport);
            try
            {
                var session = root._seasonsOpening!;
                var app = new ApplicationValidationFixture(root);
                session.DebugTravel(3, 0xac); session.Link.WarpTo(new(80, 96));
                app.Step(2, Vector2.Zero, batched: batched);
                FailIf(session.Entities.EntityAdapters<SeasonsRodAcquisitionRoomEntity>().Count() != 1 ||
                    session.Entities.EntityAdapters<SeasonsTempleDecorationRoomEntity>().Count() != 4,
                    "Seasons $3:ac must construct the source $4c:$0a-$0d statues and $e6:$00 controller in order.");
                for (int update = 0; update < 50 && session.DisabledObjects == 0; update++) app.Step(1, Vector2.Up);
                FailIf(session.DisabledObjects != 0x81 || session.Inventory.HasTreasure(TreasureId.RodOfSeasons),
                    $"Approaching the actual $3:ac pedestal must begin the Rod script before its grant: XY={session.Link.Position}, mask=${session.DisabledObjects:x2}.");
                var texts = new List<int>();
                bool held = false, checkedSprite = false;
                for (int update = 0; update < 2400; update++)
                {
                    var rod = session.Entities.EntityAdapters<SeasonsRodAcquisitionRoomEntity>()
                        .SingleOrDefault(actor => actor.ReadByte(0x42) == 2);
                    if (!checkedSprite && rod is not null && rod.Npc.Visible && !session.Fading && root._scene.WarpFade.Color.A == 0)
                    {
                        ValidateSeasonsRodAcquisitionPixels(rod.Npc);
                        checkedSprite = true;
                        if (DisplayServer.GetName() != "headless")
                        {
                            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                            await CaptureSeasonsOpeningScreen("temple-rod",viewport);
                        }
                    }
                    if (session.Dialogue.IsOpen)
                    {
                        int id = session.Dialogue.SourceTextId ?? -1;
                        texts.Add(id);
                        if (id == 0x0071)
                        {
                            held = true;
                            FailIf(!session.Inventory.HasTreasure(TreasureId.RodOfSeasons) || session.Inventory.ObtainedSeasons != 0x80 ||
                                !session.Save.HasRoomFlag(3, 0xac, 0x20), "TX_0071 must commit the unpowered Rod and native room flag $20 exactly once.");
                            app.Step(5, Vector2.Zero, batched: batched);
                            FailIf(session.DisabledObjects == 0, "The Rod's held text must retain its script input owner.");
                        }
                        session.Dialogue.Close();
                    }
                    app.Step(1, Vector2.Zero);
                    if (held && session.DisabledObjects == 0 && !session.Fading &&
                        !session.Entities.EntityAdapters<SeasonsRodAcquisitionRoomEntity>().Any()) break;
                }
                FailIf(!checkedSprite || !held || !texts.SequenceEqual(new[] { 0x0071, 0x0810 }) || session.DisabledObjects != 0 || session.Fading ||
                    session.Memory.ReadWramByte(0xcceb) != 1, $"Rod acquisition did not finish its source messages/aura/control: texts={string.Join(',',texts.Select(id=>id.ToString("x4")))}, mask=${session.DisabledObjects:x2}.");
                session.DebugTravel(0, 0xb6); session.DebugTravel(3, 0xac); app.Step(3, Vector2.Zero, batched: batched);
                FailIf(session.Entities.EntityAdapters<SeasonsRodAcquisitionRoomEntity>().Any(), "The collected Rod must stay absent on $3:ac re-entry.");
                FailIf(!OracleSaveData.TryDeserialize(session.Save.Serialize(), OracleGame.Seasons, out var saved) ||
                    !saved!.HasTreasure(TreasureId.RodOfSeasons) || !saved.HasRoomFlag(3, 0xac, 0x20), "Explicit save/reload must retain the Rod and acquisition room flag.");
            }
            finally { root.Free(); viewport.Free(); ResetValidationInput(); }
        }
    }

    private void ValidateSeasonsRodAcquisitionPixels(NpcCharacter rod)
    {
        // Clean-US spr_item_icons_2 at $1b:7820; $e6:$02 uses tiles $10/$11,
        // palette $02 and one OAM cell (Y=$08, X=$04).
        var rom = ValidationRom.LoadCleanUs(OracleGame.Seasons);
        var palettes = new LinkCollisionRom(game:OracleGame.Seasons);
        palettes.Call(0x04e7,bank:0,accumulator:0x0f);
        var frames = SomariaPrivate<List<NpcCharacterAnimationFrame>>(rod,"_scriptAnimation");
        var frame = frames[rod.CurrentAnimationFrame];
        using Image actual = frame.Texture.GetImage();
        FailIf(actual.GetWidth() != 8 || actual.GetHeight() != 16 || frame.Offset != new Vector2(-4,-8),
            "Temple Rod $e6:$02 must retain its native 8x16 cell and OAM offset.");
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 8; x++)
        {
            int at = 0x6f820 + 0x100 + y*2, bit = 7-x;
            int shade = ((rom.Span[at]>>bit)&1) | (((rom.Span[at+1]>>bit)&1)<<1);
            int paletteAt = 0xdec0 + 2*8 + shade*2;
            int rgb = palettes.BankByte(2,paletteAt) | (palettes.BankByte(2,paletteAt+1)<<8);
            Color expected = shade == 0 ? Colors.Transparent : new((rgb&31)/31f,((rgb>>5)&31)/31f,((rgb>>10)&31)/31f);
            Color observed = actual.GetPixel(x,y);
            FailIf(Math.Abs(observed.A-expected.A)>1f/255 || shade != 0 &&
                (Math.Abs(observed.R-expected.R)>1f/255 || Math.Abs(observed.G-expected.G)>1f/255 || Math.Abs(observed.B-expected.B)>1f/255),
                $"Temple Rod $e6:$02 pixel({x},{y}) differs from the clean-US Seasons Rod; another game's icon sheet must not replace it.");
        }
    }

    private void ValidateSeasonsSeasonPowers()
    {
        foreach (bool batched in new[] { false, true })
        {
            var root = StartSeasonsAfterImpa(batched);
            try
            {
                var session = root._seasonsOpening!;
                var app = new ApplicationValidationFixture(root);
                session.Inventory.GiveTreasure(TreasureId.RodOfSeasons, 7);
                foreach (var entry in new[] { (Room:0xf2, Season:3), (Room:0xf5, Season:0),
                    (Room:0xf8, Season:1), (Room:0xfb, Season:2) })
                {
                    session.DebugTravel(5, entry.Room); session.Link.WarpTo(new(112, 112));
                    app.Step(2, Vector2.Zero, batched: batched);
                    for (int update = 0; update < 80 && session.DisabledObjects == 0; update++) app.Step(1, Vector2.Up);
                    FailIf(session.DisabledObjects != 0x81, $"Spirit $5:{entry.Room:x2} must be reachable through its real floor geometry; XY={session.Link.Position}.");
                    for (int update = 0; update < 1600 && (!session.Save.HasRoomFlag(5, entry.Room, 0x40) || session.DisabledObjects != 0 || session.Dialogue.IsOpen); update++)
                    {
                        if (session.Dialogue.IsOpen) session.Dialogue.Close();
                        app.Step(1, Vector2.Zero);
                    }
                    FailIf((session.Inventory.ObtainedSeasons & (1 << entry.Season)) == 0 || !session.Save.HasRoomFlag(5, entry.Room, 0x40) ||
                        session.DisabledObjects != 0, $"Spirit $5:{entry.Room:x2} did not grant native season {entry.Season} and release input.");
                }
                FailIf(session.Inventory.ObtainedSeasons != 0x8f, "Four powers must retain the Rod's obtained bit and all four season bits.");
                session.DebugTravel(5, 0xf2); session.Link.WarpTo(new(112, 64)); app.Step(3, Vector2.Zero, batched: batched);
                FailIf(session.DisabledObjects != 0 || session.Inventory.ObtainedSeasons != 0x8f,
                    "A completed season statue must await a new A interaction instead of replaying its grant.");
                for (int update = 0; update < 40; update++) app.Step(1, Vector2.Up);
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    app.Step(1, Vector2.Zero, held: ["attack"], pressed: ["attack"]);
                    for (int update = 0; update < 240 && !session.Dialogue.IsOpen; update++) app.Step(1, Vector2.Zero);
                    FailIf(session.Dialogue.SourceTextId != 0x0805 || session.DisabledObjects != 0x81,
                        $"Winter spirit repeat {repeat} must remain reachable with A through the statue collision; XY={session.Link.Position}, text=${session.Dialogue.SourceTextId:x4}.");
                    session.Dialogue.Close(); app.Step(60, Vector2.Zero, batched: batched);
                    FailIf(session.DisabledObjects != 0 || session.Entities.EntityAdapters<SeasonsSpiritRoomEntity>().Any() ||
                        session.Inventory.ObtainedSeasons != 0x8f, "Repeat spirit dialogue must retire its child and release input without another grant.");
                }
            }
            finally { root.Free(); }
        }
    }

    private async Task ValidateSeasonsRodUse()
    {
        foreach (bool batched in new[] { false, true })
        {
            var viewport = new SubViewport { Size = new(160,144), Disable3D = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                CanvasItemDefaultTextureFilter = Viewport.DefaultCanvasItemTextureFilter.Nearest };
            AddChild(viewport);
            var root = StartSeasonsAfterImpa(batched,viewport);
            try
            {
                var session = root._seasonsOpening!;
                var app = new ApplicationValidationFixture(root);
                session.Inventory.GiveTreasure(TreasureId.RodOfSeasons, 7); session.Inventory.EquipA(TreasureId.RodOfSeasons);
                session.DebugTravel(0, 0x99);
                session.Inventory.GiveTreasure(TreasureId.Sword,1); session.Inventory.EquipB(TreasureId.Sword);
                // room0099: source stump at packed position $42.
                session.Link.WarpTo(new(40, 104));
                app.Step(2,Vector2.Zero);
                app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"]);
                FailIf(!session.Link.IsUsingRodOfSeasons || session.Memory.ReadWramByte(0xd200) != 0xff,
                    "Off a stump, the Rod must retain the original $ff parent priority.");
                app.Step(1,Vector2.Zero,held:["item"],pressed:["item"]);
                FailIf(!session.Link.IsUsingRodOfSeasons || session.Link.IsAttacking,
                    "An off-stump Rod must reject the lower-priority Sword reservation.");
                app.Step(35,Vector2.Zero,batched:batched);
                FailIf(session.Transitions.IsTransitioning || session.Memory.ReadWramByte(0xd600) != 0,
                    "An ordinary ground swing must retire its reserved child without a season change.");
                for (int update = 0; update < 100 && (session.Memory.ReadField("wActiveTileType") != 8 || session.Link.EnemyContactZ != 0); update++)
                    app.Step(1, Vector2.Up);
                FailIf(session.Memory.ReadField("wActiveTileType") != 8 || session.Link.EnemyContactZ != 0,
                    $"The actual $0:99 stump must be mountable from below; XY={session.Link.Position}, Z={session.Link.EnemyContactZ}.");
                app.Step(8, Vector2.Zero, batched: batched);
                app.Step(1, Vector2.Zero, held: ["attack"], pressed: ["attack"]);
                FailIf(!session.Link.IsUsingRodOfSeasons || session.Memory.ReadWramByte(0xd200) != 0x31 ||
                    session.Memory.ReadWramByte(0xd606) != 0x10, $"Rod parent/child initialization must retain native $31/$d6 and a full $10-update trigger delay: parent=${session.Memory.ReadWramByte(0xd200):x2}:${session.Memory.ReadWramByte(0xd201):x2}, child=${session.Memory.ReadWramByte(0xd600):x2}:${session.Memory.ReadWramByte(0xd606):x2}, terrain=${session.Memory.ReadField("wActiveTileType"):x2}, XY={session.Link.Position}, Z={session.Link.EnemyContactZ}, A=${session.Inventory.EquippedA:x2}, disabled={session.Link.World.ItemUsageDisabled}/{session.Link.World.SwordDisabled}, state=${session.Memory.ReadWramByte(0xd004):x2}, air=${session.Memory.ReadField("wLinkInAir"):x2}.");
                if (DisplayServer.GetName() != "headless")
                {
                    await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    await CaptureSeasonsOpeningScreen("rod-swing-up",viewport);
                }
                app.Step(35, Vector2.Zero, batched: batched);
                FailIf(session.Transitions.IsTransitioning || session.Link.IsUsingRodOfSeasons,
                    "An unpowered Rod swing on a stump must end without changing seasons.");
                app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"]);
                app.Step(2,Vector2.Zero);
                app.Step(1,Vector2.Zero,held:["item"],pressed:["item"]);
                FailIf(session.Link.IsUsingRodOfSeasons || !session.Link.IsAttacking,
                    "On a stump, Sword priority $61 must replace the Rod's $31 parent.");
                app.Step(40,Vector2.Zero,batched:batched);
                app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"]);
                session.Memory.SetField("wSwordDisabledCounter",2); app.Step(1,Vector2.Zero);
                FailIf(session.Link.IsUsingRodOfSeasons || session.Memory.ReadWramByte(0xd600) != 0,
                    "Sword jinx must cancel the Rod parent and its physical child.");
                session.Memory.SetField("wSwordDisabledCounter",0);
                session.Inventory.GiveTreasure(TreasureId.RodOfSeasons, 3);
                int[] seasons = [3,0,3];
                session.Inventory.GiveTreasure(TreasureId.RodOfSeasons, 0);
                session.Memory.SetField("wRoomStateModifier", 0);
                foreach (int expected in seasons)
                {
                    app.Step(1, Vector2.Zero, held: ["attack"], pressed: ["attack"]);
                    app.Step(15, Vector2.Zero, batched: batched);
                    FailIf(session.Transitions.IsTransitioning, "Rod must retain the source $10 normal-child updates before its interaction trigger.");
                    app.Step(1, Vector2.Zero);
                    FailIf(!session.Transitions.IsTransitioning || session.Memory.ReadField("wRoomStateModifier") != expected,
                        $"The Rod must cycle only unlocked seasons on the trigger update; wanted {expected}.");
                    app.Step(110, Vector2.Zero, batched: batched);
                    FailIf(session.Transitions.IsTransitioning || session.Room.TilesetStateModifier != expected ||
                        session.Rooms.ActiveGroup != 0 || session.RoomId != 0x99 || session.Memory.ReadField("wcc4c") != 0,
                        "Season reload must update native layout/graphics/collisions and return control in the same room.");
                }
            }
            finally { root.Free(); viewport.Free(); ResetValidationInput(); }
        }
    }

    private void ValidateSeasonsTempleRoutes()
    {
        var root = StartSeasonsAfterImpa(false);
        try
        {
            var session = root._seasonsOpening!;
            var app = new ApplicationValidationFixture(root);
            session.DebugTravel(1, 0x29); session.Link.WarpTo(new(80,112));
            for (int update = 0; update < 60 && session.DisabledObjects == 0; update++) app.Step(1, Vector2.Up);
            var texts = new List<int>();
            for (int update = 0; update < 600; update++)
            {
                if (session.Dialogue.IsOpen) { texts.Add(session.Dialogue.SourceTextId ?? -1); session.Dialogue.Close(); }
                app.Step(1, Vector2.Zero);
                if (session.Save.HasRoomFlag(1, 0x29, 0x40) && session.DisabledObjects == 0) break;
            }
            FailIf(!texts.SequenceEqual(new[]{0x0800,0x0801,0x0802,0x0803}), "Temple entry must retain all four source messages and finish its one-time flag.");
            session.DebugTravel(5, 0xf2); session.Link.WarpTo(new(112,96));
            app.Step(60, Vector2.Up);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                app.Step(1, Vector2.Zero, held:["attack"], pressed:["attack"]);
                for (int update = 0; update < 240 && !session.Dialogue.IsOpen; update++) app.Step(1, Vector2.Zero);
                FailIf(session.Dialogue.SourceTextId != 0x0811 || session.DisabledObjects != 0x81,
                    $"Without the Rod, the winter statue must explain the requirement on each reachable A interaction: repeat={repeat}, XY={session.Link.Position}, text=${session.Dialogue.SourceTextId:x4}, mask=${session.DisabledObjects:x2}.");
                session.Dialogue.Close(); app.Step(60, Vector2.Zero);
                FailIf(session.Inventory.HasTreasure(TreasureId.RodOfSeasons) || session.Save.HasRoomFlag(5,0xf2,0x40) ||
                    session.DisabledObjects != 0, "No-Rod spirit dialogue must not grant a power or consume the statue.");
            }
            session.Inventory.GiveTreasure(TreasureId.Sword,1); session.Inventory.EquipA(TreasureId.Sword);
            session.DebugTravel(5,0xf0); session.Link.WarpTo(new(120,104));
            FailIf(session.Collision.Collides(session.Link.Position), "Winter temple orb approach must start on actual floor.");
            app.Step(4,Vector2.Up);
            app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"]);
            app.Step(100,Vector2.Zero);
            FailIf(session.Memory.ReadField("wToggleBlocksState") != 1 || !session.Save.HasRoomFlag(5,0xf0,0x40) ||
                session.Room.GetMetatile(new(120,120)) != 0x6a,
                $"The actual winter orb must create source bridge $6a at $77: toggle=${session.Memory.ReadField("wToggleBlocksState"):x2}, XY={session.Link.Position}.");
        }
        finally { root.Free(); }
    }

    private void ValidateSeasonsRodSelectionRom()
    {
        var root = StartSeasonsAfterImpa(false);
        try
        {
            var session = root._seasonsOpening!;
            session.DebugTravel(0, 0x99); session.Entities.Clear();
            var native = new LinkCollisionRom(game: OracleGame.Seasons, bankedStack: true);
            foreach (var gate in new[] { (Group:0,Pack:0,Menu:0,Death:0), (Group:0,Pack:0xf1,Menu:0,Death:0),
                (Group:0,Pack:0,Menu:1,Death:0), (Group:0,Pack:0,Menu:0,Death:1), (Group:1,Pack:0,Menu:0,Death:0) })
            {
            session.DebugTravel(gate.Group, gate.Group == 0 ? 0x99 : 0x29); session.Entities.Clear();
            for (int mask = 0; mask < 16; mask++)
            for (int season = 0; season < 4; season++)
            {
                session.Save.WriteField("wObtainedSeasons", (byte)(0x80 | mask));
                session.Memory.SetField("wRoomStateModifier", (byte)season); session.Memory.SetField("wcc4c", 0);
                session.Memory.SetField("wMenuDisabled", (byte)gate.Menu); session.Memory.SetField("wPaletteThread_updateRate", 0);
                session.Memory.SetField("wLinkDeathTrigger", (byte)gate.Death); session.Memory.SetField("wRoomPack", (byte)gate.Pack);
                native[0xc6b0] = (byte)(0x80 | mask); native[0xcc4e] = (byte)season;
                native[0xcc49] = (byte)gate.Group; native[0xcc4d] = (byte)gate.Pack;
                native[0xcc02] = (byte)gate.Menu; native[0xcc34] = (byte)gate.Death;
                native[0xcc68] = native[0xc4ad] = 0;
                native[0xd240] = 1; native[0xd241] = 0x15;
                native.Call(0x5138, bank:8, objectPage:0xd2, objectOffset:0x40);
                var interaction = new SeasonsUsedRodRoomEntity(session, Vector2.Zero, 0);
                interaction.BindNativeInteractionSlot(2);
                interaction.UpdateFrame(new(session.Link, session.FrameCounter, false, null), new List<RoomEntitySpawn>());
                FailIf(session.Memory.ReadField("wRoomStateModifier") != native[0xcc4e] ||
                    session.Memory.ReadField("wcc4c") != native[0xcc68] || session.Memory.ReadField("wPaletteThread_updateRate") != native[0xc4ad],
                    $"INTERAC$15 season mask${mask:x2}/current${season:x2}/gate{gate} differs from the clean-US ROM.");
                interaction.ReleaseNativeInteractionSlot();
            }
            }
        }
        finally { root.Free(); }
    }
}
