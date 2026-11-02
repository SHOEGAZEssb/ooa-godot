
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedParentAnimation(FrontendValidationRoot root, bool batched)
    {
        var rooms = root._gameplaySession!.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player;
        var inventory = player.Inventory;
        var memory = rooms.RuntimeState;
        var game = rooms.SaveData.Game;
        var app = new ApplicationValidationFixture(root);
        entities.Clear();
        var room = rooms.CurrentRoom;
        var tiles = (from y in Enumerable.Range(1, 6) from x in Enumerable.Range(1, 8)
            let point = new Vector2(x * 16 + 8, y * 16 + 8)
            select (Point: point, Tile: room.GetMetatile(point), Collision: room.GetTerrainInfo(point).Collision)).ToArray();
        foreach (var tile in tiles)
            room.SetPositionTileAndCollision(tile.Point, (byte)(game == OracleGame.Ages ? 0x3a : 0x04), 0, 0);
        try
        {
            player.WarpTo(new(72, 64)); player.Face(Vector2I.Down);
            inventory.GiveTreasure(TreasureId.SeedSatchel, 1); inventory.GiveTreasure(TreasureId.EmberSeeds, 0x20);
            inventory.SelectSatchelSeeds(0); inventory.SetScriptedEquippedItems(0, TreasureId.SeedSatchel);
            var native = new BraceletRom(room, player.PrecisePosition, 2, 0xff, game: game);
            native.LoadSave(rooms.SaveData); native.InitializeWalkingAnimation();
            native[0xcc2d] = (byte)rooms.ActiveGroup;
            void Step(int count, bool press = false) => app.Step(count, Vector2.Zero,
                held: press ? ["attack"] : [], pressed: press ? ["attack"] : [], batched: batched,
                afterUpdate: () =>
                {
                    native.UpdateLinkItems(entities.FrameCounter, press ? 1 : 0);
                    CompareParentAnimationPublicationRom(player, address => native[address], $"{game}: parent picture batch={batched}");
                });
            Step(1);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Step(1, true);
                var picture = player.PublishedParentBody ?? throw new InvalidOperationException($"{game}: Satchel did not publish its body picture.");
                FailIf(picture.Texture != player.Sprites.AttackTexture || picture.Region != new Rect2(16, 32, 16, 16) ||
                    native[0xd032] != 0xb2 || !player.IsUsingSeedSatchel,
                    $"{game}: native throw graphic$b2 must publish the ordinary downward throw body.");
                int children = entities.Entities<EmberSeedEffect>().Count;
                player.ClearNativeItemParents(); native.ClearItemParents();
                FailIf(player.IsUsingSeedSatchel || children != 1 || entities.Entities<EmberSeedEffect>().Count != children ||
                    !Equals(player.PublishedParentBody, picture) || native[0xd032] != 0xb2 ||
                    memory.ReadWramByte(0xd33f) != 0 || native[0xd33f] != 0,
                    $"{game}: clearAllParentItems must release the claim without changing the loaded body picture or physical child.");
                Step(1);
                FailIf(player.PublishedParentBody is not null || native[0xd032] != 0x56,
                    $"{game}: the next graphics pass must replace the retained throw picture with ordinary downward WALK.");
                for (int guard = 0; entities.Entities<EmberSeedEffect>().Count != 0 && guard < 240; guard++) Step(1);
                FailIf(entities.Entities<EmberSeedEffect>().Count != 0,
                    $"{game}: physical seed must complete before repeating the real source allocation.");
            }
        }
        finally
        {
            player.ClearNativeItemParents(); entities.ClearPhysicalPlayerItems();
            foreach (var tile in tiles) room.SetPositionTileAndCollision(tile.Point, tile.Tile, tile.Collision, 0);
        }
    }
}
