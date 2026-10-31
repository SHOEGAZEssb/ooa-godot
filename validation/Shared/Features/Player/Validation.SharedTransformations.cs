using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedTransformations(FrontendValidationRoot root, bool batched)
    {
        var rooms = root._gameplaySession!.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player;
        var inventory = player.Inventory;
        var memory = rooms.RuntimeState;
        var app = new ApplicationValidationFixture(root);
        var game = rooms.SaveData.Game;
        var room = rooms.CurrentRoom;
        var original = (from y in Enumerable.Range(0,room.HeightInTiles) from x in Enumerable.Range(0,room.WidthInTiles)
            let point = new Vector2(x * 16 + 8,y * 16 + 8)
            select (Point:point,Tile:room.GetMetatile(point),Collision:room.GetTerrainInfo(point).Collision)).ToArray();
        byte floor = (byte)(game == OracleGame.Ages ? 0x3a : 0x04);
        BraceletRom native = null!;
        OracleRandomState seed = default;
        ValidationSoundRequestAudit sounds = null!;
        string context = "";
        int update = 0;
        bool compareCarry = false;
        bool compareSwimming = false;
        void Prepare(string name, Vector2? start = null, int angle = 16)
        {
            entities.Clear(); inventory.SetScriptedActiveRing(0xff); inventory.EquipA(0); inventory.EquipB(0);
            memory.SetField("wDisableRingTransformations",0); memory.SetField("wMenuDisabled",0); memory.SetField("wInShop",0);
            foreach (string field in new[] { "wActiveTilePos","wActiveTileIndex","wStandingOnTileCounter","wActiveTileType","wLastActiveTileType","wIsTileSlippery" })
                memory.SetField(field,0);
            player.WarpTo(start ?? new(80.25f,64.5f)); player.Face((Vector2I)OracleObjectMath.StrictCardinalVector(angle));
            foreach (var tile in original) room.SetPositionTileAndCollision(tile.Point,floor,0,0);
            native = new(room,player.PrecisePosition,angle / 8,0xff,game:game);
            native.LoadSave(rooms.SaveData); native.InitializeWalkingAnimation();
            native[0xd009] = unchecked((byte)player.NativePlanarAngle);
            native[0xcc2d] = (byte)rooms.ActiveGroup; native[0xcd00] = 1;
            seed = entities.Random.CaptureState(); native[0xff94] = seed.Rng1; native[0xff95] = seed.Rng2;
            sounds = root._sound.AttachPlayRequestAudit(); context = $"{game}: {name}, batch={batched}"; update = 0;
            compareCarry = false;
            compareSwimming = false;
        }
        void Ring(int ring) { inventory.SetScriptedActiveRing(ring); native.LoadSave(rooms.SaveData); }
        void Compare()
        {
            update++;
            string[] tileFields = ["wActiveTilePos","wActiveTileIndex","wStandingOnTileCounter","wActiveTileType","wLastActiveTileType","wIsTileSlippery"];
            for (int i = 0; i < tileFields.Length; i++)
                FailIf(memory.ReadField(tileFields[i]) != native[0xcc99 + i],
                    $"{context} update {update}: {tileFields[i]} differs: runtime=${memory.ReadField(tileFields[i]):x2}, native=${native[0xcc99 + i]:x2}.");
            var position = new Vector2(native.Word(0xd00c) / 256f,native.Word(0xd00a) / 256f);
            int pending = SomariaPrivate<int>(player,"_ringTransformationRequested");
            int nativePending = native[0xcc57] == 0x80 ? -1 : native[0xcc57] & 0x7f;
            FailIf(player.PrecisePosition != position || player.ActiveTransformation != native[0xd001] || pending != nativePending ||
                (ushort)player.ItemCreationZFixed != native.Word(0xd00e) ||
                memory.ReadField("wDisableRingTransformations") != native[0xcc56] ||
                CarriedObjectMotion.DirectionIndex(player.FacingVector) != native[0xd008],
                $"{context} update {update}: native transformation XY/Z/ID/pending/counter/facing differs: " +
                $"runtime={player.PrecisePosition}/${(ushort)player.ItemCreationZFixed:x4}/{player.ActiveTransformation}/{pending}/{memory.ReadField("wDisableRingTransformations")}, " +
                $"native={position}/${native.Word(0xd00e):x4}/{native[0xd001]}/${native[0xcc57]:x2}/{native[0xcc56]}.");
            if (native[0xd001] is >= 3 and <= 7)
                FailIf(player.TransformationFrame * 4 != native[0xd031] || SomariaPrivate<int>(player,"_transformationTicks") != native[0xd020],
                    context + $": native transformed frame/counter differs on update {update}.");
            FailIf(player.IsAttacking != (native[0xd200] != 0 && native[0xd201] == 5),context + ": Sword parent ownership differs.");
            FailIf(!sounds.Requests.Where(id => id != SoundId.SndText).SequenceEqual(native.Sounds),
                context + $": transformation cue order differs on update {update}: runtime={string.Join(',',sounds.Requests)}, native={string.Join(',',native.Sounds)}.");
            var rng = entities.Random.CaptureState();
            FailIf(rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95] || rng.Calls != seed.Calls + native.RandomCalls,
                context + ": shared RNG differs.");
            int dust = Enumerable.Range(0xd2,14).Count(page => native[(page << 8) + 0x40] != 0 &&
                native[(page << 8) + 0x41] == 0x0f && native[(page << 8) + 0x42] == 1);
            FailIf(entities.Entities<KnockbackDustRoomEntity>().Count != dust,context + ": transformed Pegasus dust allocation/lifetime differs.");
            if (compareCarry)
            {
                var child = ((PlayerWorld)player.World).Bracelet.LiftedObject;
                FailIf((child is not null) != native.ChildActive,context + ": native reserved Bracelet child lifetime differs.");
                if (child is not null)
                {
                    Vector2 ground = child.Thrown ? child.GroundPosition : player.Position + new Vector2(child.Position.X,0);
                    int z = child.Thrown ? child.ZFixed : (int)child.Position.Y << 8;
                    FailIf(ground != new Vector2(native.Word(0xdc0c) / 256f,native.Word(0xdc0a) / 256f) || (ushort)z != native.Word(0xdc0e),
                        context + $": native held/thrown XYZ differs on update {update}: runtime={ground}/${(ushort)z:x4}, native={native.Word(0xdc0c) / 256f},{native.Word(0xdc0a) / 256f}/${native.Word(0xdc0e):x4}.");
                }
            }
            if (compareSwimming)
                FailIf(player.TopDownSwimmingState != (native[0xcc5d] & 15) || player.TopDownDiving != ((native[0xcc5d] & 0x80) != 0) ||
                    player.TopDownSwimSpeedRaw != native[0xd010] || player.TopDownSwimAngle != native[0xd009],
                    context + $": native Flippers state/dive/velocity differs on update {update}.");
        }
        void Step(int count = 1, int angle = 0xff, bool sword = false, bool pressed = false, bool divePressed = false)
        {
            var movement = angle >= 0x80 ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(angle);
            int keys = angle switch { 0 => 0x40,8 => 0x10,16 => 0x80,24 => 0x20,_ => 0 };
            app.Step(count,movement,held:divePressed ? ["item"] : sword ? ["attack"] : [],
                pressed:divePressed ? ["item"] : pressed ? ["attack"] : [],batched:batched,
                afterUpdate:() => { native.UpdateLinkItems(entities.FrameCounter,keys | (sword ? 1 : 0) | (divePressed ? 2 : 0),interactions:true); Compare(); });
        }
        try
        {
            foreach (var pair in new[] { (Ring:0x2a,Id:5),(Ring:0x2b,Id:6),(Ring:0x2c,Id:7),(Ring:0x2d,Id:3),(Ring:0x2e,Id:4) })
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Prepare($"ring ${pair.Ring:x2}, repeat={repeat}"); Ring(pair.Ring);
                Step(angle:8);
                FailIf(player.ActiveTransformation != 0 || player.PrecisePosition != new Vector2(80.25f,64.5f),context + ": selection queues ID replacement before movement.");
                Step(angle:24);
                FailIf(player.ActiveTransformation != pair.Id,context + ": next Link dispatch must initialize the selected special object.");
                Step(16,8); Step(16,0); Step(16,24); Step(16,16); Step(4);
                var graphics = new TransformedLinkDatabase(rooms.AssetRoot + "metadata/transformed_link.tsv");
                for (int direction = 0; direction < 4; direction++)
                for (int frame = 0; frame < 2; frame++)
                    FailIf(OracleGraphicsCache.PixelHash(graphics.Texture(pair.Id,direction,frame).GetImage()) == 0,
                        context + ": selected pack's transformed sprite failed to render.");
                inventory.GiveTreasure(TreasureId.Sword,1); inventory.EquipA(TreasureId.Sword); native.LoadSave(rooms.SaveData);
                Step(3,sword:true,pressed:true);
                FailIf(player.IsAttacking,context + ": transformed dispatch must not allocate an equipped item parent.");
                root._scene.Dialogue.ShowMessage("Transformation pause.",player.Position.Y); native[0xcba0] = 1;
                Ring(0xff); Step(3,8); root._scene.Dialogue.Close(); native[0xcba0] = 0;
                Step(); Step(3);
                FailIf(player.ActiveTransformation != 0,context + ": removing the ring must restore ordinary Link through state00.");
                Ring(pair.Ring); memory.SetField("wDisableRingTransformations",2); native[0xcc56] = 2;
                root._scene.Dialogue.ShowMessage("Suppression pause.",player.Position.Y); native[0xcba0] = 1;
                Step(3); root._scene.Dialogue.Close(); native[0xcba0] = 0;
                Step(2,8);
                FailIf(player.ActiveTransformation != 0 || memory.ReadField("wDisableRingTransformations") != 0,
                    context + ": suppression decrements on eligible selector calls, including its zero update.");
                Step(); Step();
                memory.SetField("wMenuDisabled",1); native[0xcc02] = 1; Step(3);
                FailIf(player.ActiveTransformation != 0,context + ": native menu disable must force ordinary form.");
                memory.SetField("wMenuDisabled",0); native[0xcc02] = 0;
                Step(3);
                memory.SetField("wInShop",2); native[0xccd3] = 2; Step(3);
                FailIf(player.ActiveTransformation != 0,context + ": native shop flag must block transformation.");
                memory.SetField("wInShop",0); native[0xccd3] = 0; Step(3);
                Ring(0xff); Step(4);
            }
            Prepare("pending override initializes through text and $81"); Ring(0x2a); Step();
            memory.SetField("wDisabledObjects",0x81); native[0xcc8a] = 0x81;
            root._scene.Dialogue.ShowMessage("Pending disguise.",player.Position.Y); native[0xcba0] = 1;
            Step();
            FailIf(player.ActiveTransformation != 5,context + ": transformed state0 initializes before text/$81 gates.");
            Ring(0xff); Step(3,8); root._scene.Dialogue.Close(); native[0xcba0] = 0;
            Step(3,8); memory.SetField("wDisabledObjects",0); native[0xcc8a] = 0; Step(4);
            Prepare("transformed recoil and gravity"); Ring(0x2a); Step(2);
            typeof(Player).GetField("_enemyKnockbackFrames",System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(player,2f);
            typeof(Player).GetField("_enemyKnockbackAngle",System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(player,24);
            native[0xd02d] = 2; native[0xd02c] = 24; Step(4,8);
            typeof(Player).GetField("_topDownAirZFixed",System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(player,-0x280);
            typeof(Player).GetField("_topDownAirSpeedZ",System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(player,0x40);
            native.Word(0xd00e,-0x280); native.Word(0xd014,0x40); Step(8,8);
            FailIf(player.ItemCreationZFixed != 0,context + ": transformed gravity must finish without an ordinary jump/landing cue.");
            Prepare("transformed forced-state cancellation"); Ring(0x2a); Step(2);
            player.RequestNativeForcedMovement(4,8,1); native[0xcc4f] = 0x0b; native[0xcc51] = 4; native[0xd008] = 1; native[0xd009] = 8;
            Step();
            FailIf(player.ActiveTransformation != 0 || memory.ReadField("wDisableRingTransformations") != 1,
                context + ": forceState must restore normal identity before consuming the requested state.");
            Step(12);
            foreach (bool full in new[] { false,true })
            {
                Prepare($"transformed Pegasus dust, full={full}"); Ring(0x2a); Step(2);
                if (full)
                    for (int slot = 0; slot < 14; slot++)
                    {
                        Vector2 position = new(24 + slot * 8,40);
                        FailIf(!entities.TryCreateKnockbackDust(position,0),context + ": source pool fixture requires fourteen free interactions.");
                        int address = (0xd2 + slot) * 256 + 0x40;
                        native[address] = 1; native[address + 1] = 0x0f; native[address + 2] = 1;
                        native[address + 0x0b] = (byte)position.Y; native[address + 0x0d] = (byte)position.X;
                    }
                memory.SetField("wPegasusSeedCounter",32); memory.SetField("wPegasusSeedCounter",0,1);
                native[0xcc6c] = 32; native[0xcc6d] = 0; Step(24,8);
            }
            Prepare("transformed hole force-state handoff",new(32.25f,60.5f),8); Ring(0x2a); Step(2);
            for (int x = 3; x < 9; x++) room.SetPositionTileAndCollision(new(x * 16 + 8,72),0xf3,0,0);
            native.CopyRoom(room);
            for (int walk = 0; walk < 96 && native[0xcc4f] == 0; walk++) Step(angle:8);
            FailIf(native[0xcc4f] != 2 || player.ActiveTransformation != 5,context + ": transformed hole pull must queue state02 while retaining the current ID for this pass.");
            Step(4);
            Prepare("ring-to-ring replacement"); Ring(0x2a); Step(2);
            Ring(0x2d); Step(4);
            FailIf(player.ActiveTransformation != 3,context + ": changing disguise must pass through ordinary state00, suppression and deferred initialization.");
            Prepare("Sword parent cleared by deferred transformation");
            inventory.GiveTreasure(TreasureId.Sword,1); inventory.EquipA(TreasureId.Sword); Ring(0x2a);
            Step(sword:true,pressed:true);
            FailIf(!player.IsAttacking || player.ActiveTransformation != 0,context + ": selector runs after the Sword parent, before transformed initialization.");
            Step(sword:true);
            FailIf(player.IsAttacking || player.ActiveTransformation != 5,context + ": transformed state0 must clear the parent and orphan its physical child.");
            Step(3);
            Prepare("carrying blocks ring selection",new(88,50),0);
            compareCarry = true;
            byte liftable = (byte)(game == OracleGame.Ages ? 0xc3 : 0xe5);
            room.SetPositionTileAndCollision(new(88,24),liftable,0x0f,0); room.SetUnderlyingMetatile(new(88,24),floor); native.CopyRoom(room);
            inventory.GiveTreasure(TreasureId.Bracelet,1); inventory.EquipA(TreasureId.Bracelet); native.LoadSave(rooms.SaveData);
            Step(24,0); Step(sword:true,pressed:true); Step(11,16,sword:true); Step(20);
            FailIf(!player.IsCarryingObject || native[0xcc5a] != 0x83,context + ": source carry gate needs a real reachable pickup.");
            Ring(0x2a); Step(8,16);
            FailIf(player.ActiveTransformation != 0 || !player.IsCarryingObject,context + ": carrying must retain Link's normal identity.");
            Step(sword:true,pressed:true); Step(20);
            FailIf(player.ActiveTransformation != 5 || player.IsCarryingObject,context + ": releasing the held object permits the subsequent transformation.");
            Prepare("water handoff"); inventory.GiveTreasure(TreasureId.Flippers,1);
            // Seasons treasure $4a is the Pirate's Bell; it must not select
            // Ages' Mermaid entry delay, impulse or swimming mode.
            if (game == OracleGame.Seasons) inventory.GiveTreasure(0x4a,1);
            Ring(0x2a); Step(2);
            for (int y = 1; y < 7; y++)
            for (int x = 6; x < 9; x++) room.SetPositionTileAndCollision(new(x * 16 + 8,y * 16 + 8),(byte)(game == OracleGame.Ages ? 0xfa : 0xfd),0,0);
            native.CopyRoom(room);
            for (int walk = 0; walk < 40 && player.ActiveTransformation != 0; walk++) Step(angle:8);
            FailIf(player.ActiveTransformation != 0 || player.TopDownSwimmingState != 1 || native[0xcc5d] != 1,
                context + ": water must publish swimming state1 before restoring normal Link.");
            Step(); compareSwimming = true;
            Step(16);
            Step(divePressed:true); Step(119);
            FailIf(!player.TopDownDiving,context + ": diving must retain its native bit through update $77.");
            Step();
            FailIf(player.TopDownDiving,context + ": the $78th eligible dive update must surface.");
            Step(angle:0,sword:true,pressed:true); Step(24,0); Step(12,16); Step(16);
            if (game == OracleGame.Seasons)
            {
                Prepare("ring cannot jump from stump",new(48.25f,66.5f),8);
                room.SetPositionTileAndCollision(new(72,72),0x20,room.GetCollision(0x20),0); native.CopyRoom(room);
                for (int walk = 0; walk < 64 && native[0xcc5c] == 0; walk++) Step(angle:8);
                Step(40); Ring(0x2a); Step(2);
                Vector2 stump = player.PrecisePosition;
                Step(16,0);
                FailIf(player.ActiveTransformation != 5 || player.PrecisePosition != stump || player.TopDownAirborne,
                    context + ": transformed Link faces/animates but remains on the stump.");
                Ring(0xff); Step(4); Step(angle:0); Step(40);
            }
        }
        finally
        {
            root._scene.Dialogue.Close(); entities.Clear(); inventory.SetScriptedActiveRing(0xff);
            memory.SetField("wDisableRingTransformations",0); memory.SetField("wMenuDisabled",0); memory.SetField("wInShop",0);
            memory.SetField("wDisabledObjects",0); memory.SetField("wPegasusSeedCounter",0); memory.SetField("wPegasusSeedCounter",0,1);
            player.WarpTo(new(72,80));
            foreach (var tile in original) room.SetPositionTileAndCollision(tile.Point,tile.Tile,tile.Collision,0);
        }
    }
}
