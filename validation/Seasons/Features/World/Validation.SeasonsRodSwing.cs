namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsRodSwingRom()
    {
        var rom = ValidationRom.LoadCleanUs(OracleGame.Seasons);
        // uncmpGfxHeader1c uploads spr_rod_of_seasons ($1a:$6f40) at OBJ tile $52.
        byte[] weaponTiles = rom.Slice(0x6af40, 0xa0).ToArray();
        int Word(int address) => rom.Span[address] | rom.Span[address + 1] << 8;
        foreach (bool batched in new[] { false, true })
        {
            var root = StartSeasonsAfterImpa(batched);
            try
            {
                var session = root._seasonsOpening!;
                var player = session.Link;
                var memory = session.Memory;
                var app = new ApplicationValidationFixture(root);
                session.Inventory.GiveTreasure(TreasureId.RodOfSeasons, 7);
                session.Inventory.EquipA(TreasureId.RodOfSeasons);
                foreach (bool stump in new[] { false, true })
                for (int direction = 0; direction < 4; direction++)
                {
                    session.DebugTravel(0, 0x99);
                    player.WarpTo(new(40, 104));
                    app.Step(2, Vector2.Zero);
                    if (stump)
                    {
                        for (int guard = 0; guard < 100 && (memory.ReadField("wActiveTileType") != 8 || player.EnemyContactZ != 0); guard++)
                            app.Step(1, Vector2.Up);
                        FailIf(memory.ReadField("wActiveTileType") != 8 || player.EnemyContactZ != 0,
                            "Rod swing $0:99 must approach the actual stump through its collision geometry.");
                        app.Step(8, Vector2.Zero);
                    }
                    player.Face((Vector2I)OracleObjectMath.StrictCardinalVector(direction * 8));
                    var native = new BraceletRom(session.Room, player.Position, direction, 0xff, game: OracleGame.Seasons);
                    native.LoadSave(session.Save);
                    native.SetNativeByte(0xccb6, (byte)memory.ReadField("wActiveTileType"));
                    int updates = 0;
                    var poses = new HashSet<int>();
                    void Check(bool press)
                    {
                        native.Update(item: ItemId.RodOfSeasons, held: press, pressed: press);
                        string context = $"ITEM$07 direction{direction}, stump={stump}, batch={batched}, update{updates++}";
                        CompareParentAnimationPublicationRom(player, native.ReadNativeByte, context);
                        FailIf(player.IsUsingRodOfSeasons != (native[0xd200] != 0), context + ": parent lifetime differs.");
                        var weapon = session.WorldRoot.GetChildren().OfType<RodOfSeasonsWeapon>().SingleOrDefault();
                        if (!player.IsUsingRodOfSeasons)
                        {
                            FailIf(weapon is not null || player.PublishedParentBody is not null,
                                context + ": completed swing must release its weapon and Link body picture.");
                            return;
                        }
                        foreach (int offset in new[] { 0x20, 0x21, 0x22, 0x23, 0x30, 0x31 })
                            FailIf(memory.ReadWramByte(0xd200 + offset) != native[0xd200 + offset],
                                context + $": Link animation byte${offset:x2} differs.");
                        FailIf(weapon is null || !weapon.Visible || !weapon.IsInsideTree(), context + ": physical rod is not displayed.");
                        var visual = SomariaPrivate<EnemyAnimationPlayer>(weapon!, "_visual");
                        int pose = visual.AnimationIndex;
                        poses.Add(pose);
                        // Native ITEM$07 OAM pointers are $07:$6668; their cells live in bank $12.
                        int oam = 0x48000 + (Word(0x1e668 + pose * 2) & 0x3fff);
                        CheckNativeOamTexture(rom, weaponTiles, oam, visual.CurrentTexture, visual.CurrentOffset, 2, false, context);
                        foreach (int offset in new[] { 0x0b, 0x0d, 0x0f, 0x26, 0x27, 0x30 })
                            FailIf(memory.ReadWramByte(0xd600 + offset) != native[0xd600 + offset],
                                context + $": weapon arc byte${offset:x2} differs.");
                        var body = player.PublishedParentBody ?? throw new InvalidOperationException(context + ": Link swing body is missing.");
                        var expected = player.Sprites.NativeFrame(native[0xd231], direction, false);
                        FailIf(body.Texture != expected.Texture || body.Offset != expected.Offset + Vector2.Down * player.EnemyContactZ,
                            context + ": displayed Link pose differs from the native parent graphic.");
                    }
                    app.Step(1, Vector2.Zero, held: ["attack"], pressed: ["attack"], afterUpdate: () => Check(true));
                    app.Step(24, Vector2.Zero, batched: batched, afterUpdate: () => Check(false));
                    FailIf(player.IsUsingRodOfSeasons || poses.Count != 3,
                        "ITEM$07 must display all three native swing poses and return to standing.");
                }
            }
            finally { root.Free(); }
        }
    }
}
