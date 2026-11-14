namespace Oracles;

public sealed partial class ValidationRoot
{
    private void WithSeasonsVasu(bool batched, Action<SeasonsOpeningSession, ApplicationValidationFixture> scenario)
    {
        var root = StartSeasonsAfterImpa(batched);
        try
        {
            var session = root._seasonsOpening!;
            session.Save.SetTextSpeed(4);
            var app = new ApplicationValidationFixture(root);
            byte[] disk = root.FileSlotSnapshot(0, OracleGame.Seasons);
            session.DebugTravel(0, 0xe8);
            session.Link.WarpTo(new(88, 100));
            int updates = 0;
            while (!session.Transitions.IsTransitioning && updates++ < 90) app.Step(1, Vector2.Up);
            FailIf(!session.Transitions.IsTransitioning, "Vasu fixture must enter through $0:e8's real doorway.");
            app.Step(160, Vector2.Zero, batched: batched);
            var actors = session.Entities.EntityAdapters<VasuShopNpcRoomEntity>().Select(owner => owner.Npc).ToArray();
            FailIf(session.Rooms.ActiveGroup != 3 || session.RoomId != 0x91 ||
                !actors.Select(npc => npc.Record.Id * 256 + npc.Record.SubId).SequenceEqual(new[] { 0x8900, 0x8901, 0x8906, 0xe500, 0xe501 }) ||
                actors.Any(npc => !npc.Visible || npc.Record.Group != 3 || npc.AssetRoot != SeasonsGameContent.AssetRoot),
                "$3:91 must construct Vasu, blue snake, red snake and both books in native order using Seasons graphics.");
            FailIf(session.Vasu.Database.RingsAppraisedAddress != 0xc6c8 || session.Vasu.Database.GlobalFinishedGame != 0x28,
                "Seasons Vasu must use wNumRingsAppraised=$c6c8 and GLOBALFLAG_FINISHEDGAME=$28.");
            scenario(session, app);
            FailIf(!root.FileSlotSnapshot(0, OracleGame.Seasons).SequenceEqual(disk),
                "Vasu's inventory and secret transactions must remain live until an explicit save.");
        }
        finally { root.Free(); }
    }

    private static void ApproachSeasonsVasu(SeasonsOpeningSession session, ApplicationValidationFixture app, int id = 0x89, int subid = 0)
    {
        var npc = session.Entities.Entities<NpcCharacter>().Single(actor => actor.Record.Id == id && actor.Record.SubId == subid);
        int updates = 0;
        while (session.Link.Position.Y < 100 && updates++ < 90) app.Step(1, Vector2.Down);
        while (Math.Abs(session.Link.Position.X - npc.Position.X) > 1 && updates++ < 180)
            app.Step(1, session.Link.Position.X < npc.Position.X ? Vector2.Right : Vector2.Left);
        session.Link.Face(Vector2I.Up);
        while (session.Entities.FindTalkTarget(session.Link) != npc && updates++ < 240) app.Step(1, Vector2.Up);
        FailIf(session.Entities.FindTalkTarget(session.Link) != npc || session.Collision.Collides(session.Link.Position) ||
            npc.BlocksLinkCenter(session.Link.PrecisePosition),
            $"Vasu actor ${id:x2}:${subid:x2} must be reachable across actual $3:91 collision geometry: Link={session.Link.Position}.");
    }

    private static void PressSeasonsVasu(ApplicationValidationFixture app, string action = "attack") =>
        app.Step(1, Vector2.Zero, [action], [action]);

    private static void ChooseSeasonsVasu(SeasonsOpeningSession session, ApplicationValidationFixture app, int choice)
    {
        session.Dialogue.SubmitChoiceForValidation(choice);
        app.Step(1, Vector2.Zero);
    }

    private static void CloseSeasonsVasuText(SeasonsOpeningSession session, ApplicationValidationFixture app)
    {
        session.Dialogue.Close();
        app.Step(1, Vector2.Zero);
    }

