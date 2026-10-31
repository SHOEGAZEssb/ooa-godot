using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedBracelet(FrontendValidationRoot root, bool batched)
    {
        var session = root._gameplaySession!;
        var rooms = session.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player;
        var inventory = player.Inventory;
        var controller = ((PlayerWorld)player.World).Bracelet;
        var app = new ApplicationValidationFixture(root);
        OracleGame game = rooms.SaveData.Game;
        byte floor = (byte)(game == OracleGame.Ages ? 0x3a : 0x04);
        if (game == OracleGame.Seasons)
        {
            entities.Clear();
            player.WarpTo(new(88, 82));
            rooms.CurrentRoom.SetPositionTileAndCollision(new(88, 87), 0x20, 0, 0);
            inventory.GiveTreasure(TreasureId.Bracelet, 1);
            inventory.EquipA(TreasureId.Bracelet); inventory.EquipB(0);
            var stump = new BraceletRom(rooms.CurrentRoom, player.Position, 0, 0xff, game: game);
            stump[0xcc9c] = 8; // tileTypeMappings.s: $20 -> TILETYPE_STUMP $08.
            app.Step(1, Vector2.Zero, held: ["attack"], pressed: ["attack"], batched: batched,
                afterUpdate: () => stump.Update(held: true, pressed: true));
            FailIf(controller.State != BraceletState.Idle || stump[0xd200] != 0 || player.IsCarryingObject,
                "Seasons: a stump must reject Bracelet before wall/object grabbing, as in native parent state0.");
        }
        byte[] tiles = game == OracleGame.Ages ? [0xc3] : [0xe5, 0xd7];
        foreach (byte tile in tiles)
        foreach (bool primary in new[] { true, false })
        {
            entities.Clear();
            player.WarpTo(new(88, 82));
            player.Face(Vector2I.Up);
            inventory.GiveTreasure(TreasureId.Bracelet, 1);
            inventory.EquipA(primary ? TreasureId.Bracelet : 0);
            inventory.EquipB(primary ? 0 : TreasureId.Bracelet);
            var room = rooms.CurrentRoom;
            for (int y = 16; y < 112; y += 16)
            for (int x = 16; x < 144; x += 16)
                room.SetPositionTileAndCollision(new(x + 8, y + 8), floor, 0, 0);
            Vector2 center = new(88, 56);
            room.SetPositionTileAndCollision(center, tile, 0x0f, 0);
            room.SetUnderlyingMetatile(center, floor);
            var rom = new BraceletRom(room, player.Position, 0, 0xff, game: game);
            rom[0xcc2d] = (byte)rooms.ActiveGroup;
            rom[0xcc2e] = (byte)(room.Width > 160 ? 1 : 0);
            app.Step(24, Vector2.Up, batched: batched, afterUpdate: () => rom.Walk(0));
            FailIf(player.Position != new Vector2(88, 66) || rom[0xd00b] != 66 || player.World.Collides(player.Position),
                $"{game}: shared Bracelet must approach tile ${tile:x2} through actual collision geometry.");
            var rng = entities.Random.CaptureState();
            rom[0xff94] = rng.Rng1; rom[0xff95] = rng.Rng2;
            var audit = root._sound.AttachPlayRequestAudit();
            string button = primary ? "attack" : "item";
            int updates = 0;
            void Compare()
            {
                CompareParentAnimationPublicationRom(player, address => rom[address], $"{game}: Bracelet update{updates}");
                if (rom[0xd23f] != 0)
                    FailIf(rooms.RuntimeState.ReadWramByte(0xd231) != rom[0xd231],
                        $"{game}: Bracelet native parent graphic${rooms.RuntimeState.ReadWramByte(0xd231):x2}, ROM${rom[0xd231]:x2} on update{updates}.");
                BraceletState expected = rom[0xd200] == 0 ? rom.ChildActive ? BraceletState.Projectile : BraceletState.Idle : rom[0xd204] switch
                {
                    0 => BraceletState.SeekingWall, 1 => BraceletState.GrabbingWall, 2 => BraceletState.Lifting,
                    3 => BraceletState.Holding, 4 => BraceletState.Throwing,
                    _ => throw new InvalidOperationException($"Native Bracelet parent ${rom[0xd204]:x2} is not represented.")
                };
                FailIf(controller.State != expected || player.IsCarryingObject != (rom[0xcc5a] == 0x83) ||
                    player.BraceletLiftCollisionsDisabled != (rom[0xcc5a] == 0xc2),
                    $"{game}: shared Bracelet ${tile:x2}, A={primary}, state/Link ownership differs on update {updates}: {controller.State}/{expected}; native child={rom[0xdc0d]},{rom[0xdc0b]}, Z={unchecked((short)rom.Word(0xdc0e))}, speedZ={unchecked((short)rom.Word(0xdc14))}, speed={rom[0xdc10]}.");
                int packed = room.GetPackedPosition(center);
                FailIf(room.GetMetatile(center) != rom[0xcf00 + packed] || room.GetUnderlyingMetatile(center) != rom.Underlying(packed) ||
                    room.GetTerrainInfo(center).Collision != rom[0xce00 + packed],
                    $"{game}: shared Bracelet tile/underlying/collision mutation differs on update {updates}.");
                var child = controller.LiftedObject;
                FailIf((child is not null) != rom.ChildActive, $"{game}: shared Bracelet reserved $dc occupancy differs on update {updates}.");
                if (child is not null)
                {
                    Vector2 ground = child.Thrown ? child.GroundPosition : player.Position + new Vector2(child.Position.X, 0);
                    int z = child.Thrown ? child.ZFixed : (int)child.Position.Y << 8;
                    FailIf(ground != new Vector2(rom.Word(0xdc0c) / 256f, rom.Word(0xdc0a) / 256f) ||
                        z != unchecked((short)rom.Word(0xdc0e)) || child.Thrown != (rom[0xdc04] == 3),
                        $"{game}: shared Bracelet child XYZ/throw state differs on update {updates}.");
                    if (child.Thrown)
                        FailIf(child.SpeedRaw != rom[0xdc10] || child.SpeedZ != unchecked((short)rom.Word(0xdc14)),
                            $"{game}: shared Bracelet native throw/bounce velocity differs on update {updates}.");
                }
            }
            void Step(int count, int angle = 0xff, bool held = false, bool pressed = false)
            {
                int update = 0;
                app.Step(count, angle == 0xff ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(angle),
                    held: held || pressed ? [button] : [], pressed: pressed ? [button] : [], batched: batched,
                    afterUpdate: () => { rom.Probe(); rom.Update(angle, held || pressed, pressed && update++ == 0, primary, updateInteractions: true); updates++; Compare(); });
            }
            Step(1, held: true, pressed: true);
            Step(11, 16, held: true);
            root._scene.Dialogue.ShowMessage("Bracelet lift pause.", player.Position.Y); rom[0xcba0] = 1;
            Step(5);
            root._scene.Dialogue.Close(); rom[0xcba0] = 0;
            Step(20);
            FailIf(!controller.HoldingTile, $"{game}: actual A/B input must reach shared Bracelet holding state.");
            Step(1, primary ? 0 : 0xff, pressed: true);
            Step(90);
            FailIf(controller.State != BraceletState.Idle || controller.LiftedObject is not null || !audit.Requests.SequenceEqual(rom.Sounds),
                $"{game}: Bracelet ${tile:x2} completion or native effect cue order differs: runtime={string.Join(',',audit.Requests)}, native={string.Join(',',rom.Sounds)}.");
        }

        // Repeat a reachable pickup after completion, cancel through the room-reset
        // owner, then reuse the same parent for a real thrown-item/target handoff.
        for (int attempt = 0; attempt < 2; attempt++)
        {
            entities.Clear();
            player.WarpTo(new(88, 82)); player.Face(Vector2I.Up);
            inventory.EquipA(TreasureId.Bracelet); inventory.EquipB(0);
            var room = rooms.CurrentRoom;
            room.SetPositionTileAndCollision(new(88, 56), tiles[0], 0x0f, 0);
            app.Step(24, Vector2.Up, batched: batched);
            app.Step(1, Vector2.Zero, held: ["attack"], pressed: ["attack"], batched: batched);
            app.Step(11, Vector2.Down, held: ["attack"], batched: batched);
            app.Step(20, Vector2.Zero, batched: batched);
            FailIf(!controller.HoldingTile, $"{game}: shared Bracelet must permit collision-reachable repeat pickup.");
            if (attempt == 0)
            {
                var held = controller.LiftedObject!;
                player.WarpTo(new(88, 82));
                app.Step(2, Vector2.Zero, batched: batched);
                FailIf(controller.State != BraceletState.Idle || controller.LiftedObject is not null ||
                    player.IsCarryingObject || held.GetParent() is not null || !held.IsQueuedForDeletion(),
                    $"{game}: room-reset cancellation must retire the shared Bracelet child and release Link.");
                continue;
            }
            FailIf(!entities.TrySpawnDebugEnemy(0x32, 0, new(88, 104), out string error), error);
            var target = entities.Entities<KeeseCharacter>().Single();
            app.Step(1, Vector2.Zero, batched: batched);
            target.Health = 9;
            int collision = game == OracleGame.Ages ? 0x16 : 0x14;
            int mode = game == OracleGame.Ages ? 0x1f : 0x29;
            var native = new ObjectCollisionRom(game);
            FailIf(native.Table((game == OracleGame.Ages ? 0x6d0a : 0x6a90) + mode * 32 + collision) != 9,
                $"{game}: clean-US Keese thrown-object row must select effect $09 / ENEMYDMG_04.");
            app.Step(1, Vector2.Down, held: ["attack"], pressed: ["attack"], batched: batched);
            for (int update = 0; target.Health == 9 && update < 40; update++) app.Step(1, Vector2.Zero, batched: batched);
            FailIf(target.Health != 6 || !target.NativeHitPending || target.InvincibilityCounter != 0x15 || target.KnockbackCounter != 0x0b,
                $"{game}: the real thrown Bracelet child must publish native effect $09 damage/recoil in the post-object scan.");
            int rest = target.Counter1;
            app.Step(1, Vector2.Zero, batched: batched);
            FailIf(target.NativeHitPending || target.InvincibilityCounter != 0x14 || target.KnockbackCounter != 0x0b || target.Counter1 != rest,
                $"{game}: the next native Keese visit must consume the thrown-item hit before AI or recoil movement.");
            player.WarpTo(new(88, 82));
            entities.Clear();
        }
    }
}
