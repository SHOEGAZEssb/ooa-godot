using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateUnderwaterWalkGraphics()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int shield in new[] { 0, 1, 2 })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(2, 0x90);
            _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Flippers, 0);
            _inventory.GiveTreasure(TreasureId.MermaidSuit, 0);
            if (shield != 0) _inventory.GiveTreasure(TreasureId.Shield, shield - 1);
            _inventory.SetScriptedEquippedItems(shield == 0 ? TreasureId.None : TreasureId.Shield, TreasureId.None);
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0xa0,
                    x is 0 or 9 || y is 0 or 7 ? (byte)0x0f : (byte)0, 0);

            foreach (Vector2I direction in new[] { Vector2I.Up, Vector2I.Right, Vector2I.Down, Vector2I.Left })
            {
                _player.WarpTo(new(80, 64));
                _player.Face(direction);
                string context = $"Underwater WALK room 2:90 shield={shield}, facing={direction}, batched={batched}";
                StepGameplayUpdates(6, Vector2.Zero, batched: batched);
                ValidateUnderwaterWalkFrame(0x7c + CarriedObjectMotion.DirectionIndex(direction), context + " idle");
                bool first = false, second = false;
                StepGameplayUpdates(24, direction, batched: batched, afterUpdate: () =>
                {
                    int frame = (int)(_player.CurrentWalkBodyFrame.Region.Position.X / 16);
                    first |= frame == 0;
                    second |= frame == 1;
                    ValidateUnderwaterWalkFrame((frame == 0 ? 0x7c : 0xa8) +
                        CarriedObjectMotion.DirectionIndex(_player.FacingVector), context + " moving");
                });
                FailIf(!first || !second || _player.TopDownSwimming || _player.IsUsingShield,
                    context + ": underwater WALK must use both frames independently of surface swimming or shield parents.");
                StepGameplayUpdates(60, Vector2.Zero, batched: batched);
                FailIf(!_player.ApplyEnemyContactDamage(_player.Position - (Vector2)direction * 16, 2),
                    context + ": damage palette fixture failed to accept contact.");
                bool damage = false, normal = false;
                StepGameplayUpdates(8, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    damage |= _player.DamagePaletteActive;
                    normal |= !_player.DamagePaletteActive;
                    ValidateUnderwaterWalkFrame(0x7c + CarriedObjectMotion.DirectionIndex(_player.FacingVector), context + " recoil");
                });
                FailIf(!damage || !normal, context + ": damage palette did not alternate with the native frame-counter bit.");
                StepGameplayUpdates(60, Vector2.Zero, batched: batched);
            }
        }
        GD.Print("Validated underwater WALK idle/moving/recoil pixels, both frames, four directions, normal/damage palettes and shield priority in split/batched gameplay.");
    }

    private void ValidateUnderwaterWalkFrame(int graphic, string context)
    {
        // Ages specialObjectAnimationData.s graphics $7c-$7f/$a8-$ab,
        // selected by func_4553 WALK + var34=$28 + direction. These are
        // source expectations, independent of the imported Mermaid rows.
        int index = graphic >= 0xa8 ? graphic - 0xa8 + 4 : graphic - 0x7c;
        int[] offsets = { 0x1640, 0x1680, 0x1600, 0x1680, 0x1640, 0x16c0, 0x1600, 0x16c0 };
        bool[] mirrors = { false, true, false, false, true, true, true, false };
        FailIf(index is < 0 or > 7, context + $": unexpected underwater WALK graphic ${graphic:x2}.");
        var body = _player.CurrentWalkBodyFrame;
        FailIf(body.Region != new Rect2((index / 4) * 16, (index % 4) * 16, 16, 16),
            context + $": selected body region {body.Region} differs from graphic ${graphic:x2}.");
        using Image atlas = body.Texture.GetImage();
        using Image actual = atlas.GetRegion(new Rect2I((Vector2I)body.Region.Position, new(16, 16)));
        using Image expected = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);
        Image source = OracleGraphicsCache.LoadImage("res://assets/oracle/gfx/spr_link.png");
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16; x++)
        {
            int spriteX = mirrors[index] ? 15 - x : x;
            int cell = offsets[index] / 32 + spriteX / 8;
            float value = source.GetPixel((cell % 16) * 8 + spriteX % 8, (cell / 16) * 16 + y).R;
            Color color = value < 0.1f ? Colors.Transparent : _player.DamagePaletteActive
                ? value < 0.5f ? new(31 / 31f, 22 / 31f, 6 / 31f)
                    : value < 0.9f ? new(27 / 31f, 0, 0) : Colors.Black
                : value < 0.5f ? Colors.Black
                    : value < 0.9f ? new(2 / 31f, 21 / 31f, 8 / 31f) : new(31 / 31f, 26 / 31f, 17 / 31f);
            expected.SetPixel(x, y, color);
        }
        FailIf(OracleGraphicsCache.PixelHash(actual) != OracleGraphicsCache.PixelHash(expected),
            context + $": selected WALK graphic ${graphic:x2} pixels differ (damage={_player.DamagePaletteActive}).");
    }
}
