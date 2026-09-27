using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateTerrainShadows()
    {
        // data/terrainEffects.s: shadowAnimation = $01,$13,$04,$20,$08.
        TerrainShadowDefinition graphic = TerrainShadow.Load();
        FailIf(graphic.Texture.GetWidth() != 8 || graphic.Texture.GetHeight() != 16 ||
            graphic.Offset != new Vector2(-4, 3),
            "Terrain shadow must use the source single 8x16 cell at (-4,+3), tile $20.");
        foreach (int page in new[] { 0xc0, 0xc1, 0xd7, 0xdc, 0xdf })
        foreach (int frame in new[] { 0, 1, 254, 255, 256 })
        foreach (int z in new[] { -129, -128, -1, 0, 1, 127, 128, 255 })
        {
            bool expected = (byte)z >= 0x80 && (frame % 2 != page % 2);
            FailIf(TerrainShadow.ShouldDraw(z, true, 0, 0x96, frame, page) != expected,
                "Shadow must test zh bit 7 and global frame XOR native page, without float height tests.");
            FailIf(TerrainShadow.ShouldDraw(z, false, 0, 0, frame, page) ||
                TerrainShadow.ShouldDraw(z, true, 0x20, 0, frame, page) ||
                TerrainShadow.ShouldDraw(z, true, 0, 0x97, frame, page) ||
                TerrainShadow.ShouldDraw(null, true, 0, 0, frame, page),
                "Shadow lost visible bit 7/6, TILESETFLAG_SIDESCROLL, or B >= $97 suppression.");
        }

        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa8);
            _entities.Clear();
            _player.ApplicationUpdateOwned = true;
            _player.WarpTo(new(40, 80));
            for (int y = 8; y < 176; y += 16)
            for (int x = 8; x < 240; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), 0xa0, 0, 0);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                var drops = new[]
                {
                    _entities.Spawn<ItemDropEffect>(new ItemDropSpawn(0x02, new(104, 80))),
                    _entities.Spawn<ItemDropEffect>(new ItemDropSpawn(0x02, new(136, 80)))
                };
                FailIf(drops.Any(drop => drop.TerrainShadowDrawn),
                    "Uninitialized PART$01 must not draw a shadow.");
                bool sawAir = false, sawGround = false;
                for (int update = 0; update < 70; update += batched ? 2 : 1)
                {
                    StepGameplayUpdates(batched ? 2 : 1, Vector2.Zero, batched: batched);
                    for (int slot = 0; slot < 2; slot++)
                    {
                        ItemDropEffect drop = drops[slot];
                        bool airborne = drop.ZFixed < 0;
                        sawAir |= airborne;
                        sawGround |= drop.State == DropState.Grounded;
                        bool expected = airborne && ((_entities.FrameCounter + slot) % 2 == 1);
                        FailIf(drop.TerrainShadowDrawn != expected,
                            $"PART$01 page ${0xc0 + slot:x2}, update {update}: wrong shadow phase/landing gate.");
                        ObjectTerrainShadow node = drop.GetChildren().OfType<ObjectTerrainShadow>().Single();
                        FailIf(node.ZAsRelative || node.ZIndex >= ObjectDrawPriority.FixedLowPriorityZIndex ||
                            drop.Position != new Vector2(slot == 0 ? 104 : 136, 80),
                            "Shadow must render below object sprites without changing ground coordinates.");
                    }
                }
                FailIf(!sawAir || !sawGround, "Drop shadow fixture must exercise the bounce and landing.");
                drops[0].ClearHealthAndCollision();
                drops[1].ClearHealthAndCollision();
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                FailIf(_entities.Entities<ItemDropEffect>().Count != 0,
                    "Collected drops must release their shadows and native slots before repeat.");
            }

            var fairy = _entities.Spawn<ItemDropEffect>(new ItemDropSpawn(0x00, new(120, 80)));
            StepGameplayUpdates(12, Vector2.Zero, batched: batched);
            FailIf(fairy.ZFixed >= 0 || fairy.State != DropState.Grounded,
                "Fairy must remain airborne in state 2 for its shadow check.");
            fairy.AttachToItem(() => new ItemDropCarrier(1, fairy.Position, -6));
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(fairy.State != DropState.Attached || fairy.TerrainShadowDrawn ||
                ((ITerrainShadowSource)fairy).TerrainShadowZHigh is not null,
                "PART$01 state3 visible80 must suppress shadows even on an airborne carrier.");

            var bomb = _entities.Spawn<BombEffect>(new BombSpawn(
                _player, new BombDatabase().Data, 4, _ => { }));
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            bomb.SetHeldOffset(_player, new(0, -14));
            for (int update = 0; update < 4; update++)
            {
                StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                // First dynamic item is $d7: opposite to PART page $c0.
                FailIf(bomb.ZFixed != -14 * 256 ||
                    bomb.TerrainShadowDrawn != (_entities.FrameCounter % 2 == 0),
                    "Held ITEM$03 must cast its ground shadow in page $d7's phase.");
            }
            bomb.Throw(_player, new(0, -14), Vector2I.Zero, 0, 0);
            bool landed = false, exploded = false;
            for (int update = 0; update < 400 && !exploded; update++)
            {
                StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                if (bomb.State == BombState.Grounded)
                {
                    landed = true;
                    FailIf(bomb.TerrainShadowDrawn, "A landed bomb must stop casting an airborne shadow.");
                }
                if (bomb.State == BombState.Exploding)
                {
                    exploded = true;
                    FailIf(((ITerrainShadowSource)bomb).TerrainShadowZHigh is not null || bomb.TerrainShadowDrawn,
                        "Bomb explosion visible80 must clear terrain effects.");
                }
            }
            FailIf(!landed || !exploded, "Bomb shadow fixture must cover landing and explosion.");

            LoadValidationRoom(6, 0x29);
            _entities.Clear();
            _player.WarpTo(new(8, 8));
            var sideFairy = _entities.Spawn<ItemDropEffect>(new ItemDropSpawn(0x00, new(120, 80)));
            for (int update = 0; update < 16; update++)
            {
                StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                FailIf(sideFairy.TerrainShadowDrawn, "Side-scrolling fairy must not cast terrain shadows.");
            }
        }
        ReinitializeGameplayForValidation();
    }
}
