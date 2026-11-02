
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedSideScrollDrowning(FrontendValidationRoot root, bool batched)
    {
        var session = root._gameplaySession!; var rooms = session.Rooms; var memory = rooms.RuntimeState;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player; var inventory = player.Inventory; var game = memory.Game;
        var app = new ApplicationValidationFixture(root);
        var flippers = TreasureDatabase.For(rooms.AssetRoot).GetObject("TREASURE_OBJECT_FLIPPERS_00");
        bool hadFlippers = inventory.HasTreasure(TreasureId.Flippers);
        int health = inventory.HealthQuarters;
        void Flippers(bool obtained)
        { if (inventory.HasTreasure(TreasureId.Flippers) != obtained) inventory.ToggleTreasureObjectForDebug(flippers); }
        try
        {
            // Ages' native side-view table has no lava tile; retain that content boundary.
            foreach (bool lava in game == OracleGame.Ages ? new[] { false } : new[] { false, true })
            foreach (bool rawMarker in new[] { false, true })
            {
                if (game == OracleGame.Ages) root.LoadDebugRoom(7, 0x07);
                else root._seasonsOpening!.DebugTravel(6, 0x01);
                entities.Clear(); player.ClearNativeItemParents();
                Flippers(lava); // Lava must still drown Link with Flippers.
                inventory.EquipA(0); inventory.EquipB(0); inventory.SetScriptedActiveRing(0xff);
                inventory.SetScriptedHealth(inventory.MaxHealthQuarters); session.StatusBar.SynchronizeHealth();
                var room = rooms.CurrentRoom;
                var types = TilePropertyDatabase.For(rooms.AssetRoot);
                byte liquid = (byte)Enumerable.Range(0, 256).First(tile =>
                    types.SideScrollType((byte)tile) == (lava ? SideScrollTileType.Lava : SideScrollTileType.Water));
                for (int y = 0; y < room.HeightInTiles; y++)
                for (int x = 0; x < room.WidthInTiles; x++)
                {
                    bool wet = x >= 6 && y >= 5;
                    room.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), wet ? liquid : y == 6 ? (byte)0x26 : (byte)0,
                        (byte)(!wet && y == 6 ? 15 : 0), 0);
                }
                player.WarpTo(new(80.25f, 89.5f)); player.Face(Vector2I.Right);
                player.SetLocalRespawnPosition(new(72, 88), Vector2I.Right);
                memory.SetField("wScrollMode", 1); memory.SetField("wDisabledObjects", 0);
                memory.SetField("wLinkForceState", 0); memory.SetField("wPaletteThread_mode", 0);
                var native = new BraceletRom(room, player.PrecisePosition, 1, 0xff, game: game);
                native.LoadSave(rooms.SaveData); native.InitializeWalkingAnimation();
                native[0xd009] = unchecked((byte)player.SideScrollAngle); native[0xd01a] = 0xc1;
                native[0xcc2d] = (byte)rooms.ActiveGroup; native[0xcc2e] = 1; native[0xcc91] = 1;
                native[0xcd0c] = (byte)(room.Width - 6); native[0xcd0d] = (byte)(room.Height - 7);
                native[0xcc21] = 88; native[0xcc22] = 72; native[0xcc23] = 1;
                native[0xcc50] = memory.ReadField("wcc50"); native[0xcc52] = memory.ReadField("wcc52");
                native.SetNativeByte(memory.FieldAddress("wLinkStateParameter"), memory.ReadField("wLinkStateParameter"));
                var seed = entities.Random.CaptureState(); native[0xff94] = seed.Rng1; native[0xff95] = seed.Rng2;
                var sounds = root._sound.AttachPlayRequestAudit(); int updates = 0;
                void Step(int count = 1, Vector2 movement = default) => app.Step(count, movement, batched: batched, afterUpdate: () =>
                {
                    native.UpdateLinkItems(entities.FrameCounter, movement == Vector2.Right ? 0x10 : 0, interactions: true);
                    string context = $"{game}/side-drown/lava{lava}/raw{rawMarker}/batch{batched}/update{++updates}";
                    FailIf(player.PrecisePosition != new Vector2(native.Word(0xd00c) / 256f, native.Word(0xd00a) / 256f) ||
                        player.NativeNormalStateForInteraction != (native[0xd004] == 1) || player.Visible != ((native[0xd01a] & 0x80) != 0),
                        context + $": XY/state/visibility runtime{player.PrecisePosition}/{player.NativeNormalStateForInteraction}/{player.Visible}, native${native.Word(0xd00c):x4},${native.Word(0xd00a):x4}/${native[0xd004]:x2}/${native[0xd01a]:x2}; air{player.SideScrollAirborne}/${native[0xcc32]:x2}, drown{player.IsDrowning}, force${memory.ReadField("wLinkForceState"):x2}/${native[0xcc4f]:x2}, mode${memory.ReadWramByte(0xd030):x2}/${native[0xd030]:x2}.");
                    if (player.IsDrowning || native[0xd004] == 2)
                        foreach (int address in new[] { 0xd005, 0xd006, 0xd020, 0xd021, 0xd022, 0xd023, 0xd030, 0xd031 })
                            FailIf(memory.ReadWramByte(address) != native[address],
                                context + $": native byte${address:x4} runtime${memory.ReadWramByte(address):x2}/native${native[address]:x2}.");
                    foreach (string field in new[] { "wLinkForceState", "wLinkStateParameter", "wcc92", "wGameKeysPressed", "wForceLinkPushAnimation" })
                        FailIf(memory.ReadField(field) != native.ReadNativeByte(memory.FieldAddress(field)),
                            context + $": {field} runtime${memory.ReadField(field):x2}/native${native.ReadNativeByte(memory.FieldAddress(field)):x2}.");
                    FailIf(player.HealthQuarters != native[game == OracleGame.Ages ? 0xc6aa : 0xc6a2] ||
                        player.PatchCollisionsEnabled != ((native[0xd024] & 0x80) != 0),
                        context + ": native health/collision lifetime differs.");
                    var rng = entities.Random.CaptureState();
                    FailIf(rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95] || rng.Calls - seed.Calls != native.RandomCalls ||
                        !sounds.Requests.SequenceEqual(native.Sounds), context + ": native RNG/cues differ.");
                });
                for (int approach = 0; !player.IsDrowning && approach < 40; approach++) Step(1, Vector2.Right);
                FailIf(!player.IsDrowning || player.Position.X < 96 || room.IsSolid(player.Position),
                    "Side-view drowning must enter through the fixture's actual dry-floor/liquid edge.");
                if (!lava)
                {
                    root._scene.Dialogue.ShowMessage("Side-view drowning pause.", 104); native[0xcba0] = 1;
                    Step(4); root._scene.Dialogue.Close(); native[0xcba0] = 0;
                    if (rawMarker)
                    {
                        memory.SetWramByte(0xd020, 0); native[0xd020] = 0; Step(40);
                        FailIf(native[0xd004] != 1 || native[0xd020] != 0xd8,
                            "Side-view drowning must use a live zero-counter wrap, not a private22-update duration.");
                        memory.SetWramByte(0xd021, 0x80); native[0xd021] = 0x80;
                    }
                }
                else
                {
                    Step(2); // Consume force02, then initialize parameter4 without an animation advance.
                    FailIf(native[0xd004] != 2 || native[0xd005] != 5,
                        "Side-view lava must enter shared state02/substate5 through its deferred parameter4 request.");
                    if (rawMarker) { memory.SetWramByte(0xd021, 0x80); native[0xd021] = 0x80; }
                }
                for (int wait = 0; (player.IsDrowning || !player.NativeNormalStateForInteraction) && wait < 96; wait++) Step();
                Step(2);
                FailIf(player.IsDrowning || !player.NativeNormalStateForInteraction || !player.Visible,
                    "Native side-view drowning and recovery must complete before repeating.");
            }
        }
        finally
        {
            player.WarpTo(new(72, 88)); entities.Clear(); Flippers(hadFlippers);
            inventory.SetScriptedHealth(health); session.StatusBar.SynchronizeHealth();
        }
    }
}