    private static void AppraiseSeasonsVasuRing(SeasonsOpeningSession session, ApplicationValidationFixture app, bool batched)
    {
        app.Step(60, Vector2.Zero, batched: batched);
        PressSeasonsVasu(app);
        app.Step(100, Vector2.Zero, batched: batched);
        PressSeasonsVasu(app);
        int updates = 0;
        while (session.RingMenu.IsActive && updates++ < 600)
        {
            PressSeasonsVasu(app);
            if (session.RingMenu.IsActive) app.Step(1, Vector2.Zero);
        }
        FailIf(session.RingMenu.IsActive || session.Menus!.Pause.IsLeased || session.Vasu.Counter != 10,
            "Appraisal must return its menu/pause owner with the source ten-update script wait untouched on the closing update.");
    }

    private void ValidateSeasonsVasuTutorial()
    {
        foreach (bool batched in new[] { false, true }) WithSeasonsVasu(batched, (session, app) =>
        {
            ApproachSeasonsVasu(session, app);
            PressSeasonsVasu(app);
            FailIf(session.Vasu.Stage != EventStage.VasuFirstExplanation || session.Dialogue.SourceTextId != 0x3000 ||
                session.DisabledObjects != 0x81 || session.Memory.ReadField("wMenuDisabled") != 0x80 ||
                !session.Dialogue.CurrentMessage.Contains("Understood?", StringComparison.Ordinal),
                "First Vasu conversation must expand TX_3000's explanation and question.");
            ChooseSeasonsVasu(session, app, 1);
            FailIf(session.Dialogue.SourceTextId != 0x303a, "Vasu must repeat TX_303a when Link does not understand.");
            ChooseSeasonsVasu(session, app, 0);
            FailIf(session.Dialogue.SourceTextId != 0x303b, "Vasu must offer the source L-1 Ring Box text.");
            CloseSeasonsVasuText(session, app); app.Step(2, Vector2.Zero, batched: batched);
            FailIf(session.Vasu.Stage != EventStage.VasuRingBoxReward || session.Inventory.RingBoxLevel != 1 ||
                !session.Link.IsHoldingItemTwoHands || session.Save.HasGlobalFlag(8),
                "Vasu must grant one two-hand Ring Box before committing his tutorial flag.");
            CloseSeasonsVasuText(session, app);
            FailIf(session.Dialogue.SourceTextId != 0x303f, "Vasu must introduce the Friendship Ring after the box.");
            CloseSeasonsVasuText(session, app); app.Step(2, Vector2.Zero, batched: batched);
            FailIf(session.Inventory.UnappraisedRingCount != 1 || session.Inventory.UnappraisedRingAt(0) != 0x40 ||
                !session.Link.IsHoldingItemOneHand, "Vasu must grant unidentified Friendship Ring $00 through its one-hand treasure owner.");
            CloseSeasonsVasuText(session, app);
            FailIf(session.Dialogue.SourceTextId != 0x3033, "Vasu must hand off to the mandatory appraisal explanation.");
            CloseSeasonsVasuText(session, app);
            FailIf(session.Vasu.Stage != EventStage.VasuFirstAppraisalMenu || !session.RingMenu.IsActive,
                "The tutorial must open the actual appraisal menu.");
            int wallet = session.Inventory.Rupees;
            AppraiseSeasonsVasuRing(session, app, batched);
            FailIf(session.Inventory.Rupees != wallet || session.Inventory.RingsAppraised != 1 ||
                !session.Inventory.HasAppraisedRing(0) || session.Inventory.UnappraisedRingCount != 0,
                "The tutorial appraisal must be free and move Friendship Ring $00 into the owned list exactly once.");
            app.Step(9, Vector2.Zero, batched: batched);
            FailIf(session.Vasu.Stage != EventStage.VasuFirstAppraisalWait || session.Vasu.Counter != 1,
                "The post-appraisal script must retain its wait through update nine.");
            app.Step(1, Vector2.Zero);
            FailIf(session.Dialogue.SourceTextId != 0x3013, "Update ten must select the ring-list introduction.");
            CloseSeasonsVasuText(session, app); app.Step(22, Vector2.Zero, batched: batched);
            FailIf(!session.RingMenuScreen.Visible || session.RingMenuScreen.Mode != RingMenuMode.List,
                "The tutorial must open the actual ring-list presentation.");
            PressSeasonsVasu(app); app.Step(1, Vector2.Zero); PressSeasonsVasu(app);
            FailIf(session.Inventory.RingAt(0) != 0, "Ring-list input must put Friendship Ring $00 into the one-slot Ring Box.");
            app.Step(1, Vector2.Zero); PressSeasonsVasu(app, "item");
            for (int update = 0; session.RingMenu.IsActive && update < 60; update++) app.Step(1, Vector2.Zero);
            app.Step(10, Vector2.Zero, batched: batched);
            FailIf(session.Dialogue.SourceTextId != 0x3008, "The tutorial list must return to TX_3008 after its ten-update wait.");
            CloseSeasonsVasuText(session, app);
            FailIf(session.Vasu.HasState || !session.Save.HasGlobalFlag(8) || (session.Save.ReadWramByte(0xc615) & 1) == 0,
                "Finishing the tutorial must release control and commit flag $08 and wObtainedRingBox bit zero.");
            PressSeasonsVasu(app);
            FailIf(session.Vasu.Stage != EventStage.VasuNormalMenu || session.Dialogue.SourceTextId != 0x3003,
                "Repeat talk must use the normal menu without granting another box or ring.");
            ChooseSeasonsVasu(session, app, 1); app.Step(22, Vector2.Zero, batched: batched);
            session.DebugTravel(0, 0xe8);
            FailIf(session.Vasu.HasState || session.RingMenu.IsActive || session.Menus!.Pause.IsLeased || session.Dialogue.IsOpen,
                "Replacing the shop during a ring list must cancel its menu, callback, dialogue and pause.");
            app.Step(20, Vector2.Zero, batched: batched);
            FailIf(session.Vasu.HasState || session.Inventory.RingsAppraised != 1 || session.Inventory.RingAt(0) != 0,
                "A cancelled shop must not resume its callback or lose the committed live Ring Box contents.");
        });
    }

