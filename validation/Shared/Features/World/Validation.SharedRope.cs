
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedRope(FrontendValidationRoot root, bool batched)
    {
        var rooms = root._gameplaySession!.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player;
        var app = new ApplicationValidationFixture(root);
        var game = rooms.SaveData.Game;
        var random = entities.Random.CaptureState();
        int originalGroup = rooms.ActiveGroup, originalRoom = rooms.CurrentRoom.Id;
        void Load(int group, int room)
        {
            root._scene.Dialogue.Close();
            if (game == OracleGame.Ages) root.LoadDebugRoom(group,room);
            else root._seasonsOpening!.DebugTravel(group,room);
            entities.Clear(); entities.DisableLinkCollisionsAndMenu(); player.BeginCutsceneControl();
            player.Inventory.EquipA(0); player.Inventory.EquipB(0); player.Inventory.SetScriptedActiveRing(0xff);
        }
        try
        {
            foreach (int subId in new[] { 0,1 })
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Load(0,game == OracleGame.Ages ? 0x98 : 0xc9);
                var room = rooms.CurrentRoom;
                byte floor = game == OracleGame.Ages ? (byte)0xa0 : (byte)0x04;
                for (int y = 0; y < room.HeightInTiles; y++)
                for (int x = 0; x < room.WidthInTiles; x++)
                    room.SetPositionTileAndCollision(new(x * 16 + 8,y * 16 + 8),floor,0,0);
                player.WarpTo(new(136,112)); player.Face(Vector2I.Down);
                SetDiggingRoll(entities.Random,repeat == 0 ? 0 : 0xdf);
                var seed = entities.Random.CaptureState();
                FailIf(!entities.TrySpawnDebugEnemy(0x10,subId,new(64,56),out string error),error);
                var rope = entities.Entities<RopeCharacter>().Single();
                string context = $"{game}: ordinary ENEMY$10:${subId:x2}, repeat={repeat}, batch={batched}";
                FailIf(!rope.InitializationPending || rope.Visible || entities.Random.Calls != seed.Calls,
                    context + ": standalone allocation must remain hidden state zero without consuming RNG.");
                var native = SharedNativeEnemyReference(rooms,player,0x10);
                native[0xff94] = seed.Rng1; native[0xff95] = seed.Rng2;
                native[0xd080] = 1; native[0xd081] = 0x10; native[0xd082] = (byte)subId;
                native.Word(0xd08c,64 << 8); native.Word(0xd08a,56 << 8);
                native[game == OracleGame.Ages ? 0xffaa : 0xffa8] = 0xf0;
                int updates = 0;
                void Step(int count)
                {
                    app.Step(count,Vector2.Zero,batched:batched,afterUpdate:() =>
                    {
                        native.AdvanceEnemies(entities.FrameCounter); updates++;
                        int state = rope.InitializationPending ? 0 : rope.State switch
                        {
                            RopeState.FallSetup => 8,
                            RopeState.WaitingToFall => 9,
                            RopeState.Falling => 10,
                            RopeState.Wandering => subId == 1 ? 11 : rope.CollisionEnabled ? 9 : 8,
                            RopeState.Charging => subId == 1 ? 12 : 10,
                            _ => throw new InvalidOperationException(context + ": unexpected ordinary Rope state.")
                        };
                        var rng = entities.Random.CaptureState();
                        FailIf(state != native[0xd084] || rope.Counter != native[0xd086] || rope.Cooldown != native[0xd087] ||
                            rope.Position != new Vector2(native.Word(0xd08c) / 256f,native.Word(0xd08a) / 256f) ||
                            (ushort)rope.ZFixed != native.Word(0xd08e) || (ushort)rope.SpeedZ != native.Word(0xd094) ||
                            rope.SpeedRaw != native[0xd090] || rope.Angle != native[0xd089] ||
                            rope.Visible != ((native[0xd09a] & 128) != 0) ||
                            rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95] || rng.Calls != seed.Calls + native.RandomCalls,
                            context + $": update {updates}: native state/counters/XYZ/speed/angle/visibility/RNG differs; " +
                            $"runtime={state}/{rope.Counter}/{rope.Cooldown}/{rope.Position}/${(ushort)rope.ZFixed:x4}/${(ushort)rope.SpeedZ:x4}, " +
                            $"native={native[0xd084]}/{native[0xd086]}/{native[0xd087]}/{native.Word(0xd08c):x4},{native.Word(0xd08a):x4}/${native.Word(0xd08e):x4}/${native.Word(0xd094):x4}.");
                    });
                }
                root._scene.Dialogue.ShowMessage("Native Rope initialization hold.",player.Position.Y);
                native[0xcba0] = 1; Step(1); Step(3);
                root._scene.Dialogue.Close(); native[0xcba0] = 0;
                Step(240);
            }
        }
        finally
        {
            player.EndCutsceneControl(); Load(originalGroup,originalRoom); player.EndCutsceneControl();
            entities.EnableLinkCollisionsAndMenu(); entities.Random.RestoreState(random);
        }
    }
}
