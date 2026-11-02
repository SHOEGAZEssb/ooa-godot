
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedSideScrollBubbles(FrontendValidationRoot root, bool batched)
    {
        var rooms = root._gameplaySession!.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player;
        var memory = rooms.RuntimeState;
        var app = new ApplicationValidationFixture(root);
        var game = rooms.SaveData.Game;
        var initialRandom = entities.Random.CaptureState();
        int originalGroup = rooms.ActiveGroup, originalRoom = rooms.CurrentRoom.Id;
        void Load(int group, int room)
        {
            if (game == OracleGame.Ages) root.LoadDebugRoom(group, room);
            else root._seasonsOpening!.DebugTravel(group, room);
            entities.Clear();
        }
        try
        {
            Load(game == OracleGame.Ages ? 7 : 6, game == OracleGame.Ages ? 0x07 : 0x01);
            player.BeginCutsceneControl();
            foreach (byte tile in new byte[] { 0x1b, 0, 0x0d })
            foreach (int randomByte in new[] { 0, 1 })
            {
                entities.Clear();
                var room = rooms.CurrentRoom;
                for (int y = 0; y < room.HeightInTiles; y++)
                for (int x = 0; x < room.WidthInTiles; x++)
                    room.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), tile, 0, 0);
                var seed = entities.Random.CaptureState() with { Rng1 = (byte)randomByte };
                entities.Random.RestoreState(seed);
                var actor = entities.Spawn<SideScrollBubbleRoomEntity>(new SideScrollBubbleSpawn(new(40.75f, 56.5f)));
                FailIf(actor.PrecisePosition != new Vector2(40, 56), $"{game}: INTERAC$91 must copy coordinate high bytes only.");
                var native = new BraceletRom(room, player.PrecisePosition, 2, 0xff, game: game);
                native[0xff94] = seed.Rng1; native[0xff95] = seed.Rng2;
                int at = 0xd040 + entities.InteractionSlot(actor) * 256;
                native[at] = 1; native[at + 1] = 0x91; native[at + 11] = 56; native[at + 13] = 40;
                int updates = 0;
                string context = $"{game}: INTERAC$91 tile ${tile:x2}, RNG${randomByte:x2}, batch={batched}";
                void Step(int count)
                {
                    app.Step(count, Vector2.Zero, batched: batched, afterUpdate: () =>
                    {
                        native.AdvanceInteractions(entities.FrameCounter); updates++;
                        FailIf(actor.Finished != (native[at] == 0) || !actor.Finished &&
                            (actor.PrecisePosition != new Vector2(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f) ||
                             actor.Angle != native[at + 9] || actor.TurnCounter != native[at + 6] ||
                             actor.TurnsRemaining != native[at + 0x31] || actor.Visible != ((native[at + 0x1a] & 128) != 0)),
                            context + $": update {updates}: native movement, turns, visibility or deletion differs.");
                        var rng = entities.Random.CaptureState();
                        FailIf(rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95] || rng.Calls != seed.Calls + native.RandomCalls,
                            context + $": update {updates}: initialization RNG admission differs.");
                    });
                }
                memory.SetField("wDisabledObjects", 2); native[0xcc8a] = 2;
                Step(1);
                bool wet = tile == 0x1b || game == OracleGame.Seasons && tile == 0x0d;
                FailIf(actor.Finished == wet || wet && !actor.Visible,
                    context + ": native state zero bypasses the interaction mask; only matching hazard rows survive.");
                Vector2 initialized = actor.PrecisePosition;
                Step(3);
                FailIf(actor.PrecisePosition != initialized, context + ": initialized bubbles must freeze under mask $02.");
                memory.SetField("wDisabledObjects", 0); native[0xcc8a] = 0;
                Step(37);
                if (wet)
                {
                    FailIf(actor.Finished, context + ": the native turn cycle must still be live before deletion.");
                    room.SetPositionTileAndCollision(actor.Position, 0, 0, 0);
                    native.SetTile((byte)room.GetPackedPosition(actor.Position), 0);
                    Step(1);
                    FailIf(!actor.Finished, context + ": leaving water must delete before the next movement.");
                }
                Step(2);
            }
        }
        finally
        {
            memory.SetField("wDisabledObjects", 0);
            player.EndCutsceneControl(); Load(originalGroup, originalRoom);
            entities.Random.RestoreState(initialRandom);
        }
    }
}
