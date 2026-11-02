
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void ValidateSharedDiggingEnemyData()
    {
        foreach (var game in new[] { OracleGame.Ages, OracleGame.Seasons })
        {
            bool ages = game == OracleGame.Ages;
            var rom = ValidationRom.LoadCleanUs(game);
            int Byte(int address) => rom.Span[address];
            int Word(int address) => Byte(address) | Byte(address + 1) << 8;
            int Bank(int bank, int pointer) => bank * 0x4000 + (pointer & 0x3fff);
            int data = ages ? 0xfdd4b : 0xfdd71, extra = ages ? 0xfdfb9 : 0xfdff3;
            string root = OracleAssetPack.ForGame(game);
            var definitions = EnemyDefinitionDatabase.For(root);
            foreach (int id in new[] { 0x10, 0x51, 0x2d })
            {
                int property = extra + Byte(data + id * 4 + 2) * 4, flags = Byte(data + id * 4 + 3);
                int animations = Bank(ages ? 0x0d : 0x0c, id switch
                {
                    0x10 => ages ? 0x7018 : 0x70b7,
                    0x51 => ages ? 0x6fec : 0x707f,
                    _ => ages ? 0x7086 : 0x7119
                });
                int oams = Bank(ages ? 0x0d : 0x0c, id switch
                {
                    0x10 => ages ? 0x7b77 : 0x7b32,
                    0x51 => ages ? 0x7b71 : 0x7b3a,
                    _ => ages ? 0x7bf7 : 0x7bb2
                });
                var expectedAnimations = new List<string>();
                for (int animation = 0; animation < (id == 0x51 ? 1 : 2); animation++)
                {
                    int start = Bank(ages ? 0x0d : 0x0c, Word(animations + animation * 2));
                    var frames = new List<string>(); var starts = new List<int>();
                    for (int offset = 0, visit = 0; visit < 64; offset += 3, visit++)
                    {
                        int duration = Byte(start + offset);
                        if (duration == 0xff)
                        {
                            int loop = starts.IndexOf(offset + 1 + Byte(start + offset + 1) - 256);
                            FailIf(loop < 0, $"{game}: native ENEMY${id:x2} animation loop must land on a frame.");
                            expectedAnimations.Add(string.Join('|', frames) + (loop == 0 ? "" : $"~{loop}"));
                            break;
                        }
                        int oam = Bank(ages ? 0x13 : 0x12, Word(oams + Byte(start + offset + 1)));
                        string cells = string.Join(';', Enumerable.Range(0, Byte(oam)).Select(cell =>
                            string.Join(',', Enumerable.Range(0, 4).Select(column => Byte(oam + 1 + cell * 4 + column)))));
                        int parameter = Byte(start + offset + 2);
                        starts.Add(offset); frames.Add((parameter == 0 ? $"{duration}" : $"{duration},{parameter}") + "@" + cells);
                        if (id == 0x2d && animation == 1 && parameter == 0xff)
                        {
                            expectedAnimations.Add(string.Join('|', frames)); break;
                        }
                    }
                }
                foreach (int subId in id == 0x10 ? new[] { 0, 1, 2, 3 } : id == 0x51 ? new[] { 2, 3 } : new[] { 0 })
                {
                    var record = definitions.Get(id, subId);
                    FailIf(record.AssetRoot != root || record.Id != id || record.SubId != subId ||
                        record.TileBase != (flags & 15) * 2 || record.Palette != (flags >> 4 & 7) ||
                        record.RadiusY != Byte(property) || record.RadiusX != Byte(property + 1) ||
                        record.RawDamage != Byte(property + 2) || record.DamageQuarters != (256 - Byte(property + 2)) / 2 ||
                        record.Health != Byte(property + 3) || !record.Animations.SequenceEqual(expectedAnimations),
                        $"{game}: ENEMY${id:x2}:${subId:x2} properties or complete native animation/OAM streams differ.");
                }
            }
            var digging = new DiggingEnemyDatabase(root);
            for (int roll = 0; roll < 256; roll++)
            foreach (int flags in new[] { 1, 0x81 })
            {
                int id = !ages && flags == 0x81 ? 0x2d : (roll & 7) < 3 ? 0x10 : 0x51;
                var source = digging.Enemy(roll, 3, flags);
                FailIf(source.Id != id || source.SubId != 3 || source.CollisionMode != Byte(data + id * 4 + 1),
                    $"{game}: native digging roll ${roll:x2}, flags ${flags:x2} must retain species, var03 and its own collision mode.");
            }
            var tower = PodobooTowerDatabase.For(root);
            int radii = ages ? 0x36662 : 0x32681;
            FailIf(!tower.State.Select(value => value.Value).SequenceEqual(new[] { 60, 150, 180, 180, 3 }) ||
                !tower.Radii.Select(value => value.Value).SequenceEqual(Enumerable.Range(0, 15).Select(index => Byte(radii + index))),
                $"{game}: native Podoboo Tower counter boundaries or emergence geometry differs.");
        }
    }
}
