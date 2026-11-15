namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsDance()
    {
        foreach (bool batched in new[] { false, true })
        {
            var root = StartSeasonsAfterImpa(batched);
            try
            {
                var s = root._seasonsOpening!;
                var app = new ApplicationValidationFixture(root);
                string turningAnimation = new SeasonsTalkNpcDatabase().Animation(0x6a, 4);
                byte[] disk = root.FileSlotSnapshot(0, OracleGame.Seasons);
                EnterSeasonsDance(s, app, batched);
                var actors = s.Entities.EntityAdapters<SeasonsDanceRoomEntity>().ToArray();
                FailIf(actors.Length != 10 || actors.Count(actor => actor.ReadByte(0x42) == 1) != 1 ||
                    actors.Count(actor => actor.ReadByte(0x42) == 2) != 9,
                    "$1:95 must parse objectData7e6c into the leader and nine live native dancer slots.");
                var facingDancer = actors.Single(actor => actor.ReadByte(0x42) == 2 && actor.Npc.Position == new Vector2(104, 24));
                FailIf(facingDancer.Npc.NativeAngle != 8 || facingDancer.Npc.CurrentScriptAnimationSource.Length != 0,
                    "npcFaceLinkAndAnimate must replace the dancer's initial script pose with its right-facing animation near the actual $3:95 arrival.");
                for (int visit = 0; visit < 2; visit++)
                {
                    if (visit != 0) EnterSeasonsDance(s, app, batched);
                    var leader = s.Entities.EntityAdapters<SeasonsDanceRoomEntity>().Single(actor => actor.ReadByte(0x42) == 1);
                    // Begin on real open floor southeast of the leader, then walk into talk range.
                    s.Link.WarpTo(new(136, 104)); s.Link.Face(Vector2I.Up);
                    int guard = 0;
                    while (s.Entities.FindTalkTarget(s.Link) != leader.Npc && guard++ < 120) app.Step(1, Vector2.Up);
                    FailIf(s.Entities.FindTalkTarget(s.Link) != leader.Npc || s.Collision.Collides(s.Link.Position) ||
                        leader.Npc.BlocksLinkCenter(s.Link.Position),
                        $"Dance leader must be reachable on $1:95's actual collision geometry; Link={s.Link.Position}.");
                    app.Step(1, Vector2.Zero, ["attack"], ["attack"]);
                    FailIf(!s.Dialogue.IsOpen || s.Dialogue.SourceTextId != (visit == 0 ? 0x0100 : 0x0101),
                        "Dance leader must retain first/repeat TX_0100/TX_0101.");
                    s.Dialogue.SubmitChoiceForValidation(0);
                    bool tutorial = visit == 0;
                    var tutorialTexts = new HashSet<int>();
                    var turningFrames = new HashSet<int>();
                    for (guard = 0; guard < 1800 && !s.Link.SubrosianDanceActive; guard++)
                    {
                        if (s.Dialogue.IsOpen)
                        {
                            int text = s.Dialogue.SourceTextId ?? -1; tutorialTexts.Add(text);
                            if (text == 0x0115) s.Dialogue.SubmitChoiceForValidation(tutorial ? 1 : 0);
                            else if (text == 0x0108) s.Dialogue.SubmitChoiceForValidation(0);
                            else s.Dialogue.Close();
                        }
                        app.Step(1, Vector2.Zero);
                        if (leader.Npc.CurrentScriptAnimationSource == turningAnimation)
                        {
                            turningFrames.Add(leader.Npc.CurrentAnimationFrame);
                            FailIf(!leader.Npc.Visible || leader.Npc.CurrentAnimationOpaquePixels == 0,
                                $"Dance instructor animation$04/frame{leader.Npc.CurrentAnimationFrame} must retain visible source pixels while turning onto the table.");
                        }
                    }
                    FailIf(!turningFrames.SetEquals(Enumerable.Range(0, 6)),
                        "The instructor's actual table hop must exercise all six source turning frames, including its held final pose.");
                    FailIf(!s.Link.SubrosianDanceActive || leader.ReadByte(0x44) != 4 || s.DisabledObjects != 0x91 ||
                        !s.Save.HasRoomFlag(3, 0x95, 0x80),
                        $"Dance tutorial/start did not hand off to SPECIALOBJECT$01; state=${leader.ReadByte(0x44):x2}/${leader.ReadByte(0x45):x2}, mask=${s.DisabledObjects:x2}, text={s.Dialogue.SourceTextId:x4}.");
                    FailIf(tutorial && !new[] { 0x0105, 0x0106, 0x0107, 0x0108, 0x0109 }.All(tutorialTexts.Contains),
                        "The optional dance tutorial must complete all three native demonstrations and its repeat prompt.");
                    bool won = false;
                    for (guard = 0; guard < 9000 && !s.Dialogue.IsOpen; guard++)
                    {
                        int keys = DanceAnswerKeys(s, leader, wrong: visit == 1);
                        app.Step(1, DanceDirection(keys), MenuRomActions(keys), MenuRomActions(keys));
                        if (s.Memory.ReadWramByte(0xcfd0) == 1) won = true;
                    }
                    int expectedText = visit == 0 ? 0x010a : 0x0104;
                    FailIf(!s.Dialogue.IsOpen || s.Dialogue.SourceTextId != expectedText || won != (visit == 0),
                        $"Dance {(visit == 0 ? "win" : "wrong-step failure")} did not follow its native branch; text=TX_{s.Dialogue.SourceTextId:x4}, rounds=${s.Memory.ReadWramByte(0xcfd5):x2}, state=${leader.ReadByte(0x44):x2}/${leader.ReadByte(0x45):x2}, missed=${s.Memory.ReadWramByte(0xcfdd):x2}.");
                    s.Dialogue.Close();
                    bool rewardText = false;
                    for (guard = 0; guard < 800 && (s.RoomId == 0x95 || s.Transitions.IsTransitioning); guard++)
                    {
                        if (s.Dialogue.IsOpen)
                        {
                            FailIf(s.Dialogue.SourceTextId != 0x0022, $"First dance reward must show the Seasons boomerang text TX_0022; actual TX_{s.Dialogue.SourceTextId:x4}, wins={s.Save.ReadField("wNumTimesPlayedSubrosianDance")}, boomerang={s.Inventory.BoomerangLevel}.");
                            rewardText = true; s.Dialogue.Close();
                        }
                        app.Step(1, Vector2.Zero);
                    }
                    FailIf(s.Rooms.ActiveGroup != 1 || s.RoomId != 0x24 || s.DisabledObjects != 0 || s.Link.SubrosianDanceActive ||
                        s.Memory.ReadField("wMenuDisabled") != 0 || s.Save.ReadField("wNumTimesPlayedSubrosianDance") != 1 ||
                        s.Inventory.BoomerangLevel != 1 || rewardText != (visit == 0),
                        $"Dance exit must restore $1:24 with one persisted boomerang win; room=${s.Rooms.ActiveGroup:x1}:{s.RoomId:x2}, mask=${s.DisabledObjects:x2}, menu=${s.Memory.ReadField("wMenuDisabled"):x2}, wins={s.Save.ReadField("wNumTimesPlayedSubrosianDance")}, boomerang={s.Inventory.BoomerangLevel}.");
                }
                s.Inventory.EquipA(TreasureId.Boomerang); s.Inventory.EquipB(0);
                s.Link.WarpTo(new(72, 24)); s.Link.Face(Vector2I.Up);
                app.Step(1, Vector2.Zero, ["attack"], ["attack"]);
                bool threw = s.Entities.Entities<BoomerangItem>().Any();
                app.Step(10, Vector2.Zero, batched: batched, afterUpdate: () => threw |= s.Entities.Entities<BoomerangItem>().Any());
                FailIf(!threw, $"The dance-earned boomerang must allocate its shared Seasons projectile from A input; Link state${s.Memory.ReadWramByte(0xd004):x2}, mask${s.DisabledObjects:x2}, menu${s.Memory.ReadField("wMenuDisabled"):x2}, XY{s.Link.Position}, parent={s.Link.IsUsingBoomerang}.");
                for (int guard = 0; s.Entities.Entities<BoomerangItem>().Any() && guard < 240; guard++) app.Step(1, Vector2.Zero);
                FailIf(s.Entities.Entities<BoomerangItem>().Any() || s.Link.IsUsingBoomerang,
                    "The dance-earned boomerang must return and release its shared parent/child allocations.");
                FailIf(!OracleSaveData.TryDeserialize(s.Save.Serialize(), OracleGame.Seasons, out var saved) ||
                    !saved!.HasTreasure(TreasureId.Boomerang) || saved.ReadField("wNumTimesPlayedSubrosianDance") != 1 ||
                    !saved.HasRoomFlag(3, 0x95, 0x80) || !root.FileSlotSnapshot(0, OracleGame.Seasons).SequenceEqual(disk),
                    "Dance completion must survive explicit serialization and leave disk untouched until an explicit save.");
                // Cancellation while the native Link handler owns movement must release both owners.
                EnterSeasonsDance(s, app, batched);
                var last = s.Entities.EntityAdapters<SeasonsDanceRoomEntity>().Single(actor => actor.ReadByte(0x42) == 1);
                last.WriteByte(0x44, 3); last.WriteByte(0x45, 2);
                app.Step(2, Vector2.Zero, batched: batched);
                s.DebugTravel(0, 0xb6); app.Step(2, Vector2.Zero, batched: batched);
                FailIf(s.Link.SubrosianDanceActive || s.Dialogue.IsOpen || s.Transitions.NativePaletteFadeActive ||
                    s.DisabledObjects != 0 || s.Memory.ReadField("wMenuDisabled") != 0,
                    "Cancelling the dance must release Link, owned palette work, dialogue and native control bytes.");
            }
            finally { root.Free(); }
        }
    }
    private static void EnterSeasonsDance(SeasonsOpeningSession s, ApplicationValidationFixture app, bool batched)
    {
        s.DebugTravel(1, 0x24); s.Link.WarpTo(new(72, 8));
        int guard = 0;
        while (!s.Transitions.IsTransitioning && guard++ < 90) app.Step(1, Vector2.Down);
        FailIf(!s.Transitions.IsTransitioning, "The actual $1:24 doorway must allow entering the dance hall.");
        app.Step(160, Vector2.Zero, batched: batched);
        FailIf(s.Rooms.ActiveGroup != 3 || s.RoomId != 0x95 || s.Transitions.IsTransitioning,
            "The actual Subrosian dance hall entrance must load $3:95's native object stream.");
    }
    private static Vector2 DanceDirection(int keys) => (keys & 0x10) != 0 ? Vector2.Right : (keys & 0x20) != 0 ? Vector2.Left : Vector2.Zero;
    private static int DanceAnswerKeys(SeasonsOpeningSession s, SeasonsDanceRoomEntity leader, bool wrong)
    {
        int count = s.Memory.ReadWramByte(0xcfd9);
        if (leader.ReadByte(0x44) != 4 || leader.ReadByte(0x45) != 3 || count >= 3 ||
            s.Memory.ReadWramByte(0xcfcb) <= count || s.Memory.ReadWramByte(0xd004) != 1 ||
            s.Memory.ReadField("wGameKeysPressed") != 0) return 0;
        int move = s.Memory.ReadWramByte(0xcfc8 + count);
        if (wrong) move = (move + 1) % 3;
        return move == 0 ? 0x10 : move == 1 ? 0x20 : 1;
    }
}
