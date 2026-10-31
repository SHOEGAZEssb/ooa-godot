using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedFloorBreaking(FrontendValidationRoot root, bool batched)
    {
        var rooms = root._gameplaySession!.Rooms;
        int originGroup = rooms.ActiveGroup, originRoom = rooms.CurrentRoom.Id;
        var game = rooms.SaveData.Game;
        if (game == OracleGame.Ages) root.LoadDebugRoom(4,0x00);
        var room = rooms.CurrentRoom;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player;
        var inventory = player.Inventory;
        var memory = rooms.RuntimeState;
        var app = new ApplicationValidationFixture(root);
        var original = (from y in Enumerable.Range(0,room.HeightInTiles) from x in Enumerable.Range(0,room.WidthInTiles)
            let point = new Vector2(x * 16 + 8,y * 16 + 8)
            select (Point:point,Tile:room.GetMetatile(point),Collision:room.GetTerrainInfo(point).Collision)).ToArray();
        string[] fields = ["wActiveTilePos","wActiveTileIndex","wStandingOnTileCounter","wActiveTileType","wLastActiveTileType","wIsTileSlippery"];
        BraceletRom native = null!;
        OracleRandomState seed = default;
        ValidationSoundRequestAudit sounds = null!;
        string context = "";
        int update = 0;
        byte floor = (byte)(game == OracleGame.Ages ? 0xa0 : 0x04);
        byte cracked = (byte)(game == OracleGame.Ages ? 0x4d : 0xc9);
        Vector2 target = new(40,72);
        void Prepare(string name, byte tile, Vector2? start = null)
        {
            for (int drain = 0; drain < 8; drain++) rooms.UpdateChangedTileGraphics(1);
            entities.Clear(); inventory.EquipA(0); inventory.EquipB(0); inventory.SetScriptedActiveRing(0xff);
            memory.SetField("wDisableRingTransformations",0); memory.SetField("wLinkImmobilized",0);
            foreach (string field in fields) memory.SetField(field,0);
            player.WarpTo(start ?? new(40.25f,64.5f)); player.Face(Vector2I.Right);
            foreach (var entry in original) room.SetPositionTileAndCollision(entry.Point,floor,0,0);
            room.SetPositionTileAndCollision(target,tile,0,0);
            native = new(room,player.PrecisePosition,1,0xff,game:game);
            native.LoadSave(rooms.SaveData); native.InitializeWalkingAnimation();
            native[0xd009] = unchecked((byte)player.NativePlanarAngle);
            native[0xcc2d] = (byte)rooms.ActiveGroup; native[0xcd00] = 1;
            seed = entities.Random.CaptureState(); native[0xff94] = seed.Rng1; native[0xff95] = seed.Rng2;
            sounds = root._sound.AttachPlayRequestAudit(); context = $"{game}: {name}, batch={batched}"; update = 0;
        }
        void Ring(int ring) { inventory.SetScriptedActiveRing(ring); native.LoadSave(rooms.SaveData); }
        void Compare()
        {
            update++;
            for (int index = 0; index < fields.Length; index++)
                FailIf(memory.ReadField(fields[index]) != native[0xcc99 + index],
                    $"{context} update {update}: {fields[index]} runtime=${memory.ReadField(fields[index]):x2}, native=${native[0xcc99 + index]:x2}.");
            FailIf(player.PrecisePosition != new Vector2(native.Word(0xd00c) / 256f,native.Word(0xd00a) / 256f) ||
                (ushort)player.ItemCreationZFixed != native.Word(0xd00e) || player.ActiveTransformation != native[0xd001],
                $"{context} update {update}: native XYZ/identity differs: runtime={player.PrecisePosition}/${(ushort)player.ItemCreationZFixed:x4}/{player.ActiveTransformation}, native={native.Word(0xd00c) / 256f},{native.Word(0xd00a) / 256f}/${native.Word(0xd00e):x4}/{native[0xd001]}.");
            FailIf(!sounds.Requests.Where(id => id != SoundId.SndText).SequenceEqual(native.Sounds),
                context + $" update {update}: break/swim/item cues differ: runtime={string.Join(',',sounds.Requests)}, native={string.Join(',',native.Sounds)}.");
            var rng = entities.Random.CaptureState();
            FailIf(rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95] || rng.Calls != seed.Calls + native.RandomCalls,context + ": shared RNG differs.");
            int packed = room.GetPackedPosition(target);
            FailIf(room.GetMetatile(target) != native[0xcf00 + packed] || room.GetTerrainInfo(target).Collision != native[0xce00 + packed],
                context + $" update {update}: logical replacement/collision differs.");
            FailIf(SomariaPrivate<ChangedTileQueue>(rooms,"_changedTiles").Count != ((native[0xcce0] - native[0xccdf]) & 31),
                context + $" update {update}: changed-tile queue phase differs.");
            var effects = entities.Entities<FallingDownHoleEffect>();
            var slots = Enumerable.Range(0xd2,14).Where(page => native[(page << 8) + 0x40] != 0 &&
                native[(page << 8) + 0x41] == 0x0f && native[(page << 8) + 0x42] == 0x80).ToArray();
            FailIf(effects.Count != slots.Length,context + $" update {update}: silent floor effect allocation/lifetime differs.");
            foreach (var effect in effects)
            {
                int slot = slots.Single(page => page == 0xd0 + entities.InteractionSlot(effect)) << 8;
                FailIf(effect.PrecisePosition != new Vector2(native.Word(slot + 0x4c) / 256f,native.Word(slot + 0x4a) / 256f) ||
                    effect.CurrentParameter != native[slot + 0x61] || effect.Initialized != (native[slot + 0x44] != 0),
                    context + $" update {update}: floor effect state0/position/animation differs.");
            }
        }
        void Step(int count = 1, int angle = 0xff, bool item = false)
        {
            int keys = angle switch { 0 => 0x40,8 => 0x10,16 => 0x80,24 => 0x20,_ => 0 };
            app.Step(count,angle < 0x80 ? OracleObjectMath.StrictCardinalVector(angle) : Vector2.Zero,
                held:item ? ["attack"] : [],pressed:item ? ["attack"] : [],batched:batched,
                afterUpdate:() => { native.UpdateLinkItems(entities.FrameCounter,keys | (item ? 1 : 0),interactions:true); native.AdvanceChangedTiles(); Compare(); });
        }
        try
        {
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Prepare($"cracked floor repeat={repeat}",cracked); Step(31);
                FailIf(room.GetMetatile(target) != cracked || sounds.Requests.Count != 0,context + ": floor must remain intact through update$1f.");
                Step();
                FailIf(room.GetMetatile(target) != 0xf3 || memory.ReadField("wActiveTileType") != 3 || memory.ReadField("wStandingOnTileCounter") != 0 ||
                    sounds.RequestsFor(SoundId.SndRumble) != 1,context + ": update$20 must mutate/rumble but retain the old type and reset its standing byte.");
                Step(35);
            }
            Prepare("Roc's Ring cancels standing accumulation",cracked); Ring(0x22); Step(40);
            Ring(0xff); Step(16);
            root._scene.Dialogue.ShowMessage("Cracked floor pause.",player.Position.Y); native[0xcba0] = 1;
            Step(4); root._scene.Dialogue.Close(); native[0xcba0] = 0; Step(16); Step(4);
            Prepare("leaving cancels the floor countdown",cracked); Step(20); Step(8,8); Step(8,24); Step(31);
            Prepare("Feather allocated on the breaking update",cracked);
            inventory.GiveTreasure(TreasureId.Feather,1); inventory.EquipA(TreasureId.Feather); native.LoadSave(rooms.SaveData);
            Step(31); Step(angle:8,item:true); Step(4,8);
            Prepare("centered breaking update retains Feather eligibility",cracked,new(40.25f,72.5f));
            inventory.GiveTreasure(TreasureId.Feather,1); inventory.EquipA(TreasureId.Feather); native.LoadSave(rooms.SaveData);
            Step(31); Step(angle:8,item:true); Step(4,8);
            Prepare("transformed Link uses the same floor break and forced return",cracked); Ring(0x2a); Step(2); Step(30);
            FailIf(memory.ReadField("wStandingOnTileCounter") != 31,context + ": deferred transformation initialization must not apply terrain.");
            Step(); Step(35);
            Prepare("full interaction pool still breaks and rumbles",cracked); Step(31);
            for (int index = 0; index < 14; index++)
            {
                FailIf(!entities.TryCreateKnockbackDust(new(96,40),0),context + ": pool fixture requires fourteen free slots.");
                int at = (0xd2 + index) * 256 + 0x40;
                native[at] = 1; native[at + 1] = 0x0f; native[at + 2] = 1; native[at + 0x0b] = 40; native[at + 0x0d] = 96;
            }
            Step(); Step(8);
            Prepare("full changed-tile queue retains native failure side effects",cracked); Step(31);
            for (int index = 0; index < 31; index++)
            {
                FailIf(!rooms.TrySetTile(0x11,floor),context + ": tile queue fixture must fill all31 entries.");
                native.SetTile(0x11,floor);
            }
            Step();
            FailIf(room.GetMetatile(target) != cracked || sounds.RequestsFor(SoundId.SndRumble) != 1 ||
                entities.Entities<FallingDownHoleEffect>().Count != 1,context + ": failed setTile must still reset standing, rumble and allocate its silent effect.");
            Step(32);
            if (game == OracleGame.Seasons)
            {
                foreach (int ring in new[] { 0xff,0x21 })
                {
                    Prepare($"cracked ice, ring=${ring:x2}",0xdb); Ring(ring);
                    inventory.GiveTreasure(TreasureId.Flippers,1); native.LoadSave(rooms.SaveData); Step(31);
                    FailIf(room.GetMetatile(target) != 0xdb,context + ": ice must survive31 eligible updates.");
                    Step();
                    FailIf(room.GetMetatile(target) != 0xfd || memory.ReadField("wActiveTileType") != 0x0e || player.TopDownSwimmingState != 2 ||
                        sounds.RequestsFor(SoundId.SndRumble) != 0 || entities.Entities<FallingDownHoleEffect>().Count != 0,
                        context + ": ice must enter swimming without cracked-floor feedback, retaining its type for this update.");
                    Step(16);
                }
            }
        }
        finally
        {
            root._scene.Dialogue.Close(); entities.Clear(); inventory.SetScriptedActiveRing(0xff); player.WarpTo(new(72,80));
            for (int drain = 0; drain < 8; drain++) rooms.UpdateChangedTileGraphics(1);
            foreach (var entry in original) room.SetPositionTileAndCollision(entry.Point,entry.Tile,entry.Collision,0);
            if (game == OracleGame.Ages) root.LoadDebugRoom(originGroup,originRoom);
        }
    }
}
