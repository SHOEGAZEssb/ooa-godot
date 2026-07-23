using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateGraveyardCrowsAndDropProducers()
    {
        var enemies = new EnemyDatabase();
        var npcs = new NpcDatabase();
        var visibility = new NpcVisibilityRuleDatabase();
        var runtime = new OracleRuntimeState();
        var predicateSave = OracleSaveData.CreateStandardGame();
        NpcRecord ghini = npcs.GetRoomNpcs(0, 0x5d).Single(npc =>
            npc.Id == 0xcb && npc.SubId == 0x00);

        if (enemies.CrowRecordCount != 3 || enemies.CrowInstanceCount != 3 ||
            enemies.GetRoomCrows(0, 0x5d).Single() is not
                { Y: 0x78, X: 0x78, SpeedRaw: 0x32, Health: 1,
                  CollisionRadiusY: 6, CollisionRadiusX: 6, DamageQuarters: 2 } ||
            enemies.GetRoomCrows(0, 0x6d).Count != 2 ||
            enemies.GetRoomCrows(0, 0x7d).Count != 0 ||
            ghini is not { Y: 0x68, X: 0x88, TextId: 0x4d05, Palette: 2 } ||
            visibility.ShouldShow(ghini, predicateSave, runtime))
        {
            throw new InvalidOperationException(
                "Rooms 0:5d/0:6d/0:7d lost their imported Crow/Ghini roster or initial predicate.");
        }
        predicateSave.SetLinkedGame(true);
        if (visibility.ShouldShow(ghini, predicateSave, runtime))
            throw new InvalidOperationException(
                "The room 0:5d Ghini appeared in a linked file before D1 was obtained.");
        predicateSave.WriteWramByte(0xc6bf, 0x01);
        if (!visibility.ShouldShow(ghini, predicateSave, runtime))
            throw new InvalidOperationException(
                "The room 0:5d Ghini did not appear for linked + D1.");
        predicateSave.SetLinkedGame(false);
        if (visibility.ShouldShow(ghini, predicateSave, runtime))
            throw new InvalidOperationException(
                "The room 0:5d Ghini ignored the linked-file predicate.");

        var root = new Node { Name = "GraveyardCrowAndDropValidation" };
        AddChild(root);
        var save = OracleSaveData.CreateStandardGame();
        var treasures = new TreasureDatabase();
        var inventory = new InventoryState(treasures, save);
        inventory.GiveTreasure(TreasureDatabase.TreasureBombs, 0x04);
        inventory.GiveTreasure(TreasureDatabase.TreasureEmberSeeds, 0x05);
        var manager = new RoomEntityManager(
            root, npcs, enemies, new ItemDropDatabase(),
            new TimePortalDatabase(), new OracleRandom(), save,
            new OracleRuntimeState(), inventory, treasures: treasures);

        OracleRoomData room05d = _world.LoadRoom(0, 0x5d);
        manager.LoadRoom(0, room05d);
        CrowCharacter crow = manager.Entities<CrowCharacter>().Single();
        ItemDropProducer bombProducer = manager.Entities<ItemDropProducer>().Single();
        if (crow.Position != new Vector2(0x78, 0x78) ||
            crow.State != CrowState.Perched || crow.CollisionEnabled ||
            bombProducer.Position != new Vector2(0x88, 0x38))
        {
            throw new InvalidOperationException(
                "Room 0:5d did not create the fixed perched Crow and hidden Bomb producer.");
        }

        _player.WarpTo(new Vector2(0x20, 0x20), recordSafe: false);
        manager.Update(1.0 / 60.0, _player);
        if (!bombProducer.Initialized || crow.State != CrowState.Perched)
            throw new InvalidOperationException(
                "Crow/drop-producer state 0 did not capture the source initial state.");

        _player.WarpTo(crow.Position + Vector2.Down * 0x30, recordSafe: false);
        manager.Update(1.0 / 60.0, _player);
        if (crow.State != CrowState.Rising || crow.Counter1 != 25)
            throw new InvalidOperationException(
                "The perched Crow lost its inclusive $30/$18 approach rectangle.");
        for (int frame = 0; frame < 25; frame++)
            manager.Update(1.0 / 60.0, _player);
        int aimDelta = (crow.Angle - 0x10) & 0x1f;
        if (aimDelta > 0x10)
            aimDelta -= 0x20;
        if (crow.State != CrowState.Charging || crow.Z != -6 ||
            !crow.CollisionEnabled || crow.Counter2 != 90 ||
            Math.Abs(aimDelta) != 4)
        {
            throw new InvalidOperationException(
                "The Crow did not rise six pixels for 25 updates and begin its randomized charge.");
        }
        Vector2 chargeStart = crow.PrecisePosition;
        manager.Update(1.0 / 60.0, _player);
        if (crow.Counter2 != 89 ||
            !Mathf.IsEqualApprox(
                crow.PrecisePosition.DistanceTo(chargeStart), 1.25f, 0.01f))
        {
            throw new InvalidOperationException(
                "The Crow charge lost SPEED_140 high-byte movement or counter timing.");
        }

        byte bombTile = room05d.GetMetatile(bombProducer.Position);
        room05d.SetPositionTileAndCollision(
            bombProducer.Position, (byte)(bombTile ^ 1), null, 0);
        manager.Update(1.0 / 60.0, _player);
        ItemDropEffect bombDrop = manager.Entities<ItemDropEffect>().Single();
        if (manager.Entities<ItemDropProducer>().Count != 0 ||
            bombDrop.SubId != ItemDropDatabase.Bombs ||
            bombDrop.State != DropState.Bouncing ||
            bombDrop.ElapsedFrames != 1)
        {
            throw new InvalidOperationException(
                "Room 0:5d's tile change did not replace the producer with an immediately updated Bomb drop.");
        }

        _player.WarpTo(new Vector2(0x00, 0x100), recordSafe: false);
        for (int frame = 0;
            frame < 180 && manager.Entities<CrowCharacter>().Count != 0;
            frame++)
        {
            manager.Update(1.0 / 60.0, _player);
        }
        if (!crow.DeletedOutOfBounds ||
            manager.Entities<CrowCharacter>().Count != 0 ||
            manager.Entities<EnemyDeathPuffEffect>().Count != 0)
        {
            throw new InvalidOperationException(
                "A charging Crow did not use silent enemyDelete at the source screen bounds.");
        }

        manager.LoadRoom(0, _world.LoadRoom(0, 0x5d));
        if (manager.Entities<ItemDropProducer>().Count != 0 ||
            manager.Entities<CrowCharacter>().Count != 1)
            throw new InvalidOperationException(
                "Room 0:5d's producer recent-defeat mark suppressed the wrong object on re-entry.");

        OracleRoomData room06d = _world.LoadRoom(0, 0x6d);
        manager.LoadRoom(0, room06d);
        ItemDropProducer[] emberProducers = manager.Entities<ItemDropProducer>()
            .OrderBy(producer => producer.Position.Y)
            .ThenBy(producer => producer.Position.X)
            .ToArray();
        CrowCharacter[] crows = manager.Entities<CrowCharacter>()
            .OrderBy(enemy => enemy.Position.X)
            .ToArray();
        if (emberProducers.Length != 2 || crows.Length != 2 ||
            emberProducers[0].Position != new Vector2(0x88, 0x18) ||
            emberProducers[1].Position != new Vector2(0x78, 0x28) ||
            crows[0].Position != new Vector2(0x18, 0x38) ||
            crows[1].Position != new Vector2(0x88, 0x38))
        {
            throw new InvalidOperationException(
                "Room 0:6d lost its ordered two-producer/two-Crow fixed placements.");
        }

        manager.LoadRoom(0, _world.LoadRoom(0, 0x7d));
        if (manager.Entities<CrowCharacter>().Count != 0 ||
            manager.Entities<ItemDropProducer>().Count != 0 ||
            manager.Entities<NpcCharacter>().Count != 0 ||
            enemies.GetRoomObjects(0, 0x7d).Count != 0 ||
            npcs.GetRoomNpcs(0, 0x7d).Count != 0)
        {
            throw new InvalidOperationException(
                "Room 0:7d should retain its intentionally empty object/NPC stream.");
        }

        manager.Clear();
        RemoveChild(root);
        root.Free();

        var ghiniData = new LinkedGameGhiniDatabase();
        var secretSave = OracleSaveData.CreateStandardGame();
        secretSave.WriteWramByte(0xc600, 0x34);
        secretSave.WriteWramByte(0xc601, 0x12);
        byte[] secret = ghiniData.GenerateSecretValues(secretSave);
        if (!secret.SequenceEqual(new byte[] { 0x0b, 0x29, 0x13, 0x18, 0x2f }) ||
            secretSave.ReadWramByte(0xc6fb) != 0x21)
        {
            throw new InvalidOperationException(
                "The five-character Graveyard secret lost its source bit packing, checksum, or XOR cipher.");
        }

        bool linkedBefore = _saveData.IsLinkedGame;
        byte essencesBefore = _saveData.ReadWramByte(0xc6bf);
        byte gameIdLowBefore = _saveData.ReadWramByte(0xc600);
        byte gameIdHighBefore = _saveData.ReadWramByte(0xc601);
        byte shortSecretBefore = _saveData.ReadWramByte(0xc6fb);
        bool beganBefore = _saveData.HasGlobalFlag(ghiniData.Data.BeganFlag);
        _saveData.SetLinkedGame(true);
        _saveData.WriteWramByte(0xc6bf, (byte)(essencesBefore | 0x01));
        _saveData.WriteWramByte(0xc600, 0x34);
        _saveData.WriteWramByte(0xc601, 0x12);
        _saveData.SetGlobalFlag(ghiniData.Data.BeganFlag, value: false);
        _saveData.CommitInventoryChange();

        LoadValidationRoom(0, 0x5d);
        NpcCharacter liveGhini = _entities.Entities<NpcCharacter>().Single(npc =>
            npc.Record.Id == 0xcb && npc.Record.SubId == 0x00);
        if (!liveGhini.Visible)
            throw new InvalidOperationException(
                "The linked+D1 Ghini predicate did not survive actual room loading.");
        _player.WarpTo(liveGhini.Position + Vector2.Down * 16.0f, recordSafe: false);
        _player.Face(Vector2I.Up);
        if (!_interactions.TryInteract(_player) || !_dialogue.ChoiceActive ||
            !_dialogue.CurrentMessage.Contains("Do you?", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The room 0:5d Ghini did not open TX_4d05's Yes/No offer.");
        }
        _dialogue.SubmitChoiceForValidation(1);
        _interactions.Update(0.0, _player);
        if (_dialogue.ChoiceActive ||
            !_dialogue.CurrentMessage.Contains("Suit yourself", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Choosing No did not follow linkedGameNpcScript to TX_4d06.");
        }
        _dialogue.Close();
        _interactions.Update(0.0, _player);

        if (!_interactions.TryInteract(_player))
            throw new InvalidOperationException("The Ghini offer loop could not be restarted.");
        _dialogue.SubmitChoiceForValidation(0);
        _interactions.Update(0.0, _player);
        if (!_dialogue.ChoiceActive ||
            !_dialogue.CurrentMessage.Contains("Holodrum", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Choosing Yes did not open TX_4d07's extra confirmation box.");
        }
        _dialogue.SubmitChoiceForValidation(1);
        _interactions.Update(0.0, _player);
        if (!_dialogue.ChoiceActive || _dialogue.SelectedChoice != 1 ||
            !_dialogue.CurrentMessage.Contains("Holodrum", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Choosing No did not repeat the Ghini's extra confirmation.");
        }
        _dialogue.SubmitChoiceForValidation(0);
        _interactions.Update(0.0, _player);
        if (!_dialogue.ChoiceActive ||
            _dialogue.CurrentMessage.Contains("\\secret1", StringComparison.Ordinal) ||
            !_saveData.HasGlobalFlag(ghiniData.Data.BeganFlag) ||
            _saveData.ReadWramByte(0xc6fb) != 0x21)
        {
            throw new InvalidOperationException(
                "The Ghini did not generate/substitute the Graveyard secret and set its began flag.");
        }
        _dialogue.SubmitChoiceForValidation(1);
        _interactions.Update(0.0, _player);
        if (!_dialogue.ChoiceActive || _dialogue.SelectedChoice != 1)
            throw new InvalidOperationException(
                "Choosing No did not repeat TX_4d08's generated secret.");
        _dialogue.SubmitChoiceForValidation(0);
        _interactions.Update(0.0, _player);
        if (_dialogue.ChoiceActive ||
            !_dialogue.CurrentMessage.Contains("Good luck", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Confirming the Graveyard secret did not show TX_4d09.");
        }
        _dialogue.Close();
        _interactions.Update(0.0, _player);

        _saveData.SetLinkedGame(linkedBefore);
        _saveData.WriteWramByte(0xc6bf, essencesBefore);
        _saveData.WriteWramByte(0xc600, gameIdLowBefore);
        _saveData.WriteWramByte(0xc601, gameIdHighBefore);
        _saveData.WriteWramByte(0xc6fb, shortSecretBefore);
        _saveData.SetGlobalFlag(ghiniData.Data.BeganFlag, beganBefore);
        _saveData.CommitInventoryChange();

        GD.Print("Validated rooms 0:5d/0:6d/0:7d: three perched Crows, hidden " +
            "tile-change Bomb/Ember producers, recent-defeat semantics, empty 0:7d " +
            "roster, linked+D1 Ghini visibility, and five-character secret encoding.");
    }
}
