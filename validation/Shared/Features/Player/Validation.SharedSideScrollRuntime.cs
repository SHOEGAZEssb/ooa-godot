
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedSideScrollRuntime(FrontendValidationRoot root, bool batched)
    {
        var rooms = root._gameplaySession!.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player;
        var inventory = player.Inventory;
        var memory = rooms.RuntimeState;
        var game = rooms.SaveData.Game;
        var initialRandom = entities.Random.CaptureState();
        OracleRandomState seed = default;
        var app = new ApplicationValidationFixture(root);
        int originalGroup = rooms.ActiveGroup, originalRoom = rooms.CurrentRoom.Id;
        var transitions = root._gameplaySession.Transitions;
        var disabled = transitions.ScreenTransitionsDisabledSource;
        BraceletRom native = null!;
        string context = "";
        int updates = 0;
        void Load(int group, int room)
        {
            if (game == OracleGame.Ages) root.LoadDebugRoom(group, room);
            else root._seasonsOpening!.DebugTravel(group, room);
            entities.Clear();
        }
        void Prepare(string scenario, bool ladder = false, bool water = false)
        {
            Load(game == OracleGame.Ages ? 7 : 6, game == OracleGame.Ages ? 0x07 : 0x01);
            var room = rooms.CurrentRoom;
            FailIf(!player.World.SideScrolling || room.Width != 240 || room.Height != 176 ||
                room.AssetRoot != rooms.AssetRoot || game == OracleGame.Seasons && rooms.CurrentDungeonIndex != 0,
                $"{game}: native side-view group aliases must bind large geometry and dungeon metadata through the live room owner.");
            inventory.EquipA(0); inventory.EquipB(0); inventory.SetScriptedActiveRing(0xff);
            for (int y = 0; y < room.HeightInTiles; y++)
            for (int x = 0; x < room.WidthInTiles; x++)
                room.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8),
                    (byte)(water ? 0x1b : ladder && x == 5 ? 0x16 : y == 6 ? 0x26 : 0),
                    (byte)(!water && !ladder && y == 6 ? 15 : 0), 0);
            player.WarpTo(new(88.25f, ladder ? 72.5f : 89.5f)); player.Face(Vector2I.Down);
            native = new BraceletRom(room, player.PrecisePosition, 2, 0xff, game: game);
            native.LoadSave(rooms.SaveData); native.InitializeWalkingAnimation();
            native[0xd009] = unchecked((byte)player.SideScrollAngle);
            seed = entities.Random.CaptureState(); native[0xff94] = seed.Rng1; native[0xff95] = seed.Rng2;
            native[0xcc2d] = (byte)rooms.ActiveGroup; native[0xcc2e] = 1;
            native[0xcd00] = 1; native[0xcc91] = 1;
            native[0xcd0c] = (byte)(room.Width - 6); native[0xcd0d] = (byte)(room.Height - 7);
            context = $"{game}: side-view {scenario}, batch={batched}"; updates = 0;
        }
        void Step(int count, Vector2 movement, bool jump = false, bool burst = false)
        {
            int keys = movement == Vector2.Up ? 0x40 : movement == Vector2.Right ? 0x10 :
                movement == Vector2.Down ? 0x80 : movement == Vector2.Left ? 0x20 : 0;
            app.Step(count, movement, held: burst ? ["attack"] : jump ? ["item"] : [],
                pressed: burst ? ["attack"] : jump ? ["item"] : [],
                batched: batched, afterUpdate: () =>
                {
                    native.UpdateLinkItems(entities.FrameCounter, keys | (burst ? 1 : jump ? 2 : 0), interactions: true);
                    if (native[0xcd00] != 0) native.AdvanceScreenBoundary();
                    updates++;
                    FailIf(player.PrecisePosition != new Vector2(native.Word(0xd00c) / 256f, native.Word(0xd00a) / 256f) ||
                        player.SideScrollAirborne != (native[0xcc5c] != 0) ||
                        player.SideScrollSwimmingState != (native[0xcc5d] & 15) ||
                        player.SideScrollSpeedZ != (short)native.Word(0xd014) ||
                        memory.ReadField("wActiveTileIndex") != native[0xcc9a] ||
                        memory.ReadField("wActiveTileType") != native[0xcc9c] ||
                        memory.ReadField("wLastActiveTileType") != native[0xcc9d],
                        context + $": update {updates}: native XY/air/retained tile publication differs; " +
                        $"runtime={player.PrecisePosition}/{player.SideScrollAirborne}/${(ushort)player.SideScrollSpeedZ:x4}, " +
                        $"native={native.Word(0xd00c):x4},{native.Word(0xd00a):x4}/{native[0xcc5c]}/${native.Word(0xd014):x4}.");
                    var rng = entities.Random.CaptureState();
                    FailIf(rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95] || rng.Calls != seed.Calls + native.RandomCalls,
                        context + $": update {updates}: shared swimming/effect RNG differs.");
                });
        }
        try
        {
            transitions.ScreenTransitionsDisabledSource = static () => true;
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Prepare($"walk/jump repeat={repeat}");
                Step(5, Vector2.Right); Step(3, Vector2.Left);
                inventory.GiveTreasure(TreasureId.Feather, 1); inventory.EquipB(TreasureId.Feather);
                native.LoadSave(rooms.SaveData);
                Step(1, Vector2.Right, jump: true); Step(40, Vector2.Zero);
                FailIf(player.SideScrollAirborne, context + ": a completed jump must return to the shared floor before repeating.");
                Prepare($"ladder repeat={repeat}", ladder: true);
                Step(8, Vector2.Up); Step(12, Vector2.Down); Step(2, Vector2.Zero);
                FailIf(!player.SideScrollClimbing, context + ": the native ladder must use the shared climbing path.");
                byte published = memory.ReadField("wActiveTileType");
                rooms.CurrentRoom.SetPositionTileAndCollision(player.PrecisePosition, 0, 0, 0);
                FailIf((byte)player.World.SideScrollActiveTiles.ActiveType != published,
                    context + ": later tile writes must not recompute the Link pass's retained active type.");
                Prepare($"swimming repeat={repeat}", water: true);
                inventory.GiveTreasure(TreasureId.Flippers, 1); native.LoadSave(rooms.SaveData);
                Step(2, Vector2.Zero); Step(4, Vector2.Right); Step(1, Vector2.Right, burst: true);
                Step(12, Vector2.Zero); Step(8, Vector2.Up);
                FailIf(player.SideScrollSwimmingState != 2, context + ": Flippers must enter the shared side-view swimming path.");
            }
            foreach (bool ladder in new[] { false, true })
            {
                Prepare($"bottom boundary ladder={ladder}");
                var room = rooms.CurrentRoom;
                for (int y = 0; y < room.HeightInTiles; y++)
                for (int x = 0; x < room.WidthInTiles; x++)
                    room.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0, 0, 0);
                player.WarpTo(new(88.25f, 160.25f));
                native.CopyRoom(room); native.Word(0xd00a, 0xa040); native.Word(0xd00c, 0x5840);
                // Isolate this producer: mode $00 retains the request for the
                // already separately proven object-tail transition consumer.
                memory.SetField("wScrollMode", 0); native[0xcd00] = 0;
                Step(1, Vector2.Zero);
                if (ladder)
                {
                    room.SetPositionTileAndCollision(new(88, 168), 0x16, 0, 0);
                    native.CopyRoom(room);
                }
                Step(14, Vector2.Zero);
                int expected = game == OracleGame.Seasons && ladder ? 0x82 : 0;
                FailIf(memory.ReadField("wScreenTransitionDirection") != expected || native[0xcd02] != expected,
                    context + ": only Seasons' exact LADDER type publishes the native downward forced request.");
            }
            CheckSharedSideScrollDrowning(root, batched);
        }
        finally
        {
            transitions.ScreenTransitionsDisabledSource = disabled;
            Load(originalGroup, originalRoom);
            entities.Random.RestoreState(initialRandom);
        }
    }
}
