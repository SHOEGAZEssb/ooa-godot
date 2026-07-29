using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRooms171And181()
    {
        const double frame = 1.0 / 60.0;
        var root = new Node { Name = "Rooms171And181Validation" };
        var worldRoot = new Node { Name = "World" };
        var interfaceLayer = new CanvasLayer { Name = "Interface" };
        var roomView = new RoomView { Name = "RoomView" };
        var dialogue = new DialogueBox { Name = "Dialogue" };
        var fade = new ColorRect { Name = "Fade" };
        var hud = new Hud { Name = "Hud" };
        var camera = new Camera2D { Name = "Camera" };
        root.AddChild(worldRoot);
        root.AddChild(interfaceLayer);
        root.AddChild(roomView);
        root.AddChild(dialogue);
        root.AddChild(fade);
        root.AddChild(hud);
        root.AddChild(camera);
        AddChild(root);

        OracleSaveData save = OracleSaveData.CreateStandardGame();
        long tick = 0;
        var rooms = new RoomSession(
            1, 0x81, () => tick, () => tick = 0, save);
        var treasures = new TreasureDatabase();
        var inventory = new InventoryState(
            treasures, save, () => rooms.CurrentDungeonIndex);
        using var fixture = RoomEntityValidationFixture.ForRoot(
            worldRoot, new()
            {
                SaveData = save,
                Inventory = inventory,
                Treasures = treasures,
                Rooms = rooms,
                AnimationTick = () => tick
            });
        RoomEntityManager manager = fixture.Manager;
        var context = new RoomEventContext(
            rooms,
            manager,
            _transitions,
            dialogue,
            _player,
            roomView,
            static position => position,
            () => tick,
            interfaceLayer,
            fade,
            hud,
            inventory,
            treasures,
            _sound,
            camera);
        var scrubEvent = new BusinessScrubEvent(context);
        var interactions = new InteractionController(
            rooms,
            manager,
            new SignDatabase(),
            new ChestDatabase(),
            treasures,
            dialogue,
            worldRoot,
            roomView,
            static position => position,
            () => tick,
            inventory,
            interfaceLayer,
            roomInteractionHandlers:
            [
                NpcInteractionHandler.ForNpc(
                    "businessScrub.s:interactionCodece",
                    (target, _) => scrubEvent.TryInteractNpc(target.Npc))
            ]);

        static string PlainWords(string message) => string.Join(
            " ",
            DialogueBox.PlainText(message).Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));

        var enemyData = new EnemyDatabase();
        IReadOnlyList<RoomObjectRecord> room171 =
            enemyData.GetRoomObjects(1, 0x71);
        FailIf(
            room171.Count != 4 ||
            room171[0] is not
                {
                    Order: 0,
                    Kind: RoomObjectKind.ItemDrop,
                    Id: 0x57,
                    SubId: 0x05,
                    PackedPosition: 0x11
                } ||
            room171[1] is not
                {
                    Order: 1,
                    Kind: RoomObjectKind.ItemDrop,
                    Id: 0x57,
                    SubId: 0x01,
                    PackedPosition: 0x12
                } ||
            room171[2] is not
                {
                    Order: 2,
                    Kind: RoomObjectKind.ItemDrop,
                    Id: 0x57,
                    SubId: 0x05,
                    PackedPosition: 0x13
                } ||
            room171[3] is not
                {
                    Order: 3,
                    Kind: RoomObjectKind.RandomEnemy,
                    Id: 0x0c,
                    SubId: 0x00,
                    Flags: 0x40,
                    Count: 2
                },
            "Room 1:71 did not retain three ordered item-drop producers " +
            "followed by two random Arrow Moblins.");

        manager.LoadRoom(1, _world.LoadRoom(1, 0x71));
        List<ItemDropProducer> producers =
            manager.Entities<ItemDropProducer>();
        List<ArrowMoblinCharacter> moblins =
            manager.Entities<ArrowMoblinCharacter>();
        Vector2[] expectedProducerPositions =
        {
            new(0x18, 0x18),
            new(0x28, 0x18),
            new(0x38, 0x18)
        };
        FailIf(
            producers.Count != 3 ||
            producers.Where((producer, index) =>
                producer.Position != expectedProducerPositions[index]).Any() ||
            producers.Any(producer => producer.Initialized) ||
            moblins.Count != 2 ||
            moblins[0].Position == moblins[1].Position ||
            manager.Entities<NpcCharacter>().Count != 0 ||
            manager.RandomCalls != 256,
            "Room 1:71 did not instantiate only its three invisible drop " +
            "producers and two distinct Arrow Moblins with one placement buffer.");
        manager.Update(frame, _player);
        FailIf(
            producers.Any(producer => !producer.Initialized) ||
            producers.Any(producer => producer.Visible),
            "Room 1:71 item-drop producers did not capture their source " +
            "metatiles invisibly on their first update.");
        int randomCallsBeforeRoom181 = manager.RandomCalls;

        OracleRoomData room181 = rooms.CurrentRoom;
        var database = new BusinessScrubDatabase();
        BusinessScrubOffer[] offers =
        {
            database.OfferForShieldLevel(0),
            database.OfferForShieldLevel(1),
            database.OfferForShieldLevel(2),
            database.OfferForShieldLevel(3)
        };
        FailIf(
            offers.Select(offer => offer.Price).ToArray() is not
                [30, 30, 50, 80] ||
            offers.Select(offer => offer.Parameter).ToArray() is not
                [1, 1, 2, 3],
            "Business Scrub shield-level pricing no longer matches effective " +
            "subids $03/$03/$04/$05.");

        Vector2 scrubPosition = new(0x18, 0x38);
        byte originalTile = room181.GetMetatile(scrubPosition);
        ulong underlyingTileHash = PixelHash(
            room181.BuildMimickedMetatileTexture(scrubPosition));
        Texture2D expectedBush =
            room181.BuildMimickedMetatileTexture((byte)database.BushTile);
        ulong expectedBushHash = PixelHash(expectedBush);
        manager.LoadRoom(1, room181);
        NpcCharacter scrub = manager.Entities<NpcCharacter>().Single();
        Sprite2D bush = scrub.GetNode<Sprite2D>("BusinessScrubBush");
        FailIf(
            scrub.BaseRecord is not
            {
                Group: 1,
                Room: 0x81,
                Id: 0xce,
                SubId: 0x03,
                Var03: 0x00,
                TextId: 0x0000,
                SpriteName: "spr_hostilescrub",
                TileBase: 0,
                Palette: 5,
                DefaultAnimation: 0,
                CanFace: false,
                Implementation:
                    NpcImplementationClassification.SpecializedNative
            } ||
            scrub.Position != scrubPosition ||
            scrub.CurrentScriptAnimationSource != database.Animation(0) ||
            scrub.CurrentAnimationOpaquePixels != 0 ||
            room181.GetMetatile(scrubPosition) != database.FloorTile ||
            room181.GetTerrainInfo(scrubPosition).Collision !=
                database.FloorCollision ||
            !room181.IsSolid(scrubPosition) ||
            PixelHash(
                room181.BuildMimickedMetatileTexture(scrubPosition)) !=
                underlyingTileHash ||
            bush.Texture is null ||
            bush.ShowBehindParent ||
            PixelHash(bush.Texture) != expectedBushHash ||
            underlyingTileHash == expectedBushHash ||
            originalTile == database.FloorTile ||
            manager.RandomCalls != randomCallsBeforeRoom181 + 256,
            "Room 1:81 did not replace the scrub's source bush with solid " +
            "tile $00/$0f while preserving it as the $ce:$80 mimic " +
            $"(record={scrub.BaseRecord}, position={scrub.Position}, " +
            $"animation='{scrub.CurrentScriptAnimationSource}', " +
            $"pixels={scrub.CurrentAnimationOpaquePixels}, " +
            $"originalTile=${originalTile:x2}, " +
            $"tile=${room181.GetMetatile(scrubPosition):x2}, " +
            $"collision=${room181.GetTerrainInfo(scrubPosition).Collision:x2}, " +
            $"solid={room181.IsSolid(scrubPosition)}, " +
            $"bush={PixelHash(bush.Texture!)}/{expectedBushHash}, " +
            $"showBehindParent={bush.ShowBehindParent}, " +
            $"random={manager.RandomCalls}).");

        _player.WarpTo(
            scrubPosition + Vector2.Right * database.ProximityRadius,
            recordSafe: false);
        manager.Update(frame, _player);
        FailIf(
            scrub.CurrentScriptAnimationSource != database.Animation(0) ||
            manager.FindTalkTarget(_player) is not null,
            "Business Scrub emerged at the excluded Manhattan-distance $20 boundary.");

        _player.WarpTo(
            scrubPosition + Vector2.Right *
                (database.ProximityRadius - 1),
            recordSafe: false);
        manager.Update(frame, _player);
        FailIf(
            scrub.CurrentScriptAnimationSource != database.Animation(1) ||
            scrub.CurrentAnimationOpaquePixels != 149 ||
            database.SourceGrayscaleInverted ||
            scrub.CurrentAnimationPixelHash != 0x7f12d6b653255044UL ||
            bush.Position.Y != database.BushOffsetForParameter(1),
            "Business Scrub did not emerge with spr_hostilescrub's imported " +
            "white-background interpretation behind its mimicked bush, or did " +
            "not lift that bush eight pixels inside the strict distance-$20 " +
            "boundary " +
            $"(hash=${scrub.CurrentAnimationPixelHash:x16}, " +
            $"pixels={scrub.CurrentAnimationOpaquePixels}, " +
            $"inverted={database.SourceGrayscaleInverted}, " +
            $"showBehindParent={bush.ShowBehindParent}).");

        _player.WarpTo(
            scrubPosition + Vector2.Down * 12.0f,
            recordSafe: false);
        _player.Face(Vector2I.Up);
        manager.Update(frame, _player);
        inventory.AddRupees(-inventory.Rupees);
        FailIf(
            manager.FindTalkTarget(_player) != scrub ||
            !interactions.TryInteract(_player) ||
            !dialogue.ChoiceActive ||
            !scrubEvent.BlocksGameplay ||
            scrubEvent.Stage != BusinessScrubEventStage.PurchasePrompt ||
            scrub.CurrentScriptAnimationSource != database.Animation(2) ||
            scrub.CurrentAnimationOpaquePixels != 197 ||
            scrub.CurrentAnimationPixelHash != 0xd7a80010ec23ec10UL ||
            !PlainWords(dialogue.CurrentMessage).Contains(
                "A Shield for 30 Rupees?", StringComparison.Ordinal) ||
            dialogue.CurrentMessage.Contains("\\num1", StringComparison.Ordinal),
            "Room 1:81's emerged scrub did not open TX_4509 with the " +
            "30-Rupee level-1 shield offer using its exact talking pose " +
            $"(hash=${scrub.CurrentAnimationPixelHash:x16}, " +
            $"pixels={scrub.CurrentAnimationOpaquePixels}).");
        manager.Update(frame, _player);
        FailIf(
            bush.Position.Y != database.BushOffsetForParameter(2),
            "Business Scrub talk animation did not lift the mimicked bush " +
            "eleven pixels while gameplay text was active.");

        dialogue.SubmitChoiceForValidation(1);
        scrubEvent.UpdateFrame();
        FailIf(
            PlainWords(dialogue.CurrentMessage) != "Then be gone!" ||
            scrubEvent.Stage != BusinessScrubEventStage.ResultText,
            "Declining the Business Scrub did not show TX_4506.");
        dialogue.Close();
        scrubEvent.UpdateFrame();
        FailIf(
            scrubEvent.HasState ||
            scrub.CurrentScriptAnimationSource != database.Animation(4),
            "Closing the decline response did not restore Business Scrub animation $04.");

        FailIf(
            !interactions.TryInteract(_player),
            "Business Scrub could not restart its offer after a decline.");
        dialogue.SubmitChoiceForValidation(0);
        scrubEvent.UpdateFrame();
        FailIf(
            PlainWords(dialogue.CurrentMessage) !=
                "You don't have enough Rupees!" ||
            inventory.HasTreasure(TreasureDatabase.TreasureShield),
            "A zero-Rupee Business Scrub purchase did not show TX_4507 " +
            "without granting a shield.");
        dialogue.Close();
        scrubEvent.UpdateFrame();

        inventory.AddRupees(30);
        int getSeedSounds =
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetSeed);
        FailIf(
            !interactions.TryInteract(_player),
            "Business Scrub could not restart its offer for a valid purchase.");
        dialogue.SubmitChoiceForValidation(0);
        scrubEvent.UpdateFrame();
        FailIf(
            PlainWords(dialogue.CurrentMessage) != "Thank you!" ||
            !inventory.HasTreasure(TreasureDatabase.TreasureShield) ||
            inventory.ShieldLevel != 1 ||
            inventory.Rupees != 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetSeed) !=
                getSeedSounds + 1,
            "The valid 30-Rupee Business Scrub purchase did not grant " +
            "shield level 1, deduct rupees, play SND_GETSEED, and show TX_4505.");
        dialogue.Close();
        scrubEvent.UpdateFrame();

        inventory.AddRupees(30);
        FailIf(
            !interactions.TryInteract(_player),
            "Business Scrub could not restart its offer with a shield owned.");
        dialogue.SubmitChoiceForValidation(0);
        scrubEvent.UpdateFrame();
        FailIf(
            PlainWords(dialogue.CurrentMessage) !=
                "You already have it!" ||
            inventory.Rupees != 30 ||
            inventory.ShieldLevel != 1,
            "Business Scrub did not show TX_4508 without charging for an " +
            "already-owned shield.");
        dialogue.Close();
        scrubEvent.UpdateFrame();

        _player.WarpTo(
            scrubPosition + Vector2.Right * database.ProximityRadius,
            recordSafe: false);
        manager.Update(frame, _player);
        FailIf(
            scrub.CurrentScriptAnimationSource != database.Animation(3) ||
            bush.Position.Y != database.BushOffsetForParameter(1) ||
            manager.FindTalkTarget(_player) is not null,
            "Leaving the Business Scrub's proximity did not begin animation " +
            "$03 and remove it from the A-button target list.");

        float outgoingBushY = bush.Position.Y;
        OracleRoomData transitionRoom181 = _world.LoadRoom(1, 0x81);
        Vector2 incomingOffset = Vector2.Right * transitionRoom181.Width;
        manager.BeginScreenTransition(1, transitionRoom181, incomingOffset);
        NpcCharacter incomingScrub =
            manager.Entities<NpcCharacter>().Single();
        Sprite2D incomingBush =
            incomingScrub.GetNode<Sprite2D>("BusinessScrubBush");
        NpcCharacter outgoingScrub =
            manager.OutgoingEntities<NpcCharacter>().Single();
        Sprite2D outgoingBush =
            outgoingScrub.GetNode<Sprite2D>("BusinessScrubBush");
        FailIf(
            incomingScrub.TransitionDrawOffset != incomingOffset ||
            incomingBush.Position != incomingOffset ||
            outgoingScrub.TransitionDrawOffset != Vector2.Zero ||
            outgoingBush.Position != new Vector2(0, outgoingBushY),
            "Business Scrub bush did not remain aligned with its incoming " +
            "and outgoing room presentation at transition preload.");

        Vector2 outgoingOffset = Vector2.Left * 80.0f;
        incomingOffset = Vector2.Right * 80.0f;
        manager.SetScreenTransitionOffsets(outgoingOffset, incomingOffset);
        FailIf(
            incomingScrub.TransitionDrawOffset != incomingOffset ||
            incomingBush.Position != incomingOffset ||
            outgoingScrub.TransitionDrawOffset != outgoingOffset ||
            outgoingBush.Position !=
                outgoingOffset + new Vector2(0, outgoingBushY),
            "Business Scrub bush drifted relative to the tilemap during a " +
            "screen-transition offset update.");
        manager.FinishScreenTransition();
        FailIf(
            incomingScrub.TransitionDrawOffset != Vector2.Zero ||
            incomingBush.Position != Vector2.Zero,
            "Business Scrub bush retained a transition offset after scrolling.");

        manager.Clear();
        RemoveChild(root);
        root.QueueFree();
        GD.Print(
            "Validated rooms 1:71 and 1:81: ordered drop producers/Arrow " +
            "Moblins, Business Scrub fixed-$c5 mimic, preserved ground " +
            "rendering with logical $00/$0f solidity, transition-aligned " +
            "bush, bush-over-scrub OAM priority, white-background source " +
            "interpretation, strict $20 emergence, $06 A-button radius, " +
            "animations $00-$04, shield pricing, decline/insufficient/" +
            "already-owned/" +
            "success text, rupees, treasure grant, and SND_GETSEED.");

        static ulong PixelHash(Texture2D texture)
        {
            Image image = texture.GetImage();
            byte[] bytes = image.GetData();
            ulong hash = 1469598103934665603UL;
            foreach (byte value in bytes)
            {
                hash ^= value;
                hash *= 1099511628211UL;
            }
            return hash;
        }
    }
}
