
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedTerrainJumps(FrontendValidationRoot root, bool batched)
    {
        var rooms = root._gameplaySession!.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player;
        var room = rooms.CurrentRoom;
        OracleGame game = rooms.SaveData.Game;
        var app = new ApplicationValidationFixture(root);
        var original = (from y in Enumerable.Range(0,room.HeightInTiles) from x in Enumerable.Range(0,room.WidthInTiles)
            let point = new Vector2(x * 16 + 8,y * 16 + 8)
            select (Point:point,Tile:room.GetMetatile(point),Collision:room.GetTerrainInfo(point).Collision)).ToArray();
        byte floor = (byte)(game == OracleGame.Ages ? 0x3a : 0x04);
        BraceletRom native = null!;
        OracleRandomState random = default;
        ValidationSoundRequestAudit sounds = null!;
        string context = "";
        int updates = 0;
        bool compareSword = false;
        bool compareCarry = false;
        void Floor()
        {
            foreach (var tile in original) room.SetPositionTileAndCollision(tile.Point,floor,0,0);
        }
        void Prepare(Vector2 start, int direction, string name)
        {
            entities.Clear(); player.WarpTo(start); player.Face((Vector2I)OracleObjectMath.StrictCardinalVector(direction));
            player.Inventory.EquipA(0); player.Inventory.EquipB(0);
            native = new(room,start,direction / 8,0xff,game:game);
            native.LoadSave(rooms.SaveData); native.InitializeWalkingAnimation();
            native[0xd009] = unchecked((byte)SomariaPrivate<int>(player,"_topDownMovementAngle"));
            native[0xcc2d] = (byte)rooms.ActiveGroup; native[0xcc2e] = 0;
            native[0xcd00] = 1;
            random = entities.Random.CaptureState(); native[0xff94] = random.Rng1; native[0xff95] = random.Rng2;
            sounds = root._sound.AttachPlayRequestAudit(); context = $"{game}: {name}, batch={batched}"; updates = 0;
        }
        void Compare()
        {
            updates++;
            int z = player.LedgeJumpPhase != LedgeJumpState.None ? SomariaPrivate<int>(player,"_ledgeZFixed") : player.ItemCreationZFixed;
            Vector2 position = new(native.Word(0xd00c) / 256f,native.Word(0xd00a) / 256f);
            FailIf(player.PrecisePosition != position || (ushort)z != native.Word(0xd00e) ||
                player.NativeNormalStateForInteraction != (native[0xd004] == 1) ||
                player.NativeInAirForInteraction != (native[0xcc5c] != 0) ||
                CarriedObjectMotion.DirectionIndex(player.FacingVector) != native[0xd008],
                $"{context}, update {updates}: native XY/Z/state/facing differs: runtime={player.PrecisePosition}/${(ushort)z:x4}/{player.LedgeJumpPhase}, " +
                $"native={position}/${native.Word(0xd00e):x4}/${native[0xd004]:x2}:${native[0xd005]:x2}; " +
                $"air={player.NativeInAirForInteraction}/${native[0xcc5c]:x2}, facing={player.FacingVector}/${native[0xd008]:x2}.");
            if (native[0xd004] == 0x12)
                FailIf(player.LedgeSpeedRaw != native[0xd010] || player.LedgeSpeedZ != unchecked((short)native.Word(0xd014)),
                    context + ": native ledge planar/vertical speed differs.");
            FailIf(!sounds.Requests.Where(id => id != SoundId.SndText).SequenceEqual(native.Sounds),context + ": jump/landing sound order differs.");
            var rng = entities.Random.CaptureState();
            FailIf(rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95] || rng.Calls != random.Calls + native.RandomCalls,
                context + ": shared RNG differs.");
            if (compareSword)
            {
                FailIf(player.IsAttacking != (native[0xd200] != 0) ||
                    player.IsAttacking && (unchecked((byte)player.MeleeItemZ) != native[0xd60f]),
                    context + $": retained sword parent/child height differs on update {updates}: parent={player.SwordState}/${native[0xd204]:x2}, childZ={player.MeleeItemZ}/${native[0xd60f]:x2}.");
                if (native[0xd004] == 0x12)
                    FailIf(native[0xd030] != 0x10 || native[0xd031] != rooms.RuntimeState.ReadWramByte(0xd031) ||
                        native[0xd020] != rooms.RuntimeState.ReadWramByte(0xd020),
                        context + $": retained WALK animation differs on update {updates}: native=${native[0xd030]:x2}/${native[0xd031]:x2}/{native[0xd020]}.");
            }
            if (compareCarry)
                FailIf(player.IsCarryingObject != (native[0xcc5a] == 0x83),context + ": native carry publication differs.");
        }
        void Step(int count = 1, int angle = 0xff, bool held = false, bool pressed = false)
        {
            Vector2 movement = angle >= 0x80 ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(angle);
            int keys = angle switch { 0 => 0x40,8 => 0x10,16 => 0x80,24 => 0x20,_ => 0 };
            app.Step(count,movement,held:held ? ["attack"] : [],pressed:pressed ? ["attack"] : [],batched:batched,
                afterUpdate:() => { native.UpdateLinkItems(entities.FrameCounter,keys | (held ? 1 : 0)); Compare(); });
        }
        try
        {
            // Declared cliff geometry starts on dry floor and is approached
            // through native wall probes, never from inside the solid cliff.
            (int Tile,int Angle)[] cliffs = game == OracleGame.Ages ?
                [(5,16),(6,16),(7,16),(0x0a,24),(0x0b,8),(0x64,16),(0xff,16)] :
                [(0x54,16),(0x25,24),(0x26,8),(0x28,8),(0x27,24),(0x94,16),(0x95,16),(0x2a,0),
                 (0x9a,16),(0xcc,16),(0xcd,16),(0xce,16),(0xcf,16),(0xfe,16),(0xff,16)];
            foreach (var cliff in cliffs)
            foreach (int thickness in new[] { 1,2 })
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Floor();
                for (int y = 1; y < 7; y++)
                for (int x = 1; x < 9; x++)
                {
                    bool solid = cliff.Angle switch
                    {
                        16 => y >= 4 && y < 4 + thickness,
                        0 => y <= 4 && y > 4 - thickness,
                        8 => x >= 6 && x < 6 + thickness,
                        _ => x <= 3 && x > 3 - thickness
                    };
                    if (solid) room.SetPositionTileAndCollision(new(x * 16 + 8,y * 16 + 8),(byte)cliff.Tile,0x0f,0);
                }
                Vector2 start = cliff.Angle switch { 16 => new(80.25f,52.5f),0 => new(80.25f,88.5f),8 => new(88.25f,56.5f),_ => new(72.25f,56.5f) };
                Prepare(start,cliff.Angle,$"cliff ${cliff.Tile:x2}, thickness={thickness}, repeat={repeat}");
                FailIf(player.World.Collides(start),context + ": approach must begin on collision-reachable floor.");
                for (int approach = 0; approach < 64 && native[0xd004] != 0x12; approach++) Step(angle:cliff.Angle);
                FailIf(native[0xd004] != 0x12 || player.LedgeJumpPhase == LedgeJumpState.None,context + ": actual input did not start native state $12.");
                Step(5);
                root._scene.Dialogue.ShowMessage("Ledge pause.",player.Position.Y); native[0xcba0] = 1;
                Step(3); root._scene.Dialogue.Close(); native[0xcba0] = 0;
                Step(40);
                FailIf(player.LedgeJumpPhase != LedgeJumpState.None || native[0xd004] != 1,context + ": landing must restore normal control.");
                Step(2);
            }
            // swordParent state6 enables movement while retaining its turning
            // lock. Approach backwards to avoid poking the cliff with the sword.
            foreach (bool charged in new[] { false,true })
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Floor();
                for (int x = 1; x < 9; x++) room.SetPositionTileAndCollision(new(x * 16 + 8,72),
                    (byte)(game == OracleGame.Ages ? 0x05 : 0x54),0x0f,0);
                Prepare(new(80.25f,48.5f),0,$"sword cliff, charged={charged}, repeat={repeat}");
                player.Inventory.GiveTreasure(TreasureId.Sword,1); player.Inventory.EquipA(TreasureId.Sword);
                native.LoadSave(rooms.SaveData); compareSword = true;
                Step(held:true,pressed:true); Step(charged ? 70 : 20,held:true);
                FailIf(!player.SwordAllowsMovement || native[0xd204] is not (2 or 3),context + ": source sword must permit movement before approach.");
                for (int approach = 0; approach < 64 && native[0xd004] != 0x12; approach++) Step(angle:16,held:true);
                FailIf(player.LedgeJumpPhase == LedgeJumpState.None || native[0xd004] != 0x12 || !player.IsAttacking,
                    context + ": held sword must permit cliff takeoff and retain its parent.");
                int swordFrame = player.SwordStateFrame, nativeCounter = native[0xd206];
                // Releasing the button while falling cannot reach checkUseItems.
                Step(4);
                FailIf(player.SwordStateFrame != swordFrame || native[0xd206] != nativeCounter,
                    context + ": state $12 must freeze the retained sword parent despite button release.");
                root._scene.Dialogue.ShowMessage("Sword cliff pause.",player.Position.Y); native[0xcba0] = 1;
                Step(3); root._scene.Dialogue.Close(); native[0xcba0] = 0;
                for (int fall = 0; fall < 64 && native[0xd004] == 0x12; fall++) Step();
                FailIf(!player.IsAttacking || native[0xd200] == 0,context + ": landing must retain the parent until the next state01 update.");
                Step(40);
                FailIf(player.IsAttacking || native[0xd200] != 0,context + ": released sword must finish after normal control resumes.");
                compareSword = false;
            }
            foreach (bool stump in game == OracleGame.Seasons ? new[] { false,true } : new[] { false })
            {
                Floor();
                byte liftable = (byte)(game == OracleGame.Ages ? 0xc3 : 0xe5);
                room.SetPositionTileAndCollision(new(88,24),liftable,0x0f,0);
                room.SetUnderlyingMetatile(new(88,24),floor);
                if (stump) room.SetPositionTileAndCollision(new(88,72),0x20,room.GetCollision(0x20),0);
                else for (int x = 1; x < 9; x++) room.SetPositionTileAndCollision(new(x * 16 + 8,72),
                    (byte)(game == OracleGame.Ages ? 0x05 : 0x54),0x0f,0);
                Prepare(new(88,50),0,$"carrying against {(stump ? "stump" : "cliff")}");
                player.Inventory.GiveTreasure(TreasureId.Bracelet,1); player.Inventory.EquipA(TreasureId.Bracelet);
                native.LoadSave(rooms.SaveData); compareCarry = true;
                Step(24,0); Step(held:true,pressed:true); Step(11,16,held:true); Step(20);
                FailIf(!player.IsCarryingObject || native[0xcc5a] != 0x83,context + ": real pickup must reach carrying before approaching terrain.");
                Step(40,16);
                FailIf(player.LedgeJumpPhase != LedgeJumpState.None || player.TopDownAirborne || native[0xd004] != 1 || native[0xcc5c] != 0,
                    context + ": native grab state must prevent terrain takeoff while movement remains enabled.");
                compareCarry = false;
            }
            if (game == OracleGame.Seasons)
            {
                foreach (int entry in new[] { 0,8,16,24 })
                foreach (int exit in new[] { 0,8,16,24 })
                {
                    Floor(); room.SetPositionTileAndCollision(new(72,72),0x20,room.GetCollision(0x20),0);
                    Vector2 start = entry switch { 0 => new(72.25f,94.5f),8 => new(48.25f,66.5f),16 => new(72.25f,45.5f),_ => new(96.25f,66.5f) };
                    Prepare(start,entry,$"stump entry=${entry:x2}, exit=${exit:x2}");
                    FailIf(player.World.Collides(start),context + ": stump approach must begin on reachable floor.");
                    for (int approach = 0; approach < 64 && native[0xcc5c] == 0; approach++) Step(angle:entry);
                    FailIf(native[0xcc5c] != 0x82 || !player.TopDownAirborne,context + ": approaching the actual stump collision must start state01's unblocked air path.");
                    Step(40);
                    FailIf(native[0xcc5c] != 0 || (native[0xd00d] & 0xf0) != 0x40 || native[0xd00b] != 66,
                        context + ": stump entry must retain fractions and snap high bytes to native var37/var38.");
                    Vector2 offset = exit switch { 0 => new(0,-5),8 => new(9,0),16 => new(0,27),_ => new(-10,0) };
                    Vector2 blocked = player.PrecisePosition.Floor() + offset;
                    room.SetPositionTileAndCollision(blocked,0x54,0x0f,0); native.CopyRoom(room);
                    Step(2,exit);
                    FailIf(native[0xcc5c] != 0 || player.TopDownAirborne,context + ": a solid exit probe must keep Link on the stump.");
                    room.SetPositionTileAndCollision(blocked,floor,0,0); native.CopyRoom(room); Step();
                    Step(angle:exit);
                    FailIf(native[0xcc5c] == 0 || !player.TopDownAirborne,context + ": fresh directional input must leave the stump.");
                    Step(40);
                }
                Floor(); room.SetPositionTileAndCollision(new(72,72),0x20,room.GetCollision(0x20),0);
                Prepare(new(48.25f,66.5f),8,"sword stump entry and locked-facing exit");
                player.Inventory.GiveTreasure(TreasureId.Sword,1); player.Inventory.EquipA(TreasureId.Sword);
                native.LoadSave(rooms.SaveData); compareSword = true;
                Step(held:true,pressed:true); Step(20,held:true);
                for (int approach = 0; approach < 64 && native[0xcc5c] == 0; approach++) Step(angle:8,held:true);
                FailIf(!player.TopDownAirborne || player.IsAttacking || native[0xcc5c] == 0 || native[0xd200] != 0,
                    context + ": source stump entry must cancel the held sword, unlike a cliff jump.");
                Step(40,held:true); Step();
                Step(held:true,pressed:true); Step(20,held:true);
                FailIf(!player.SwordAllowsMovement || player.FacingVector != Vector2I.Right,context + ": sword exit must begin with the retained entry facing.");
                Step(angle:0,held:true);
                FailIf(!player.TopDownAirborne || !player.IsAttacking || player.FacingVector != Vector2I.Right || native[0xd008] != 1,
                    context + ": stump exit retains the sword and its turning lock while moving in the new input direction.");
                Step(40,held:true); Step(40); compareSword = false;
                Floor(); room.SetPositionTileAndCollision(new(72,72),0x20,room.GetCollision(0x20),0);
                Prepare(new(48.25f,66.5f),8,"stump-jump cancellation");
                for (int approach = 0; approach < 64 && native[0xcc5c] == 0; approach++) Step(angle:8);
                Step(3);
                // Declared full Link replacement: both fixtures start a fresh
                // object; later updates must not retain the old air target.
                Prepare(new(24.25f,24.5f),0,"fresh Link after cancelled stump jump");
                Step(4);
                FailIf(player.TopDownAirborne || player.PrecisePosition != new Vector2(24.25f,24.5f),
                    "Full Link replacement must cancel stump air motion and its landing target.");
                // A ledge can land on the solid stump exception; its distinct
                // native centering epilogue must still leave a usable exit.
                Floor();
                for (int x = 1; x < 9; x++) room.SetPositionTileAndCollision(new(x * 16 + 8,72),0x54,0x0f,0);
                room.SetPositionTileAndCollision(new(72,88),0x20,room.GetCollision(0x20),0);
                Prepare(new(72.25f,56.5f),16,"ledge landing on stump");
                for (int approach = 0; approach < 64 && native[0xd004] != 0x12; approach++) Step(angle:16);
                Step(40); Step(angle:8); Step(40);
            }
        }
        finally
        {
            root._scene.Dialogue.Close(); entities.Clear(); player.WarpTo(new(72,80));
            foreach (var tile in original) room.SetPositionTileAndCollision(tile.Point,tile.Tile,tile.Collision,0);
        }
    }
}
