
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedBossEntry(FrontendValidationRoot root, bool batched)
    {
        var session = root._gameplaySession!; var rooms = session.Rooms; var memory = rooms.RuntimeState;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player; var inventory = player.Inventory; var game = memory.Game;
        var app = new ApplicationValidationFixture(root);
        int group = rooms.ActiveGroup, roomId = rooms.CurrentRoom.Id;
        int health = inventory.HealthQuarters, ring = inventory.ActiveRing, a = inventory.EquippedA, b = inventory.EquippedB;
        void Load(int targetGroup, int targetRoom)
        { if (game == OracleGame.Ages) root.LoadDebugRoom(targetGroup, targetRoom); else root._seasonsOpening!.DebugTravel(targetGroup, targetRoom); entities.Clear(); }
        try
        {
            Load(0, game == OracleGame.Ages ? 0x33 : 0x97);
            var room = rooms.CurrentRoom;
            for (int y = 0; y < 8; y++) for (int x = 0; x < 10; x++)
                room.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), game == OracleGame.Ages ? (byte)0x3a : (byte)4, 0, 0);
            inventory.EquipA(0); inventory.EquipB(0); inventory.SetScriptedActiveRing(0xff);
            inventory.SetScriptedHealth(inventory.MaxHealthQuarters); session.StatusBar.SynchronizeHealth();
            foreach (int direction in Enumerable.Range(0, 4))
            {
                player.WarpTo(new(80.25f, 64.5f)); player.Face(Vector2I.Down); player.ClearNativeItemParents();
                memory.SetField("wLinkForceState",0); memory.SetField("wDisabledObjects",0);
                memory.SetField("wPaletteThread_mode",0); memory.SetField("wScrollMode",1);
                var native = new BraceletRom(room, player.PrecisePosition, 2, 0xff, game: game);
                native.LoadSave(rooms.SaveData); native.InitializeWalkingAnimation(); native[0xd01a] = 0xc1;
                native[0xcc50] = memory.ReadField("wcc50"); native[0xcc52] = memory.ReadField("wcc52");
                foreach (string field in new[] { "wScrollMode","wcc93","wLinkStateParameter" })
                    native.SetNativeByte(memory.FieldAddress(field),memory.ReadField(field));
                var seed = entities.Random.CaptureState(); native[0xff94] = seed.Rng1; native[0xff95] = seed.Rng2;
                var sounds = root._sound.AttachPlayRequestAudit(); int updates = 0;
                void Set(string field, byte value)
                { memory.SetField(field,value); native.SetNativeByte(memory.FieldAddress(field),value); }
                void Publish()
                {
                    int result=entities.InitializeBossRoomWithResult();
                    FailIf(result!=native.InitializeBossRoom(),"Common boss initializer must preserve native A=1 or the scrolling direction angle.");
                }
                void Check()
                {
                    string context = $"{game}/boss-entry/direction${direction:x2}/batch{batched}/update{updates}";
                    FailIf(player.PrecisePosition != new Vector2(native.Word(0xd00c)/256f,native.Word(0xd00a)/256f) ||
                        player.NativeForcedMovementActive != (native[0xd004] == 0x0b) ||
                        CarriedObjectMotion.DirectionIndex(player.FacingVector) != native[0xd008],
                        context + $": native XY/state/facing runtime{player.PrecisePosition}/{player.NativeForcedMovementActive}/{player.FacingVector}, native${native.Word(0xd00c):x4},${native.Word(0xd00a):x4}/${native[0xd004]:x2}/${native[0xd008]:x2}.");
                    foreach (int address in new[] { 0xd005,0xd006,0xd008,0xd020,0xd021,0xd022,0xd023,0xd030,0xd031 })
                        FailIf(memory.ReadWramByte(address) != native[address],context+$": byte${address:x4} runtime${memory.ReadWramByte(address):x2}/native${native[address]:x2}.");
                    foreach (string field in new[] { "wLinkForceState","wLinkStateParameter","wcc93","wDisableLinkCollisionsAndMenu" })
                        FailIf(memory.ReadField(field) != native.ReadNativeByte(memory.FieldAddress(field)),context+$": {field} differs.");
                    FailIf(entities.NativeActiveMusicSource() != native.ReadNativeByte(memory.FieldAddress("wActiveMusic")),
                        context+": authoritative sound owner's native active-music gate differs.");
                    var rng = entities.Random.CaptureState();
                    FailIf(rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95] || rng.Calls-seed.Calls != native.RandomCalls ||
                        !sounds.Requests.SequenceEqual(native.Sounds),context+": ordered RNG/cues differ.");
                }
                void Step(int count = 1) => app.Step(count,Vector2.Zero,batched:batched,afterUpdate: () =>
                { native.UpdateLinkItems(entities.FrameCounter,0); updates++; Check(); });
                Set("wLinkForceState",4); Set("wScreenTransitionDirection",(byte)direction);
                Publish(); Check(); // scrollMode1 must preserve an existing request and direction.
                Set("wLinkForceState",0); Set("wScrollMode",8); Publish();
                FailIf(memory.ReadField("wLinkStateParameter") != (game == OracleGame.Ages ? 0x16 : 0x1a) ||
                    memory.ReadWramByte(0xd009) != direction*8,
                    "Boss entry must publish each game's native duration and immediate direction/angle before Link runs.");
                Check(); Set("wScrollMode",1);
                Set("wPaletteThread_mode",1); Step(2); // Palette gate precedes request consumption.
                Set("wPaletteThread_mode",0); Set("wDisabledObjects",0x81); Step();
                FailIf(!player.NativeForcedMovementActive || player.PrecisePosition != new Vector2(80.25f,64.5f),
                    "Boss request consumption must select state0b without moving on that update.");
                Step(4);
                // A later actor can retarget the selected state's live angle/facing.
                memory.SetWramByte(0xd009,(byte)(((direction+1)&3)*8)); native[0xd009]=(byte)(((direction+1)&3)*8);
                memory.SetWramByte(0xd008,(byte)((direction+1)&3)); native[0xd008]=(byte)((direction+1)&3);
                Set("wDisabledObjects",0x81); Step(3); Set("wDisabledObjects",0);
                for (int wait = 0; player.NativeForcedMovementActive && wait < 32; wait++) Step();
                FailIf(player.NativeForcedMovementActive,"Boss entry must finish through Link's native counter.");
                Step();
            }
        }
        finally
        {
            memory.SetField("wDisabledObjects",0); memory.SetField("wScrollMode",1); player.WarpTo(new(40,40));
            inventory.EquipA(a); inventory.EquipB(b); inventory.SetScriptedActiveRing(ring);
            inventory.SetScriptedHealth(health); session.StatusBar.SynchronizeHealth(); Load(group,roomId);
        }
    }
}