    private void ValidateSeasonsVasuSnakes()
    {
        foreach (bool batched in new[] { false, true }) WithSeasonsVasu(batched, (session, app) =>
        {
            session.Save.WriteField("wFileIsHeroGame", 1);
            var books = session.Entities.Entities<NpcCharacter>().Where(npc => npc.Record.Id == 0xe5).ToArray();
            FailIf(books.Length != 2 || books[0].Record.Palette != 1 || books[1].Record.Palette != 2 ||
                books.Any(npc => session.Entities.BlocksLink(npc.Position)),
                "Seasons books must retain their native palettes and nonblocking six-pixel A radii.");
            for (int subid = 0; subid < 2; subid++)
            {
                ApproachSeasonsVasu(session, app, 0xe5, subid); PressSeasonsVasu(app);
                FailIf(session.Dialogue.SourceTextId != (subid == 0 ? 0x3020 : 0x3019),
                    "Each book must dispatch its native source script through the shop's A-button route.");
                ChooseSeasonsVasu(session, app, 1);
                FailIf(session.Vasu.HasState, "Declining a book must return it to its repeatable A-button wait.");
                PressSeasonsVasu(app); ChooseSeasonsVasu(session, app, 1);
            }
            ApproachSeasonsVasu(session, app, 0x89, 6); PressSeasonsVasu(app);
            FailIf(session.Vasu.Stage != EventStage.RedInitial || session.Dialogue.SourceTextId != 0x3009,
                "Standard Seasons must select the red snake's pre-linked script.");
            FailIf(session.DisabledObjects != 1 || session.Memory.ReadField("wMenuDisabled") != 1,
                "Snake state1 must publish its direct $01 object/menu masks.");
            ChooseSeasonsVasu(session, app, 0);
            Vector2 anchor = session.Link.PrecisePosition;
            app.Step(29, Vector2.Down, batched: batched);
            FailIf(session.Vasu.Counter != 1 || session.Link.PrecisePosition != anchor || session.Dialogue.IsOpen,
                "The red snake must hold Link through the first 29 updates of its source wait 30.");
            app.Step(1, Vector2.Zero);
            FailIf(session.Dialogue.SourceTextId != 0x300a, "Red snake wait 30 must open its appraisal/box topics on update 30.");
            ChooseSeasonsVasu(session, app, 0);
            FailIf(session.Dialogue.SourceTextId != 0x300b, "Red snake appraisal instructions must resolve TX_300b's text jump.");
            ChooseSeasonsVasu(session, app, 1); app.Step(32, Vector2.Zero, batched: batched);
            FailIf(session.Vasu.HasState, "Red snake retreat must restore its idle animation and A-button sensitivity.");
            PressSeasonsVasu(app); ChooseSeasonsVasu(session, app, 1); app.Step(32, Vector2.Zero, batched: batched);
            ApproachSeasonsVasu(session, app, 0x89, 1); PressSeasonsVasu(app);
            FailIf(session.Dialogue.SourceTextId != 0x301f, "Standard Seasons blue snake must offer its native Ring Fortune prompt.");
            ChooseSeasonsVasu(session, app, 0);
            FailIf(session.Dialogue.SourceTextId != 0x300e, "The blue snake must show the fortune cable setup text.");
            ChooseSeasonsVasu(session, app, 0);
            app.Step(511, Vector2.Zero, batched: batched);
            FailIf(session.Vasu.Counter != 1 || session.Dialogue.IsOpen,
                "The blue snake's no-cable counter must retain $0001 after update 511.");
            app.Step(1, Vector2.Zero);
            FailIf(session.Dialogue.SourceTextId != 0x300f, "The blue snake must report no cable on update $0200.");
            CloseSeasonsVasuText(session, app); app.Step(32, Vector2.Zero, batched: batched);
            FailIf(session.Vasu.HasState || session.Menus!.Pause.IsLeased, "Blue snake timeout must return control and permit another conversation.");
            PressSeasonsVasu(app); ChooseSeasonsVasu(session, app, 1);
            CloseSeasonsVasuText(session, app); app.Step(32, Vector2.Zero, batched: batched);
        });
    }

