
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedWalkingAnimation(FrontendValidationRoot root, bool batched)
    {
        var rooms = root._gameplaySession!.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player;
        var memory = rooms.RuntimeState;
        var game = rooms.SaveData.Game;
        var inventory = player.Inventory;
        int originalGroup = rooms.ActiveGroup, originalRoom = rooms.CurrentRoom.Id;
        var app = new ApplicationValidationFixture(root);
        var transitions = root._gameplaySession.Transitions;
        int? force = null, shield = null, walls = null, keys = null;
        bool vine = false;
        int turning = 0, visits = 0;
        try
        {
            if (game == OracleGame.Ages) root.LoadDebugRoom(0, 0x45);
            else root._seasonsOpening!.DebugTravel(0, 0xb6);
            entities.Clear();
            inventory.SetScriptedEquippedItems(0, 0);
            var room = rooms.CurrentRoom;
            for (int y = 16; y < 112; y += 16)
            for (int x = 16; x < 144; x += 16)
                room.SetPositionTileAndCollision(new(x + 8, y + 8),
                    (byte)(game == OracleGame.Ages ? 0x3a : 0x04), (byte)(x == 96 ? 15 : 0), 0);
            player.WarpTo(new(72, 72)); player.Face(Vector2I.Right);
            app.Step(1, Vector2.Zero, batched: batched);
            // A real interaction-slot visit publishes after Link. Observe the
            // source reset before writing overrides, then consume at the tail.
            entities.AddEntity(new ItemPhaseValidationEntity(() =>
            {
                FailIf(memory.ReadField("wForceLinkPushAnimation") is not (0 or 0x80) ||
                    memory.ReadField("wLinkClimbingVine") != 0 ||
                    (memory.ReadField("wLinkTurningDisabled") & 0x80) != 0,
                    $"{game}: force/vine/turning signals survived their next special-object reset.");
                visits++;
                if (force is { } f) memory.SetField("wForceLinkPushAnimation", (byte)f);
                if (shield is { } s) memory.SetField("wUsingShield", (byte)s);
                if (walls is { } w) memory.SetWramByte(0xd033, (byte)w);
                if (keys is { } k) memory.SetField("wGameKeysPressed", (byte)k);
                memory.SetField("wLinkClimbingVine", vine ? (byte)1 : (byte)0);
                memory.SetField("wLinkTurningDisabled", (byte)turning);
            }));
            for (int repeat = 0; repeat < 2; repeat++)
            {
                app.Step(32, Vector2.Right, batched: batched);
                FailIf(room.IsSolid(player.Position) || player.Position != new Vector2(92, 72) || (memory.ReadWramByte(0xd033) & 3) != 3,
                    $"{game}: WALK repeat{repeat} must reach the wall through collision geometry: XY={player.PrecisePosition}, walls=${memory.ReadWramByte(0xd033):x2}, solid={player.World.Collides(player.Position)}, masks=${memory.ReadField("wDisabledObjects"):x2}, visits={visits}.");
                Require(0x10, 1, "held wall contact");
                transitions.BeginNativeWhiteFade(true);
                app.Step(3, Vector2.Right, batched: batched, afterUpdate: () => Require(0, 0xff, "normal palette gate publishes suppression"));
                transitions.CancelNativeWhiteFade(clearPresentation: true);
                force = 0x80;
                app.Step(3, Vector2.Right, batched: batched, afterUpdate: () => Require(0, 0xff, "bit7 suppression"));
                force = 2;
                app.Step(3, Vector2.Right, batched: batched, afterUpdate: () => Require(0x10, 1, "nonforcing byte permits wall test"));
                force = 1; walls = 0; keys = 0;
                app.Step(3, Vector2.Zero, batched: batched, afterUpdate: () => Require(0x10, 1, "late force without wall/input"));
                vine = true;
                app.Step(1, Vector2.Zero, batched: batched);
                Require(0x10, 1, "push publication precedes vine-facing normalization");
                FailIf(player.FacingVector != Vector2I.Up || !player.SideScrollClimbing,
                    $"{game}: the raw vine byte must select final up facing through the animation tail.");
                vine = false; turning = 0x80; player.Face(Vector2I.Right);
                app.Step(1, Vector2.Zero, batched: batched);
                Require(0, 0xff, "turning suppression takes precedence over force1");
                turning = 0; force = null; walls = null; keys = null;
                app.Step(1, Vector2.Zero, batched: batched);
                Require(0, 0xff, "next-pass override expiry");
            }
            // The shield parent publishes its level, and text retains that
            // byte even if inventory is edited while the parent cannot run.
            inventory.GiveTreasure(TreasureId.Shield, 1); inventory.SetScriptedEquippedItems(0, TreasureId.Shield);
            app.Step(1, Vector2.Zero, held: ["attack"], pressed: ["attack"], batched: batched);
            Require(0x1c, 0xff, "native shield parent level1");
            FailIf(memory.ReadField("wUsingShield") != 1 || !player.IsUsingShield,
                $"{game}: shield use must publish the native level byte.");
            root._scene.Dialogue.ShowGameplayMessage("Walking signal pause", 100);
            inventory.GiveTreasure(TreasureId.Shield, 2);
            app.Step(3, Vector2.Zero, batched: batched, afterUpdate: () => Require(0x1c, 0xff, "shield level retained during text"));
            root._scene.Dialogue.Close();
            app.Step(1, Vector2.Zero, held: ["attack"], batched: batched);
            Require(0x20, 0xff, "parent republishes level2 after text");
            inventory.SetScriptedEquippedItems(0, 0);
            app.Step(1, Vector2.Zero, batched: batched);
            Require(0, 0xff, "shield release");
            // Raw aliases must be consumed at this same late boundary; a
            // second selection during drawing would observe the later edit.
            shield = 1;
            app.Step(1, Vector2.Zero, batched: batched);
            Require(0x1c, 0xff, "late raw shield alias");
            memory.SetField("wUsingShield", 2);
            Require(0x1c, 0xff, "drawing retains the published style");
            shield = null;
            app.Step(1, Vector2.Zero, batched: batched);
            Require(0, 0xff, "normal parent clear after raw alias");
            FailIf(visits < 80, $"{game}: walking signal integration omitted actual repeated object visits.");
        }
        finally
        {
            root._scene.Dialogue.Close();
            transitions.CancelNativeWhiteFade(clearPresentation: true);
            memory.SetField("wLinkTurningDisabled", 0);
            memory.SetField("wUsingShield", 0);
            memory.SetField("wLinkClimbingVine", 0);
            if (game == OracleGame.Ages) root.LoadDebugRoom(originalGroup, originalRoom);
            else root._seasonsOpening!.DebugTravel(originalGroup, originalRoom);
            entities.Clear();
        }

        void Require(int style, int pushing, string boundary)
        {
            FailIf(memory.ReadWramByte(0xd034) != style || player.World.TilePushingDirection != pushing ||
                player.IsPushing != (style == 0x10 && pushing != 0xff),
                $"{game}, batch={batched}, {boundary}: var34=${memory.ReadWramByte(0xd034):x2}, push=${player.World.TilePushingDirection:x2}, expected ${style:x2}/${pushing:x2}.");
            var body = player.CurrentWalkBodyFrame;
            Texture2D expected = style == 0x10 ? player.Sprites.PushTexture : style == 0 ? player.Sprites.WalkTexture : player.Sprites.ShieldLinkTexture;
            int column = style is 0x1c or 0x20 ? (style - 0x14) / 4 : 0;
            FailIf(body.Texture != expected || (int)body.Region.Position.X / 32 != column ||
                (int)body.Region.Position.Y / 16 != CarriedObjectMotion.DirectionIndex(player.FacingVector),
                $"{game}: {boundary} did not consume the published native style in the actual body texture/region.");
        }
    }
}
