using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedSeedFactory(FrontendValidationRoot root, bool batched)
    {
        var rooms = root._gameplaySession!.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player;
        var app = new ApplicationValidationFixture(root);
        var data = new SeedSatchelDatabase(rooms.AssetRoot);
        OracleGame game = rooms.SaveData.Game;
        entities.Clear(); player.WarpTo(new(72,80)); player.Face(Vector2I.Up);
        player.Inventory.EquipA(0); player.Inventory.EquipB(0);
        var random = entities.Random.CaptureState();
        var native = new BraceletRom(rooms.CurrentRoom,player.Position,0,0xff,game:game);
        native.LoadSave(rooms.SaveData);
        native[0xff94] = random.Rng1; native[0xff95] = random.Rng2;
        // Stage a cleared lower slot after constructing the higher child.
        // Native state-zero RNG follows physical slot order, not birth order.
        var placeholder = entities.Spawn<EmberSeedEffect>(new EmberSeedSpawn(new(72,64),Vector2I.Up,data.Ember,rooms.ActiveGroup));
        var higher = entities.Spawn<EmberSeedEffect>(new EmberSeedSpawn(new(88,64),Vector2I.Up,data.Mystery,rooms.ActiveGroup));
        entities.RetireEntity(entities.EntityAdapters<EmberSeedRoomEntity>().Single(entity => ReferenceEquals(entity.Node,placeholder)));
        var lower = entities.Spawn<EmberSeedEffect>(new EmberSeedSpawn(new(56,64),Vector2I.Up,data.Mystery,rooms.ActiveGroup));
        var allocated = entities.Random.CaptureState();
        FailIf(allocated.Rng1 != random.Rng1 || allocated.Rng2 != random.Rng2 || allocated.Calls != random.Calls ||
            higher.MysteryEffect != -1 || lower.MysteryEffect != -1,
            $"{game}: Mystery allocation/preload must leave native RNG untouched until item state zero.");
        foreach (var (slot,position) in new[] { (0xd700,new Vector2(56,64)), (0xd800,new Vector2(88,64)) })
        {
            native[slot] = 1; native[slot + 1] = 0x24;
            native.Word(slot + 10,(int)position.Y * 256); native.Word(slot + 12,(int)position.X * 256);
        }
        for (int update = 1; update <= 5; update++)
        {
            byte mask = (byte)(update <= 3 ? 0x90 : 0);
            entities.RuntimeState.SetField("wDisabledObjects",mask); native[0xcc8a] = mask;
            native.Update(item:0);
            app.Step(1,Vector2.Zero,batched:batched);
            var actual = entities.Random.CaptureState();
            FailIf(lower.MysteryEffect != native[0xd703] || higher.MysteryEffect != native[0xd803] ||
                lower.ZFixed != unchecked((short)native.Word(0xd70e)) || higher.ZFixed != unchecked((short)native.Word(0xd80e)) ||
                lower.PrecisePosition != new Vector2(native.Word(0xd70c) / 256f,native.Word(0xd70a) / 256f) ||
                higher.PrecisePosition != new Vector2(native.Word(0xd80c) / 256f,native.Word(0xd80a) / 256f) ||
                actual.Rng1 != native[0xff94] || actual.Rng2 != native[0xff95] || actual.Calls != random.Calls + 2,
                $"{game}: Mystery slot reuse/state-zero RNG/mask ${mask:x2} differs on item update {update}.");
        }
        entities.RuntimeState.SetField("wDisabledObjects",0); entities.Clear();
        random = entities.Random.CaptureState();
        var cancelled = entities.Spawn<EmberSeedEffect>(new EmberSeedSpawn(new(72,64),Vector2I.Up,data.Mystery,rooms.ActiveGroup));
        entities.RetireEntity(entities.EntityAdapters<EmberSeedRoomEntity>().Single(entity => ReferenceEquals(entity.Node,cancelled)));
        var retired = entities.Random.CaptureState();
        FailIf(retired.Rng1 != random.Rng1 || retired.Rng2 != random.Rng2 || retired.Calls != random.Calls,
            $"{game}: retiring a Mystery child before state zero must not draw RNG.");
    }
}
