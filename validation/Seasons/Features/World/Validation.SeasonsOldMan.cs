namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsOldManCave()
    {
        foreach (bool batched in new[] { false, true })
        {
            var root = StartSeasonsAfterImpa(batched);
            try
            {
                var session = root._seasonsOpening!;
                var app = new ApplicationValidationFixture(root);
                byte[] disk = root.FileSlotSnapshot(0, OracleGame.Seasons);
                // The native cave is under a burnt tree; room flag $80 restores its stairs.
                session.Save.SetRoomFlag(0, 0x97, 0x80);
                session.Inventory.GiveTreasure(TreasureId.Sword, 1);
                session.Inventory.EquipA(ItemId.Sword);
                session.DebugTravel(0, 0x97);
                session.Link.WarpTo(new(72, 56));
                FailIf(session.Collision.Collides(session.Link.Position),
                    "The $0:97 cave approach must start on actual floor east of the bush below stairs $23.");
                CutBush(Vector2I.Left);
                app.Step(16, Vector2.Left, batched: batched);
                Enter();
                var oldMan = Actor();
                FailIf(oldMan.Npc.Position != new Vector2(128, 56) || oldMan.ReadByte(0x73) != 0x1f ||
                    !oldMan.Npc.Visible || session.Entities.RoomEnemyCount != 0 ||
                    session.Entities.Entities<ItemDropProducer>().Single().Position != new Vector2(136, 120),
                    $"$5:03 must initialize INTERAC$99:$02 at XY($80,$38), text page $1f and its uncounted ENEMY$59:$05 at XY($88,$78): NPC={oldMan.Npc.Position}, text=${oldMan.ReadByte(0x73):x2}, visible={oldMan.Npc.Visible}, enemies={session.Entities.RoomEnemyCount}, drop={session.Entities.Entities<ItemDropProducer>().Single().Position}.");
                Approach(); Talk(0x1f03);
                int beforeCancellation = session.Inventory.Rupees;
                session.DebugTravel(0, 0x97);
                FailIf(session.Dialogue.IsOpen || oldMan.Script!.InputDisabled || session.DisabledObjects != 0 ||
                    session.Inventory.Rupees != beforeCancellation || session.Save.HasRoomFlag(5, 3, 0x40),
                    "Replacing the cave during TX_1f03 must cancel input/dialogue without granting money or completing the gift.");
                session.Link.WarpTo(new(72, 56)); CutBush(Vector2I.Left);
                app.Step(16, Vector2.Left, batched: batched);
                Enter(); oldMan = Actor();
                Approach();
                Talk(0x1f03);
                int wallet = session.Inventory.Rupees;
                Vector2 talkingPosition = session.Link.PrecisePosition;
                app.Step(4, Vector2.Right, batched: batched);
                FailIf(session.Link.PrecisePosition != talkingPosition || session.Inventory.Rupees != wallet ||
                    session.Save.HasRoomFlag(5, 3, 0x40),
                    "The gift must wait for TX_1f03 to close while dialogue holds Link and leaves room flag $40 clear.");
                session.Dialogue.Close();
                app.Step(1, Vector2.Zero);
                FailIf(session.Inventory.Rupees != wallet + 100 || !oldMan.Script!.InputDisabled ||
                    session.Save.HasRoomFlag(5, 3, 0x40),
                    "oldMan_giveRupees subid $02 must grant 100 once before wait 8 and the displayed-wallet gate.");
                app.Step(7, Vector2.Right, batched: batched);
                FailIf(session.Link.PrecisePosition != talkingPosition || session.Save.HasRoomFlag(5, 3, 0x40),
                    "The old man must retain input ownership and delay flag $40 through the reward wait.");
                int updates = 0;
                while (!session.Save.HasRoomFlag(5, 3, 0x40) && updates++ < 240)
                    app.Step(1, Vector2.Zero);
                FailIf(!session.Save.HasRoomFlag(5, 3, 0x40) || !oldMan.Script!.InputDisabled ||
                    session.StatusBar.DisplayedRupees != wallet + 100,
                    "The old man must commit room flag $40 after the displayed wallet catches up; orroomflag yields before enableinput.");
                app.Step(1, Vector2.Zero);
                FailIf(oldMan.Script.InputDisabled || session.DisabledObjects != 0,
                    "The update after orroomflag must execute enableinput and release Link.");
                Talk(0x1f07);
                session.Dialogue.Close(); app.Step(2, Vector2.Zero, batched: batched);
                FailIf(session.Inventory.Rupees != wallet + 100, "Repeat talk must not grant another 100 rupees.");
                Exit();
                Enter();
                Approach(); Talk(0x1f07);
                session.Dialogue.Close(); app.Step(2, Vector2.Zero, batched: batched);
                FailIf(session.Inventory.Rupees != wallet + 100 || !session.Save.HasRoomFlag(5, 3, 0x40),
                    "Re-entering $5:03 must retain the reward flag and select the completed dialogue.");
                FailIf(!root.FileSlotSnapshot(0, OracleGame.Seasons).SequenceEqual(disk),
                    "The cave visit must not write the file slot without an explicit save.");

                NativeNpcRoomEntity Actor() => session.Entities.EntityAdapters<NativeNpcRoomEntity>()
                    .Single(actor => !actor.Finished && actor.Npc.Record.Id == 0x99);
                void Enter()
                {
                    int updates = 0;
                    while (!session.Transitions.IsTransitioning && updates++ < 120) app.Step(1, Vector2.Up);
                    FailIf(!session.Transitions.IsTransitioning, "$0:97's original doorway must initiate the cave warp.");
                    app.Step(160, Vector2.Zero, batched: batched);
                    // warpSources.s:group0WarpSources $97 -> group5WarpDestTable $8c.
                    FailIf(session.Rooms.ActiveGroup != 5 || session.RoomId != 3 || session.Transitions.IsTransitioning ||
                        session.Room.Width != 240 || session.Room.Height != 176 || session.Rooms.CurrentDungeonIndex != -1 ||
                        session.Link.Position != new Vector2I(184, 120),
                        $"Cave entry must resolve $5:03, packed destination $7b: group={session.Rooms.ActiveGroup}, room=${session.RoomId:x2}, XY={session.Link.Position}.");
                    app.Step(2, Vector2.Zero, batched: batched);
                }
                void Approach()
                {
                    // Walk from the actual warp arrival, outside the old man's collision box.
                    app.Step(48, Vector2.Up, batched: batched);
                    int updates = 0;
                    while (session.Link.Position.X > 128 && updates++ < 90) app.Step(1, Vector2.Left);
                    while (Actor().FindTalkTarget(session.Link) is null && updates++ < 150) app.Step(1, Vector2.Up);
                    FailIf(Actor().FindTalkTarget(session.Link) is null || Actor().Npc.BlocksLinkCenter(session.Link.PrecisePosition),
                        $"The old man must be reachable through the cave floor outside his hitbox: Link={session.Link.Position}.");
                }
                void Talk(int text)
                {
                    app.Step(1, Vector2.Zero, ["attack"], ["attack"]);
                    for (int update = 0; update < 4 && !session.Dialogue.IsOpen; update++) app.Step(1, Vector2.Zero);
                    FailIf(!session.Dialogue.IsOpen || session.Dialogue.SourceTextId != text,
                        $"INTERAC$99:$02 must show TX_{text:x4}, got {session.Dialogue.SourceTextId:x4}.");
                }
                void CutBush(Vector2I direction)
                {
                    app.Step(2, Vector2.Zero, batched: batched);
                    session.Link.Face(direction);
                    app.Step(1, Vector2.Zero, ["attack"], ["attack"]);
                    app.Step(24, Vector2.Zero, batched: batched);
                    FailIf(session.Room.GetPackedStorageMetatile(0x33) != 0x04,
                        $"The real sword caller must clear bush $c4 at $33 before approaching or leaving the cave stairs: tile=${session.Room.GetPackedStorageMetatile(0x33):x2}, Link={session.Link.Position}, mask=${session.DisabledObjects:x2}.");
                }
                void Exit()
                {
                    app.Step(48, Vector2.Down, batched: batched);
                    int updates = 0;
                    while (!session.Transitions.IsTransitioning && session.Link.Position.X < 184 && updates++ < 90) app.Step(1, Vector2.Right);
                    while (!session.Transitions.IsTransitioning && updates++ < 180) app.Step(1, Vector2.Down);
                    FailIf(!session.Transitions.IsTransitioning, "$5:03's doorway must initiate the original return warp.");
                    app.Step(160, Vector2.Zero, batched: batched);
                    // group5WarpSources $03 -> group0WarpDestTable $6b ($97/$23).
                    FailIf(session.Rooms.ActiveGroup != 0 || session.RoomId != 0x97 || session.Transitions.IsTransitioning ||
                        session.Link.Position != new Vector2I(56, 40),
                        "The cave exit must return to $0:97 XY($38,$28).");
                    CutBush(Vector2I.Down);
                    app.Step(16, Vector2.Down, batched: batched);
                }
            }
            finally { root.Free(); }
        }
    }
}