    private void ValidateSeasonsVasuRingSecrets()
    {
        var source = OracleSaveData.CreateStandardGame(OracleGame.Ages);
        source.WriteField("wGameID", 0x34); source.WriteField("wGameID", 0x12, 1);
        byte[] rings = [1,2,4,8,16,32,64,128];
        for (int index = 0; index < 8; index++) source.WriteWramByte(0xc616 + index, rings[index]);
        byte[] secret = new LinkedGameNpcDatabase(AgesGameContent.AssetRoot).GenerateRingSecretValues(source);
        FailIf(!secret.SequenceEqual(new byte[] { 0x1d,0x28,0x3f,0x33,0x09,0x2d,0x3b,0x20,0,0x16,0x3f,0x3a,0x28,0x11,0x3e }),
            "The source ring-secret vector must retain native field order, checksum and GameID-selected XOR.");
        foreach (bool batched in new[] { false, true }) WithSeasonsVasu(batched, (session, app) =>
        {
            session.Inventory.GiveTreasure(TreasureId.RingBox, 1);
            session.Inventory.GrantAppraisedRingForDebug(7);
            if (batched) session.Save.WriteField("wFileIsHeroGame", 1);
            else session.Save.SetGlobalFlag(0x28);
            session.Save.WriteField("wGameID", 0x34); session.Save.WriteField("wGameID", 0x12, 1);
            ApproachSeasonsVasu(session, app, 0x89, 6); PressSeasonsVasu(app);
            FailIf(session.Dialogue.SourceTextId != 0x3018,
                "Seasons flag $28 or a Hero file, together with a Ring Box, must select the red snake's ring-secret service.");
            ChooseSeasonsVasu(session, app, 1);
            string generated = session.Dialogue.CurrentMessage;
            FailIf(!session.Save.HasGlobalFlag(SeasonsGlobalFlag.RingSecretGenerated) || generated.Contains("\\secret", StringComparison.Ordinal),
                "The red snake must render its own fifteen-symbol Seasons secret and mark its native generated-secret flag.");
            ChooseSeasonsVasu(session, app, 0);
            FailIf(session.Dialogue.CurrentMessage != generated, "Repeating a generated ring secret must retain its contents.");
            ChooseSeasonsVasu(session, app, 1); CloseSeasonsVasuText(session, app);
            app.Step(32, Vector2.Zero, batched: batched);
            PressSeasonsVasu(app); ChooseSeasonsVasu(session, app, 0);
            app.Step(22, Vector2.Zero, batched: batched);
            FailIf(!session.RingSecrets.IsActive || session.RingSecrets.Screen is null,
                "Telling the red snake a secret must open the selected-pack MENU_SECRET owner.");
            session.RingSecrets.Submit(new byte[15]);
            FailIf(!session.RingSecrets.IsActive || session.Inventory.HasAppraisedRing(0),
                "An invalid ring secret must retain the menu without changing ring ownership.");
            app.Step(16, Vector2.Zero, batched: batched); PressSeasonsVasu(app);
            session.RingSecrets.Submit(secret);
            for (int update = 0; session.RingSecrets.IsActive && update < 60; update++) app.Step(1, Vector2.Zero);
            FailIf(session.Vasu.Stage != EventStage.RedSecretInputWait || session.Vasu.Counter != 1,
                "MENU_SECRET close must preserve the script's wait 1 on the callback update.");
            app.Step(1, Vector2.Zero);
            FailIf(session.Dialogue.SourceTextId != 0x3027 ||
                session.Save.ReadWramByte(0xc616) != 0x81 ||
                Enumerable.Range(1, 7).Any(index => session.Save.ReadWramByte(0xc616 + index) != rings[index]),
                "A valid Ages ring secret must merge all eight ring bytes into Seasons without discarding owned ring $07.");
            CloseSeasonsVasuText(session, app); app.Step(32, Vector2.Zero, batched: batched);
            PressSeasonsVasu(app); ChooseSeasonsVasu(session, app, 0);
            app.Step(22, Vector2.Zero, batched: batched);
            session.DebugTravel(0, 0xe8);
            FailIf(session.Vasu.HasState || session.RingSecrets.IsActive || session.Menus!.Pause.IsLeased,
                "Room replacement must cancel the red snake's secret menu and completion callback.");
            app.Step(20, Vector2.Zero, batched: batched);
            FailIf(session.Dialogue.IsOpen || session.Save.ReadWramByte(0xc616) != 0x81,
                "A cancelled secret callback must not reopen dialogue or discard a completed ring merge.");
        });
    }

