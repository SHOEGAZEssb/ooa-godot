using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedActiveTiles(FrontendValidationRoot root, bool batched)
    {
        var rooms = root._gameplaySession!.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player;
        var inventory = player.Inventory;
        var memory = rooms.RuntimeState;
        var room = rooms.CurrentRoom;
        var game = rooms.SaveData.Game;
        var app = new ApplicationValidationFixture(root);
        var original = (from y in Enumerable.Range(0,room.HeightInTiles) from x in Enumerable.Range(0,room.WidthInTiles)
            let point = new Vector2(x * 16 + 8,y * 16 + 8)
            select (Point:point,Tile:room.GetMetatile(point),Collision:room.GetTerrainInfo(point).Collision)).ToArray();
        string[] fields = ["wActiveTilePos","wActiveTileIndex","wStandingOnTileCounter","wActiveTileType","wLastActiveTileType","wIsTileSlippery"];
        BraceletRom native = null!;
        string context = "";
        int update = 0;
        OracleRandomState seed = default;
        ValidationSoundRequestAudit sounds = null!;
        byte floor = (byte)(game == OracleGame.Ages ? 0x3a : 0x04);
        void Prepare(string name, byte tile, Vector2? start = null)
        {
            entities.Clear(); inventory.SetScriptedActiveRing(0xff); inventory.EquipA(0); inventory.EquipB(0);
            memory.SetField("wDisableRingTransformations",0);
            memory.SetField("wLinkImmobilized",0);
            foreach (string field in fields) memory.SetField(field,0);
            player.WarpTo(start ?? new(40.25f,64.5f)); player.Face(Vector2I.Right);
            foreach (var entry in original) room.SetPositionTileAndCollision(entry.Point,tile,0,0);
            native = new(room,player.PrecisePosition,1,0xff,game:game);
            native.LoadSave(rooms.SaveData); native.InitializeWalkingAnimation();
            native[0xd009] = unchecked((byte)player.NativePlanarAngle);
            native[0xcc2d] = (byte)rooms.ActiveGroup; native[0xcd00] = 1;
            seed = entities.Random.CaptureState(); native[0xff94] = seed.Rng1; native[0xff95] = seed.Rng2;
            sounds = root._sound.AttachPlayRequestAudit(); context = $"{game}: {name}, batch={batched}"; update = 0;
        }
        void Compare()
        {
            update++;
            foreach (var pair in fields.Select((field,index) => (field,index)))
                FailIf(memory.ReadField(pair.field) != native[0xcc99 + pair.index],
                    $"{context} update {update}: {pair.field} runtime=${memory.ReadField(pair.field):x2}, native=${native[0xcc99 + pair.index]:x2}.");
            FailIf(player.PrecisePosition != new Vector2(native.Word(0xd00c) / 256f,native.Word(0xd00a) / 256f) ||
                (ushort)player.ItemCreationZFixed != native.Word(0xd00e) || player.ActiveTransformation != native[0xd001],
                $"{context} update {update}: native XYZ/identity differs: runtime={player.PrecisePosition}/${(ushort)player.ItemCreationZFixed:x4}/{player.ActiveTransformation}, native={native.Word(0xd00c) / 256f},{native.Word(0xd00a) / 256f}/${native.Word(0xd00e):x4}/{native[0xd001]}.");
            FailIf(!sounds.Requests.Where(id => id != SoundId.SndText).SequenceEqual(native.Sounds),context + $" update {update}: terrain cue order differs: runtime={string.Join(',',sounds.Requests)}, native={string.Join(',',native.Sounds)}, animationParam=${memory.ReadWramByte(0xd021):x2}/ROM${native[0xd021]:x2}.");
            if (native[0xd004] == 1 && native[0xd030] == 0x10 && player.ActiveTransformation == 0)
                foreach (int address in new[] { 0xd020, 0xd021, 0xd022, 0xd023, 0xd030, 0xd031 })
                    FailIf(memory.ReadWramByte(address) != native[address],
                        $"{context} update {update}: WALK byte ${address:x4} runtime${memory.ReadWramByte(address):x2}/ROM${native[address]:x2}.");
            var rng = entities.Random.CaptureState();
            FailIf(rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95] || rng.Calls != seed.Calls + native.RandomCalls,
                context + ": native shared RNG differs.");
        }
        void Step(int count = 1, int angle = 0xff, bool jump = false)
        {
            int keys = angle switch { 0 => 0x40,8 => 0x10,16 => 0x80,24 => 0x20,_ => 0 };
            app.Step(count,angle < 0x80 ? OracleObjectMath.StrictCardinalVector(angle) : Vector2.Zero,
                held:jump ? ["attack"] : [],pressed:jump ? ["attack"] : [],batched:batched,
                afterUpdate:() => { native.UpdateLinkItems(entities.FrameCounter,keys | (jump ? 1 : 0),interactions:true); Compare(); });
        }
        try
        {
            foreach (Vector2 boundary in new[] { new Vector2(80.25f,123.5f),new Vector2(240.25f,251.5f) })
            {
                Prepare("native unused layout rows and wrapped foot coordinates",floor,boundary);
                memory.SetField("wScrollMode",0); native[0xcd00] = 0; Step(4);
            }
            Prepare("native layout alias above $cfbf",floor,new(224.25f,203.5f));
            memory.SetField("wScrollMode",0); native[0xcd00] = 0;
            byte alias = memory.ReadWramByte(0xcfde);
            try { memory.SetWramByte(0xcfde,0xf8); native[0xcfde] = 0xf8; Step(4); }
            finally { memory.SetWramByte(0xcfde,alias); }
            memory.SetField("wScrollMode",1);
            Prepare("normal/grass/stairs/puddle boundaries",floor);
            for (int y = 1; y < 7; y++)
            for (int x = 3; x <= 5; x++) room.SetPositionTileAndCollision(new(x * 16 + 8,y * 16 + 8),
                (byte)(x == 3 ? 0xf8 : x == 4 ? 0xd0 : game == OracleGame.Ages ? 0xf9 : 0xfa),0,0);
            native.CopyRoom(room); Step(96,8); Step(4);
            Prepare("standing counter byte wrap",0xf8); Step(260);
            FailIf(memory.ReadField("wStandingOnTileCounter") != 4,context + ": grass must retain the native wrapping byte counter.");
            Prepare("text retains active fields",0xf8); Step(4);
            root._scene.Dialogue.ShowMessage("Tile publication pause.",player.Position.Y); native[0xcba0] = 1;
            Step(6,8); root._scene.Dialogue.Close(); native[0xcba0] = 0; Step(3,8);
            Prepare("Feather clears type but retains index/position in air",0xf8);
            inventory.GiveTreasure(TreasureId.Feather,1); inventory.EquipA(TreasureId.Feather); native.LoadSave(rooms.SaveData);
            Step(4); Step(angle:8,jump:true); Step(32,8);
            Prepare("ice publication and retained momentum",(byte)(game == OracleGame.Ages ? 0xea : 0xdc));
            Step(12,8); Step(8); Step(8,24);
            Prepare("Baby retains preceding grass type and speed column",0xf8); Step(3);
            player.QueueBabyTransformation(); native[0xcc57] = 0x82;
            native[0xd002] = native[0xd003] = native[0xd004] = native[0xd005] = 0;
            Step();
            foreach (var entry in original) room.SetPositionTileAndCollision(entry.Point,floor,0,0);
            native.CopyRoom(room); Step(24,8); Step(480);
            Prepare("normal hole pull retains later state01 phases",0xf3);
            bool sawImmobilized = false;
            for (int walk = 0; walk < 96 && native[0xd004] != 2; walk++)
            {
                Step(angle:8);
                sawImmobilized |= memory.ReadField("wStandingOnTileCounter") >= 0x10;
                if (native[0xd004] == 1)
                    FailIf(native[0xd031] != memory.ReadWramByte(0xd031) ||
                        native[0xd020] != memory.ReadWramByte(0xd020),
                        context + $" update {update}: immobilization must preserve the later WALK animation phase.");
            }
            FailIf(!sawImmobilized || native[0xd004] != 2 || !player.IsFallingInHole,
                context + ": hole geometry must exercise partial control, immobilization and state02 selection.");
            Step(4);
        }
        finally
        {
            root._scene.Dialogue.Close(); entities.Clear(); inventory.SetScriptedActiveRing(0xff);
            memory.SetField("wScrollMode",1);
            player.WarpTo(new(72,80));
            foreach (var entry in original) room.SetPositionTileAndCollision(entry.Point,entry.Tile,entry.Collision,0);
        }
    }
}
