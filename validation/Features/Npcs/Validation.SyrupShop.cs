using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSyrupShopGraphics()
    {
        // interactionOamData519d4/519e5/519f6 and interactionAnimation5a2a9:
        // four 15-update frames, with head cells at signed Y=-8, body at +8.
        string[] sourceOam = [
            "248,0,0,3;248,8,2,3;8,0,4,2;8,8,6,2",
            "248,0,8,3;248,8,10,3;8,0,12,2;8,8,14,2",
            "248,0,2,35;248,8,0,35;8,0,6,34;8,8,4,34",
            "248,0,8,3;248,8,10,3;8,0,12,2;8,8,14,2"
        ];
        Image source = OracleGraphicsCache.LoadImage("res://assets/oracle/gfx/spr_syrup_teenager.png");
        ulong[] hashes = sourceOam.Select(oam =>
        {
            var (texture, offset) = NpcCharacter.BuildPositionedOamTexture(source, oam, 0, 0,
                paletteOverride: null, sourceGrayscaleInverted: true);
            using Image frame = texture.GetImage();
            FailIf(offset != new Vector2(-8,-24) || frame.GetSize() != new Vector2I(16,32),
                "Syrup's four OAM cells must span 16x32 at offset (-8,-24).");
            int headPixels = 0;
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 16; x++)
                if (frame.GetPixel(x,y).A > 0.1f) headPixels++;
            FailIf(headPixels == 0, "Syrup source head rows unexpectedly contain no opaque pixels.");
            ulong hash = 14695981039346656037UL;
            foreach (byte value in frame.GetData()) { hash ^= value; hash *= 1099511628211UL; }
            return hash;
        }).ToArray();
        foreach (bool batched in new[] { false, true })
        {
            LoadValidationRoom(3,0xed);
            _player.WarpTo(new Vector2(80,96));
            NpcCharacter syrup = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x5f);
            for (int frame = 0; frame <= 4; frame++)
            {
                int index = frame % 4;
                void Check() => FailIf(syrup.CurrentAnimationTextureSize != new Vector2I(16,32) ||
                    syrup.CurrentAnimationOffset != new Vector2(-8,-24) ||
                    syrup.CurrentAnimationPixelHash != hashes[index] || syrup.Position != new Vector2(32,40),
                    $"3:ed Syrup frame {index} clipped or shifted source OAM head/body pixels.");
                Check();
                if (frame == 4) break;
                StepGameplayUpdates(14,Vector2.Zero,batched:batched);
                Check();
                StepGameplayUpdates(1,Vector2.Zero,batched:batched);
            }
        }
    }

    private void ValidateSyrupShopInteractions()
    {
        var data = new LynnaShopDatabase(syrup: true);
        var save = OracleSaveData.CreateStandardGame();
        SetTreasure(save, TreasureId.Bombchus, false);
        save.WriteWramByte(0xc642, 0);
        var stock = data.ResolveStock(save);
        FailIf(!stock.Select(s => s.Item.SubId).SequenceEqual(new[] { 7, 8 }) ||
            !stock.Select(s => s.X).SequenceEqual(new[] { 0x4c, 0x74 }),
            "3:ed without Bombchus must delete $47:$0b and retain $07/$08 at X=$4c/$74.");
        SetTreasure(save, TreasureId.Bombchus, true);
        stock = data.ResolveStock(save);
        FailIf(!stock.Select(s => s.Item.SubId).SequenceEqual(new[] { 0x0b, 9, 0x0a }) ||
            !stock.Select(s => s.X).SequenceEqual(new[] { 0x44, 0x64, 0x84 }) ||
            !stock.Select(s => s.Item.Price).SequenceEqual(new[] { 100, 300, 300 }),
            "3:ed Bombchu stock lost source order, replacement offsets or prices.");
        save.WriteWramByte(0xc642, 0x40);
        FailIf(data.ResolveStock(save).Any(s => s.Item.SubId == 0x0a),
            "3:ed wBoughtShopItems1 bit6 did not suppress $47:$0a.");
        FailIf(data.Item(7).TreasureId != 0x2f || data.Item(0x0b).Parameter != 5 ||
            data.Item(8).TreasureId != 0x34 || data.Item(7).ItemTextId != 0x006d ||
            data.Item(0x0b).ItemTextId != 0x0032 || data.Text(0x0d01).Contains("\\cmd8", StringComparison.Ordinal),
            "Syrup source treasures/text continuations were not imported correctly.");

        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            ResetValidationInput();
            SetTreasure(_saveData, TreasureId.Bombchus, false);
            SetTreasure(_saveData, TreasureId.Potion, false);
            _saveData.WriteWramByte(0xc642, 0);
            _saveData.WriteWramByte(0xc643, 0);
            LoadValidationRoom(3, 0xed);
            SyrupShopEvent shop = _roomEvents.Get<SyrupShopEvent>();
            NpcCharacter syrup = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x5f);
            NpcCharacter cucco = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0xc9);
            LynnaShopItem potion = _entities.Entities<LynnaShopItem>().Single(i => i.Record.SubId == 7);
            FailIf(syrup.Position != new Vector2(32,40) || cucco.Position != new Vector2(120,104),
                "3:ed lost $5f:$80/$c9:$80 native placements.");
            void Step(int n) => StepGameplayUpdates(n, Vector2.Zero, batched: batched);
            void Walk(Vector2 target)
            {
                for (int axis = 0; axis < 2; axis++)
                {
                    int limit = 180;
                    while (Mathf.Abs(axis == 0 ? _player.Position.X-target.X : _player.Position.Y-target.Y) > 1)
                    {
                        if (--limit == 0) throw new InvalidOperationException($"Syrup room collision blocked {_player.Position} -> {target}, stage {shop.Stage}.");
                        Vector2 dir = axis == 0 ? new(Mathf.Sign(target.X-_player.Position.X),0)
                            : new(0,Mathf.Sign(target.Y-_player.Position.Y));
                        string action = dir == Vector2.Left ? "move_left" : dir == Vector2.Right ? "move_right" : dir == Vector2.Up ? "move_up" : "move_down";
                        StepGameplayUpdates(1,dir,[action],[action]);
                    }
                }
                Step(1);
                FailIf(_rooms.CurrentRoom.IsSolid(_player.Position), $"3:ed approach entered solid geometry at {_player.Position}.");
            }
            void PressA() { StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]); Step(1); }
            void Text(int id) => FailIf(!_dialogue.IsOpen || _dialogue.CurrentMessage != DialogueBox.PlainText(data.Text(id)),
                $"3:ed expected TX_{id:x4}, stage {shop.Stage}, got {_dialogue.CurrentMessage}.");
            void Close() { _dialogue.Close(); Step(2); }
            void Merchant() { Walk(new Vector2(76,72)); Walk(new Vector2(32,66)); _player.Face(Vector2I.Up); PressA(); }
            void Shelf(int x) { Walk(new Vector2(76,72)); Walk(new Vector2(x,58)); _player.Face(Vector2I.Up); PressA(); }

            _player.WarpTo(new Vector2(80,96));
            // Independent 8.8 trace: speedZ=-$c0, gravity=$20, speedX=-$80.
            int z = 0, speedZ = -192;
            for (int i = 1; i <= 12; i++)
            {
                z += speedZ;
                if (z >= 0) { z = 0; speedZ = -192; } else speedZ += 32;
                Step(1);
                FailIf(cucco.Position.X != 120-i*0.5f || shop.CuccoZ != z || shop.CuccoSpeedZ != speedZ,
                    $"$c9:$80 patrol/hop mismatch on update {i}.");
            }
            Merchant(); Text(0x0d00);
            Vector2 frozen = cucco.Position;
            Step(20);
            FailIf(cucco.Position != frozen, "$c9:$80 must freeze while text is active.");
            Close(); Merchant(); Text(0x0d00); Close();
            Shelf(76);
            FailIf(!potion.Held || !_player.IsCarryingObject, "$47:$07 cannot be picked up through room 3:ed's shelf geometry.");
            Merchant(); Text(0x0d01);
            _dialogue.SubmitChoiceForValidation(1); Step(2); Text(0x0d03); Close();
            FailIf(potion.Held || _player.IsCarryingObject, "Syrup declined purchase did not return $47:$07.");

            _inventory.AddRupees(-_inventory.Rupees);
            Shelf(76); Merchant(); _dialogue.SubmitChoiceForValidation(0); Step(2); Text(0x0d08); Close();
            FailIf(potion.Held || _inventory.HasTreasure(TreasureId.Potion), "Syrup insufficient-funds response granted an item.");
            _inventory.AddRupees(999);
            Shelf(76); Merchant(); _dialogue.SubmitChoiceForValidation(0); Step(2); Text(0x0d02);
            FailIf(_inventory.Rupees != 999, "Syrup charged before the purchase continuation finished.");
            Close(); Text(0x006d);
            FailIf(_inventory.Rupees != 699 || !_inventory.HasTreasure(TreasureId.Potion),
                "Syrup $47:$07 purchase must cost 300 and give TREASURE_POTION.");
            Close();
            FailIf(!potion.Removed || _player.IsCarryingObject || shop.BlocksGameplay,
                "Syrup purchase completion retained stock/input control.");
            Shelf(116); Merchant(); _dialogue.SubmitChoiceForValidation(0); Step(2);
            Text(0x0d06); Close(); Text(0x004b); Close();
            Merchant(); Text(0x0d0b); Close();
            Merchant(); Text(0x0d0b); Close();
            _inventory.AddRupees(300);

            // Re-entry restocks potions; possession gates the purchase, not visibility.
            LoadValidationRoom(3,0xed);
            _player.WarpTo(new Vector2(80,96));
            potion = _entities.Entities<LynnaShopItem>().Single(i => i.Record.SubId == 7);
            Shelf(76); Merchant(); _dialogue.SubmitChoiceForValidation(0); Step(2); Text(0x0d04); Close();
            FailIf(_inventory.Rupees != 699 || potion.Held, "Duplicate potion purchase ignored the native capacity check.");
            _inventory.AddRupees(-_inventory.Rupees);
            Shelf(76); Merchant(); _dialogue.SubmitChoiceForValidation(0); Step(2); Text(0x0d08); Close();
            FailIf(_inventory.Rupees != 0, "Syrup must test funds before the already-owned potion gate.");
            _inventory.AddRupees(699);

            Shelf(116);
            LynnaShopItem seed = _entities.Entities<LynnaShopItem>().Single(i => i.Record.SubId == 8);
            FailIf(!seed.Held, "Syrup's gasha shelf was unreachable.");
            Walk(new Vector2(80,104));
            FailIf(shop.CuccoState != 1 || _player.CutsceneControlled,
                "$c9:$80 intercepted before Link crossed Y=$69.");
            for (int i = 0; i < 4 && shop.CuccoState == 1; i++)
                StepGameplayUpdates(1, Vector2.Down,["move_down"],["move_down"]);
            FailIf(shop.CuccoState != 2 || _player.Position.Y != 105 || shop.CuccoZ != 0,
                "$c9:$80 crossing Y=$69 must clamp Link and initialize state2 with Z=0.");
            cucco = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0xc9);
            float chaseStart = cucco.Position.X;
            int chaseUpdates = 1;
            while ((((int)(chaseStart-2*chaseUpdates)-12)&0xff) >= (int)_player.Position.X) chaseUpdates++;
            Step(chaseUpdates-1);
            FailIf(shop.CuccoState != 2 || _dialogue.IsOpen, "$c9:$80 stopped its SPEED_200 chase one update early.");
            Step(1);
            FailIf(shop.CuccoState != 4 || _dialogue.IsOpen || cucco.Position.X != chaseStart-2*chaseUpdates,
                "$c9:$80 state2 must install the theft script on the strict X-$0c < Link.X boundary.");
            Step(1);
            Text(0x0d09);
            FailIf(_player.Position.Y != 105 || !seed.Held || !_player.CutsceneControlled,
                "$c9:$80 theft must clamp Y=$69 and retain the carried item.");
            float returnStart = cucco.Position.X;
            _dialogue.Close(); Step(1);
            FailIf(shop.CuccoState != 3 || cucco.Position.X != returnStart || shop.CuccoZ != 0,
                "$c9:$80 script completion must initialize state3 without moving.");
            int returnUpdates = (int)Math.Ceiling((120-returnStart)/2);
            Step(returnUpdates-1);
            FailIf(shop.CuccoState != 3 || !_player.CutsceneControlled,
                "$c9:$80 released Link before reaching X=$78.");
            Step(1);
            FailIf(shop.CuccoState != 1 || _player.CutsceneControlled || !seed.Held,
                "$c9:$80 did not return to patrol and release Link after theft dialogue.");
            Walk(new Vector2(80,96)); Shelf(116);
            FailIf(seed.Held, "Stock could not be returned after cucco theft prevention.");
            Shelf(116); Merchant(); _dialogue.SubmitChoiceForValidation(0); Step(2); Text(0x0d06); Close(); Text(0x004b); Close();
            FailIf(!seed.Removed || _inventory.GashaSeeds == 0 || _inventory.Rupees != 399,
                "Syrup gasha purchase lost its item, cost, or completion.");
            // Native $47 replacement uses treasure ownership even at zero ammo.
            _inventory.GiveTreasure(TreasureId.Bombchus,0);
            LoadValidationRoom(3,0xed); _player.WarpTo(new Vector2(80,96));
            LynnaShopItem bombchus = _entities.Entities<LynnaShopItem>().Single(i => i.Record.SubId == 0x0b);
            Shelf(68); Merchant(); Text(0x0d0a);
            _dialogue.SubmitChoiceForValidation(0); Step(2); Text(0x0d0c); Close(); Text(0x0032); Close();
            FailIf(_inventory.Bombchus != 5 || _inventory.Rupees != 299 || !bombchus.Removed,
                "Syrup Bombchu purchase must give five for 100 rupees.");
            _inventory.GiveTreasure(TreasureId.Bombchus,0x99);
            _inventory.GiveTreasure(TreasureId.GashaSeed,0x99);
            _inventory.AddRupees(999-_inventory.Rupees);
            LoadValidationRoom(3,0xed); _player.WarpTo(new Vector2(80,96));
            Shelf(68); Merchant(); _dialogue.SubmitChoiceForValidation(0); Step(2); Text(0x0d07); Close();
            Shelf(132); Merchant(); _dialogue.SubmitChoiceForValidation(0); Step(2); Text(0x0d07); Close();
            FailIf(_inventory.Rupees != 999 || _inventory.Bombchus != 0x99 || _inventory.GashaSeeds != 0x99,
                "Syrup $99 capacity rejections changed inventory or rupees.");
            LoadValidationRoom(3,0xed); _player.WarpTo(new Vector2(80,96));
            Shelf(68);
            Walk(new Vector2(80,104));
            for (int i = 0; i < 4 && shop.CuccoState == 1; i++)
                StepGameplayUpdates(1,Vector2.Down,["move_down"],["move_down"]);
            FailIf(shop.CuccoState != 2 || !_player.CutsceneControlled,
                "Repeated Syrup theft did not acquire native control before cancellation.");
            shop.Cancel();
            FailIf(_player.IsCarryingObject || _player.CutsceneControlled || shop.HasState,
                "Cancelling Syrup's room retained carried-item or native control.");
        }
    }
}