    private void ValidateSeasonsVasuCancellation()
    {
        foreach (bool batched in new[] { false, true }) WithSeasonsVasu(batched, (session, app) =>
        {
            ApproachSeasonsVasu(session, app); PressSeasonsVasu(app);
            ChooseSeasonsVasu(session, app, 0); CloseSeasonsVasuText(session, app);
            app.Step(2, Vector2.Zero, batched: batched);
            FailIf(session.Inventory.RingBoxLevel != 1 || !session.Link.IsHoldingItemTwoHands,
                "Cancellation fixture must reach the real held Ring Box grant.");
            session.DebugTravel(0, 0xe8);
            app.Step(32, Vector2.Zero, batched: batched);
            FailIf(session.Vasu.HasState || session.Dialogue.IsOpen || session.Link.IsHoldingItemTwoHands ||
                session.DisabledObjects != 0 || session.Memory.ReadField("wMenuDisabled") != 0 ||
                session.Menus!.Pause.IsLeased || session.Save.HasGlobalFlag(8) ||
                session.Inventory.RingBoxLevel != 1 || session.Inventory.UnappraisedRingCount != 0,
                "Replacing a held Vasu reward must release its text, pose and control without replaying the grant or completing the tutorial.");
        });
    }

    private void ValidateSeasonsVasuAppraisal()
    {
        foreach (int kind in new[] { 0, 1, 2, 3 }) WithSeasonsVasu(kind == 0, (session, app) =>
        {
            // bank2.s: paid new, paid duplicate, insufficient funds and the hundredth appraisal.
            session.Save.SetGlobalFlag(8); session.Inventory.GiveTreasure(TreasureId.RingBox, 1);
            int wallet = kind == 2 ? 19 : 50;
            session.Inventory.AddRupees(wallet - session.Inventory.Rupees);
            session.Inventory.GiveUnappraisedRing(7);
            if (kind == 1) session.Inventory.GrantAppraisedRingForDebug(7);
            session.Save.WriteWramByte(0xc6c8, (byte)(kind == 3 ? 99 : 0));
            ApproachSeasonsVasu(session, app); PressSeasonsVasu(app);
            ChooseSeasonsVasu(session, app, 0);
            FailIf(!session.RingMenu.IsActive, "Normal Vasu appraisal must acquire the shared menu owner.");
            AppraiseSeasonsVasuRing(session, app, kind == 0);
            int expectedWallet = kind == 2 ? 19 : kind == 1 ? 60 : 30;
            int expectedCount = kind == 2 ? 0 : kind == 3 ? 100 : 1;
            FailIf(session.Inventory.Rupees != expectedWallet || session.Inventory.RingsAppraised != expectedCount ||
                session.Save.ReadWramByte(0xc6c8) != expectedCount || session.Inventory.UnappraisedRingCount != (kind == 2 ? 1 : 0) ||
                session.Inventory.HasAppraisedRing(7) != (kind != 2),
                $"Seasons appraisal kind {kind}: expected wallet/count/ownership {expectedWallet}/{expectedCount}/{kind != 2}, got {session.Inventory.Rupees}/{session.Inventory.RingsAppraised}/{session.Inventory.HasAppraisedRing(7)}.");
            app.Step(10, Vector2.Zero);
            if (kind == 3)
            {
                FailIf(session.Dialogue.SourceTextId != 0x3038 || !session.Save.HasGlobalFlag(9),
                    "Appraisal 100 must publish flag $09 and Vasu's Hundredth Ring text.");
                CloseSeasonsVasuText(session, app); app.Step(2, Vector2.Zero);
                FailIf(session.Save.HasGlobalFlag(9) || session.Inventory.UnappraisedRingAt(0) != 0x78,
                    "The Hundredth Ring reward must clear flag $09 and grant concrete ring $38 once.");
            }
            CloseSeasonsVasuText(session, app);
            FailIf(session.Vasu.HasState || session.Menus!.Pause.IsLeased,
                "Appraisal completion must release the conversation and menu owners.");
            if (kind == 0)
            {
                int[] texts = [0x3036, 0x3037, 0x3039];
                for (int reward = 0; reward < 3; reward++)
                {
                    session.Save.SetGlobalFlag(reward);
                    PressSeasonsVasu(app);
                    FailIf(session.Dialogue.SourceTextId != texts[reward] || !session.Save.HasGlobalFlag(4 + reward),
                        "Vasu's Slayer/Wealth/Victory rewards must select the native text and mark their received flag before the grant.");
                    CloseSeasonsVasuText(session, app); app.Step(2, Vector2.Zero);
                    FailIf(session.Inventory.UnappraisedRingAt(reward) != 0x74 + reward,
                        "Vasu's special reward must grant the concrete ring $34/$35/$36 once.");
                    CloseSeasonsVasuText(session, app);
                }
                PressSeasonsVasu(app); ChooseSeasonsVasu(session, app, 0);
                app.Step(22, Vector2.Zero);
                int unappraised = session.Inventory.UnappraisedRingCount;
                session.DebugTravel(0, 0xe8);
                FailIf(session.RingMenu.IsActive || session.Vasu.HasState || session.Menus.Pause.IsLeased ||
                    session.Inventory.UnappraisedRingCount != unappraised || session.Inventory.Rupees != expectedWallet,
                    "Cancelling appraisal before selection must release modal ownership without charging or consuming rings.");
            }
        });
    }
}
