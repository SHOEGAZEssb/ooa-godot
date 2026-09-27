using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateObjectDrawOrder()
    {
        // bank0.s @getPriority: CP is unsigned and both Y operands are bytes.
        var npc = new NpcCharacter();
        foreach (var sample in new (float ObjectY, float LinkY, int Z, int Expected)[]
        {
            (51.75f, 40, 0, 9), (52, 40.75f, 0, 11),
            (11, 256, 0, 9), (12, 256, 0, 11),
            (0, 245, 0, 9), (1, 245, 0, 11),
            (200, 40, 1, 8), (200, 40, 16, 8),
            (200, 40, 17, 11), (200, 40, -1, 11)
        })
        {
            npc.Position = new Vector2(80, sample.ObjectY);
            npc.UpdateDrawPriority(new Vector2(80, sample.LinkY), sample.Z);
            FailIf(npc.ZIndex != sample.Expected, $"Native relative priority failed at Y={sample.ObjectY}/{sample.LinkY}, Z={sample.Z}.");
        }
        npc.SetFixedDrawPriority(ObjectDrawPriority.FixedHighPriorityZIndex);
        npc.UpdateDrawPriority(Vector2.Zero, 1);
        FailIf(npc.ZIndex != 12, "Fixed visible80 must bypass Link-relative priority.");
        npc.Free();

        LoadValidationRoom(3, 0xed);
        _player.WarpTo(new Vector2(80, 104));
        NpcCharacter syrup = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x5f);
        NpcCharacter cucco = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0xc9);
        FailIf(cucco.ZIndex != 12 || syrup.ZIndex != 9,
            "3:ed requires cucco visible80 and Syrup's relative priority82.");

        var position = new Vector2(80, 80);
        FailIf(!_entities.TrySpawnEnemy(EnemyId.Octorok, 0, position, "Draw-order validation", out string error), error);
        OctorokCharacter first = _entities.Entities<OctorokCharacter>().Single();
        FailIf(!_entities.TrySpawnEnemy(EnemyId.Octorok, 0, position, "Draw-order validation", out error), error);
        OctorokCharacter second = _entities.Entities<OctorokCharacter>().Single(n => n != first);
        Node2D part = _entities.Spawn(new EnemyDeathPuffSpawn(position, DropsItem: false));
        Node2D item = _entities.Spawn(new BoomerangSpawn(position, 8));
        // queueDrawEverything: ITEM before ENEMY before PART before INTERACTION.
        // OAM's first object wins; Godot's last equal-Z sibling wins.
        FailIf(new[] { item, first, second, part, syrup }.Any(n => n.ZIndex != 9) ||
            !(item.GetIndex() > first.GetIndex() && first.GetIndex() > second.GetIndex() &&
              second.GetIndex() > part.GetIndex() && part.GetIndex() > syrup.GetIndex()),
            "Native ITEM/ENEMY/PART/INTERACTION precedence or ascending enemy slots changed.");

        NpcCharacter a = _entities.Spawn<NpcCharacter>(new CutsceneNpcSpawn(syrup.Record, "OrderA"));
        NpcCharacter b = _entities.Spawn<NpcCharacter>(new CutsceneNpcSpawn(syrup.Record, "OrderB"));
        int slot = _entities.InteractionSlot(a);
        FailIf(a.GetIndex() < b.GetIndex(), "Earlier interaction allocation must win equal-priority overlap.");
        a.SetActive(false);
        NpcCharacter reused = _entities.Spawn<NpcCharacter>(new CutsceneNpcSpawn(syrup.Record, "OrderReused"));
        FailIf(_entities.InteractionSlot(reused) != slot || reused.GetIndex() < b.GetIndex(),
            "Deleted event actor allocation must be reused with its original OAM precedence.");
        a.SetActive(true);
        FailIf(_entities.InteractionSlot(a) <= _entities.InteractionSlot(b) || a.GetIndex() > b.GetIndex(),
            "Reactivated event actor must acquire a live allocation and reorder its retained node.");

        LoadValidationRoom(3, 0xed);
        // breakTileDebris.s's soundAndPriorityTable, independent of factory data.
        foreach (var effect in new (RoomEntitySpawn Spawn, int Priority)[]
        {
            (new GrassDebrisSpawn(position), 8),
            (new ShovelDebrisSpawn(position, Vector2I.Down), 8),
            (new RockDebrisSpawn(position), 12),
            (new PuzzlePuffSpawn(position, SoundId.MusNone), 12),
            (new EnemyClinkSpawn(position), 12),
            (new EnemySplashSpawn(position, HazardType.Water), 8)
        })
            FailIf(_entities.Spawn(effect.Spawn).ZIndex != effect.Priority,
                $"{effect.Spawn.GetType().Name}: native effect table priority mismatch.");
        GD.Print("Validated native priority bytes, subpixel boundaries, byte wrap, positive Z, fixed priority, cross-pool order, ascending slots and event actor slot reuse.");
    }

    private void ValidateItemDropDrawPriority()
    {
        foreach (bool batched in new[] { false, true })
        {
            LoadValidationRoom(3, 0xed);
            _player.WarpTo(new Vector2(32, 104));
            ItemDropEffect drop = _entities.Spawn<ItemDropEffect>(
                new ItemDropSpawn(ItemDropDatabase.Heart, new Vector2(80, 80)));
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(drop.ZIndex != 11 || drop.State != DropState.Bouncing,
                "PART_ITEM_DROP initialization must select visiblec1.");
            StepGameplayUpdates(90, Vector2.Zero, batched: batched);
            FailIf(drop.ZIndex != 8 || drop.State != DropState.Grounded,
                "PART_ITEM_DROP @doneBouncing must select visiblec3.");
            drop.AttachToItem(() => new ItemDropCarrier(0x06, new Vector2(80, 80), 0));
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(drop.ZIndex != 12 || drop.State != DropState.Attached,
                "PART_ITEM_DROP state3 must select visible80 when attaching to an item.");
        }
        GD.Print("Validated dropped-item visiblec1 -> visiblec3 -> visible80 through individual and batched gameplay updates.");
    }
}
