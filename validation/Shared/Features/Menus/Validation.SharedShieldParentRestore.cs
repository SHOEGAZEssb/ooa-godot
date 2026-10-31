using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedShieldParentRestore(FrontendValidationRoot root, bool batched)
    {
        var rooms = root._gameplaySession!.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player;
        var inventory = player.Inventory;
        player.ClearNativeItemParents(); entities.Clear();
        // Earlier system fixtures mutate terrain; debug reconstruction reloads the source layout.
        rooms.ReloadCurrentRoomLayout();
        Vector2? dry = null;
        for (int y=24;y<104 && dry is null;y+=16)
        for (int x=24;x<136;x+=16)
            if (!player.World.Collides(new(x,y)) && player.World.GetActiveTerrain(new(x,y)).Terrain.Hazard==HazardType.None)
            { dry=new(x,y); break; }
        FailIf(dry is null,"Shield debug parent fixture needs actual dry floor.");
        player.WarpTo(dry!.Value); player.Face(Vector2I.Down);
        inventory.GiveTreasure(TreasureId.Shield,1); inventory.EquipA(TreasureId.Shield); inventory.EquipB(0);
        var app = new ApplicationValidationFixture(root);
        app.Step(1,Vector2.Zero,batched:batched);
        app.Step(2,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched);
        FailIf(!player.IsUsingShield || !player.ShieldParentInitialized,"Shield must initialize before its actual debug capture.");
        byte[] header=Enumerable.Range(0,64).Select(offset=>rooms.RuntimeState.ReadWramByte(0xd500+offset)).ToArray();
        var snapshot=root.CaptureDebugSavestate();
        root.RestoreDebugSavestate(snapshot);
        player=root._scene.Player; rooms=root._gameplaySession!.Rooms;
        FailIf(player.World.GetActiveTerrain(player.Position).Terrain.Hazard!=HazardType.None,
            $"{rooms.SaveData.Game}: Shield restore must retain an actual source dry-floor position.");
        FailIf(!player.IsUsingShield || !player.ShieldParentInitialized || player.ShieldParentButton!=1 ||
            Enumerable.Range(0,64).Any(offset=>rooms.RuntimeState.ReadWramByte(0xd500+offset)!=header[offset]),
            $"{rooms.SaveData.Game}: shared debug reconstruction must retain the exact initialized Shield parent.");
        var sound=root._sound.AttachPlayRequestAudit();
        app.Step(2,Vector2.Zero,held:["attack"],batched:batched,afterUpdate:()=>
            FailIf(!player.IsUsingShield || sound.RequestsFor(SoundId.SndShield)!=0,
                $"{rooms.SaveData.Game}: restored Shield must keep its owner without replaying state-zero initialization: using={player.IsUsingShield}, initialized={player.ShieldParentInitialized}, button=${player.ShieldParentButton:x2}, cues={sound.RequestsFor(SoundId.SndShield)}, XY={player.Position}, inputDisabled={player.World.ItemUsageDisabled}, parent=${rooms.RuntimeState.ReadWramByte(0xd500):x2}/${rooms.RuntimeState.ReadWramByte(0xd501):x2}."));
        app.Step(1,Vector2.Zero,batched:batched);
        FailIf(player.IsUsingShield || Enumerable.Range(0,64).Any(offset=>rooms.RuntimeState.ReadWramByte(0xd500+offset)!=0),
            "Releasing restored Shield must delete its full native parent.");
        app.Step(2,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched);
        FailIf(!player.IsUsingShield || !player.ShieldParentInitialized || sound.RequestsFor(SoundId.SndShield)!=1,
            "Using Shield again after restoring and releasing must initialize one new parent with one cue.");
        app.Step(1,Vector2.Zero,batched:batched);
        FailIf(player.IsUsingShield || rooms.RuntimeState.ReadWramByte(0xd500)!=0,
            "Releasing the repeated Shield must clear its parent again.");
    }
}
