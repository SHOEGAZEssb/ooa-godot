using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateGaleSeedTutorial()
    {
        LoadValidationRoom(0, 0x13);
        _inventory.GiveTreasure(TreasureDatabase.TreasureSeedSatchel, 1);
        FailIf(_inventory.HasTreasure(0x23), "Fresh Gale tutorial fixture already owns TREASURE_GALE_SEEDS $23.");
        SeedOnTree firstGale = _entities.Entities<SeedOnTree>()[0];
        _player.WarpTo(firstGale.Position + new Vector2(0, 24));
        _entities.Update(1.0 / 60.0, _player);
        _entities.ApplySwordHit(firstGale.CollisionBounds, _player.Position);
        for (int i = 0; i < 120 && !_inventory.HasTreasure(0x23); i++)
            base._Process(1.0 / 60.0);
        FailIf(!_dialogue.IsOpen || _dialogue.CurrentMessage != DialogueBox.PlainText(new SeedTreeDatabase().Type(3).IntroMessage) ||
            _inventory.GaleSeeds != 0x06 || firstGale.State != SeedOnTreeState.AwaitingIntroText,
            $"First tree pickup did not display TX_002c, grant six Gale Seeds, and wait for text closure: open={_dialogue.IsOpen}, seeds=${_inventory.GaleSeeds:x2}, state={firstGale.State}, text={_dialogue.CurrentMessage}.");
        _dialogue.Close();
        _entities.Update(1.0 / 60.0, _player);
        SeedOnTree repeatGale = _entities.Entities<SeedOnTree>()[0];
        _entities.ApplySwordHit(repeatGale.CollisionBounds, _player.Position);
        for (int i = 0; i < 120 && _inventory.GaleSeeds == 0x06; i++)
            _entities.Update(1.0 / 60.0, _player);
        FailIf(_dialogue.IsOpen || _inventory.GaleSeeds != 0x12,
            "Repeat Gale tree pickup replayed TX_002c or failed to add six seeds.");
        GD.Print("Validated Symmetry Gale Seed tree first-pickup TX_002c and repeat-pickup suppression.");
    }

    private void ValidateGaleSeeds()
    {
        SeedRecord gale = new SeedSatchelDatabase().Gale;
        FailIf(gale.SeedItem != 0x23 || gale.InitialZ != -8 || gale.SpeedRaw != 0 ||
            gale.FlameCounter != 180 || gale.CollisionEffectCounter != 50 ||
            gale.FlameSound != 0x90 || gale.Collision != 0x9e,
            "ITEM_GALE_SEED $23 source attributes/activation rows diverged.");
        LoadBushValidationRoom();
        Vector2 point = new(80, 80);
        _currentRoom.SetPositionTileAndCollision(point, 0xc5, null, (long)_animationTicks);
        List<RoomEntitySpawn> spawns = [];
        List<int> sounds = [];
        EmberSeedEffect NewSeed(OracleRoomData room, int group, SeedLaunchKind kind = SeedLaunchKind.Satchel)
        {
            var seed = new EmberSeedEffect();
            seed.Initialize(gale, room, new BreakableTileDatabase(), point, Vector2I.Up,
                sounds.Add, (_, _) => { }, () => { }, () => 0, _ => null, _saveData, group,
                launchKind: kind);
            return seed;
        }
        var waiting = NewSeed(_currentRoom, 0);
        waiting.UpdateFrame(spawns);
        int expectedZ = -0x800;
        int expectedSpeed = -0x20;
        int arc = 0;
        while (expectedZ < 0)
        {
            expectedZ += expectedSpeed;
            if (expectedZ < 0) expectedSpeed += 0x1c;
            else expectedZ = 0;
            waiting.UpdateFrame(spawns);
            arc++;
            FailIf(waiting.ZFixed != expectedZ || waiting.PrecisePosition != point,
                $"Gale Satchel stationary arc disagreed with signed 8.8 arithmetic at update {arc}.");
        }
        FailIf(waiting.State != EmberState.Gale || waiting.FlameCounter != 180 ||
            !sounds.SequenceEqual(new[] { 0x52, 0x90 }),
            "Gale landing did not set state 2, counter $b4, and SND_BOMB_LAND/SND_GALE_SEED.");
        waiting.UpdateFrame(spawns);
        FailIf(waiting.FlameCounter != 179 || waiting.GalePalette != 0x0b,
            "Uncaptured outdoor gale failed its double animation/palette update.");
        for (int i = 0; i < 178; i++) waiting.UpdateFrame(spawns);
        FailIf(waiting.Finished || waiting.FlameCounter != 1, "Gale $b4 lifetime expired early.");
        waiting.UpdateFrame(spawns);
        FailIf(!waiting.Finished, "Gale $b4 lifetime missed its zero update.");
        waiting.Free();

        OracleRoomData indoor = _world.LoadRoom(2, 0x47);
        indoor.SetPositionTileAndCollision(point, 0, 0, 0);
        var indoorSeed = NewSeed(indoor, 2);
        for (int i = 0; i <= arc; i++) indoorSeed.UpdateFrame(spawns);
        indoorSeed.UpdateFrame(spawns);
        FailIf(indoorSeed.GaleSubstate != 3 || indoorSeed.GaleCounter2 != 0,
            "Indoor gale did not enter substate 3 without decrementing counter2.");
        for (int i = 0; i < 255; i++) indoorSeed.UpdateFrame(spawns);
        FailIf(indoorSeed.Finished || indoorSeed.GaleCounter2 != 1,
            "Indoor gale failed its $00->$ff counter2 wrap.");
        indoorSeed.UpdateFrame(spawns);
        FailIf(!indoorSeed.Finished, "Indoor gale missed its 256-update deletion boundary.");
        indoorSeed.Free();

        // seedsDontBounce uses the current tile. Force its first Shooter
        // tile to the imported overworld bush so activation cannot ricochet.
        var wallSeed = NewSeed(_currentRoom, 0, SeedLaunchKind.Shooter);
        Vector2 wallPoint = wallSeed.Position;
        _currentRoom.SetPositionTileAndCollision(wallPoint, 0xc5, null, 0);
        wallSeed.UpdateFrame(spawns);
        wallSeed.UpdateFrame(spawns);
        FailIf(wallSeed.State != EmberState.Gale || wallSeed.FlameCounter != 30,
            "Shooter gale wall activation did not select @data[$26] counter $1e.");
        for (int i = 0; i < 29; i++) wallSeed.UpdateFrame(spawns);
        FailIf(wallSeed.Finished || wallSeed.FlameCounter != 1, "Shooter wall gale expired early.");
        wallSeed.UpdateFrame(spawns);
        FailIf(!wallSeed.Finished, "Shooter wall gale failed to release its item on update $1e.");
        wallSeed.Free();

        // Capture starts on the update after landing, followed by 59 held
        // updates and the first -2 Z step on counter2's 60th update.
        _player.WarpTo(point);
        var capture = NewSeed(_currentRoom, 0);
        capture.GalePlayer = _player;
        for (int i = 0; i <= arc; i++) capture.UpdateFrame(spawns);
        FailIf(_player.GaleActive, "Gale caught Link on the landing update.");
        _entities.RuntimeState.SetWramByte(OracleRuntimeState.WarpsDisabledAddress, 1);
        capture.UpdateFrame(spawns);
        FailIf(_player.GaleActive || capture.FlameCounter != 179,
            "wWarpsDisabled $cc6e did not reject capture while advancing the gale lifetime.");
        _entities.RuntimeState.SetWramByte(OracleRuntimeState.WarpsDisabledAddress, 0);
        capture.UpdateFrame(spawns);
        FailIf(!_player.GaleActive || capture.GaleSubstate != 1 || capture.GaleCounter2 != 60,
            "Gale failed its vulnerable-Link capture gate.");
        for (int i = 0; i < 59; i++) capture.UpdateFrame(spawns);
        FailIf(capture.ZFixed != 0 || capture.GaleCounter2 != 1, "Gale left its ground hold before update $3c.");
        capture.UpdateFrame(spawns);
        FailIf(capture.ZFixed != -0x200 || capture.GaleSubstate != 2,
            "Gale did not ascend on counter2's zero update.");
        for (int i = 0; i < 63; i++) capture.UpdateFrame(spawns);
        FailIf(capture.ZFixed != -0x8000 || capture.Finished, "Gale ascent ended before signed Z crossed $80.");
        capture.UpdateFrame(spawns);
        FailIf(!capture.Finished || !capture.GaleMenuRequested, "Gale did not request MENU_GALE_SEED on Z wrap.");
        capture.Free();
        _player.ReturnFromGale();
        for (int i = 0; i < 100 && _player.GaleActive; i++) _player.AdvanceApplicationUpdate();
        FailIf(_player.GaleActive || _player.Position != point,
            "Gale cancel did not fall back to the original room coordinate.");

        var destinations = new GaleTreeWarpDatabase();
        _saveData.SetRoomFlag(0, 0x13, OracleSaveData.RoomFlagVisited);
        _saveData.SetRoomFlag(0, 0x78, OracleSaveData.RoomFlagVisited);
        FailIf(destinations.Get(_rooms, 0).Room != 0x13 || destinations.Move(_rooms, -1, 1) != 0 ||
            destinations.Move(_rooms, 0, 1) != 1 || destinations.Move(_rooms, 0, -1) != 1,
            "Gale tree selection failed its absent-scent-tree shift or eight-entry wrap.");
        _saveData.SetRoomFlag(0, 0xac, 0x80 | OracleSaveData.RoomFlagVisited);
        FailIf(destinations.Get(_rooms, 0) != new GaleTreeWarp(0xac, 0x54, 0x16),
            "Present scent-tree warp ignored room 0:ac bit 7.");

        _player.BeginGale();
        _mapMenu.OpenGale();
        for (int i = 0; i < 22; i++) _mapMenu.Update(1.0 / 60.0);
        FailIf(!_mapMenu.IsOpen || _mapScreen.CursorRoom != 0xac,
            "MENU_GALE_SEED did not open on the first visited source destination.");
        _mapMenu.PromptGale(cancel: true);
        _dialogue.SubmitChoiceForValidation(0);
        _mapMenu.Update(1.0 / 60.0);
        FailIf(_mapMenu.GaleState != 1, "TX_0301 Reselect did not return to tree selection.");
        _mapMenu.PromptGale(cancel: false);
        _dialogue.SubmitChoiceForValidation(1);
        _mapMenu.Update(1.0 / 60.0);
        FailIf(_mapMenu.GaleState != 1, "TX_0300 Cancel did not return to tree selection.");
        _mapMenu.PromptGale(cancel: true);
        _dialogue.SubmitChoiceForValidation(1);
        _mapMenu.Update(1.0 / 60.0);
        for (int i = 0; i < 22; i++) _mapMenu.Update(1.0 / 60.0);
        FailIf(_mapMenu.IsActive || !_player.IsRoomWarpFalling,
            "TX_0301 Go back did not close and transfer Link to falling arrival.");
        _player.WarpTo(point);

        _player.BeginGale();
        _mapMenu.OpenGale();
        for (int i = 0; i < 22; i++) _mapMenu.Update(1.0 / 60.0);
        _mapMenu.PromptGale(cancel: false);
        _dialogue.SubmitChoiceForValidation(0);
        _mapMenu.Update(1.0 / 60.0);
        for (int i = 0; i < 31; i++) _mapMenu.Update(1.0 / 60.0);
        FailIf(!_mapMenu.IsActive || _currentRoom.Id == 0xac,
            "Confirmed gale warp transferred before the 32-update normal fade completed.");
        _mapMenu.Update(1.0 / 60.0);
        FailIf(_mapMenu.IsActive || _rooms.ActiveGroup != 0 || _currentRoom.Id != 0xac ||
            !_player.IsRoomWarpFalling || _player.Position != new Vector2(72, 84),
            "Confirmed gale warp did not transfer at white to room 0:ac, position $54, transition $05.");
        for (int i = 0; i < 150 && _transitions.IsTransitioning; i++) _transitions.Update(1.0 / 60.0);
        FailIf(_transitions.IsTransitioning || _player.IsRoomWarpFalling,
            "Gale destination did not complete falling arrival and release transition ownership.");

        LoadValidationRoom(5, 0xb4);
        HardhatBeetleCharacter enemy = _entities.Entities<HardhatBeetleCharacter>()[0];
        var motion = new GaleSeedEnemyMotion(enemy);
        int randomCalls = 0;
        motion.Begin(new Vector2(80, 80), 0, () => { randomCalls++; return 0x08; });
        FailIf(randomCalls != 1 || enemy.Position != new Vector2(80, 80) || motion.ZFixed != -256,
            "collisionEffect29 failed its one RNG call, copied XY, or initial $ff Z.");
        motion.Update(0);
        FailIf(enemy.Position.X != 82 || motion.Counter != 29, "Gale shake first update must add $02 after decrementing $1e.");
        for (int i = 0; i < 28; i++) motion.Update(0);
        FailIf(motion.Counter != 1 || motion.ZFixed != -256, "Gale enemy lifted before its $1e boundary.");
        motion.Update(0);
        FailIf(motion.Counter != 0 || motion.ZFixed != -0x700,
            "Gale enemy failed initial -$600 speedZ on the zero update.");
        for (int i = 0; i < 80 && !enemy.IsDead; i++) motion.Update(0);
        FailIf(!enemy.IsDead, "Gale enemy did not leave the $b0 camera window.");

        LoadValidationRoom(5, 0xb4);
        _entities.Update(1.0 / 60.0, _player);
        enemy = _entities.Entities<HardhatBeetleCharacter>()[0];
        Vector2 enemyPoint = enemy.Position;
        foreach (var other in _entities.Entities<HardhatBeetleCharacter>().Where(e => e != enemy)) other.Position = new Vector2(16, 16);
        foreach (var other in _entities.Entities<KeeseCharacter>()) other.Position = new Vector2(16, 32);
        int countBefore = _entities.RoomEnemyCount;
        int killsBefore = _saveData.MapleKillCounter;
        var projectile = _entities.Spawn<EmberSeedEffect>(new EmberSeedSpawn(
            enemyPoint + new Vector2(4, 14), Vector2I.Up, gale, 5, SeedLaunchKind.Shooter));
        projectile.UpdateFrame(spawns);
        _entities.Update(1.0 / 60.0, _player);
        FailIf(!enemy.GaleCollisionDisabled || projectile.FlameCounter != 50 || projectile.Position != enemyPoint,
            $"Integrated gale did not catch Hardhat Beetle before movement and retain the $32 enemy-impact lifetime: caught={enemy.GaleCollisionDisabled}, enemy={enemy.Position}, seed={projectile.Position}, state={projectile.State}, counter={projectile.FlameCounter}, collision={enemy.CollisionEnabled}.");
        for (int i = 0; i < 90; i++) _entities.Update(1.0 / 60.0, _player);
        FailIf(_entities.Entities<HardhatBeetleCharacter>().Contains(enemy) ||
            _entities.RoomEnemyCount != countBefore - 1 || _saveData.MapleKillCounter != killsBefore ||
            _entities.Entities<EnemyDeathPuffEffect>().Count != 0,
            "Gale removal failed room counting or incorrectly ran enemyDie/drop/kill-counter effects.");
        LoadValidationRoom(4, 0x12);
        WallmasterCharacter hand = _entities.Entities<WallmasterCharacter>()[0];
        int handsBefore = hand.Remaining;
        var handMotion = new GaleSeedEnemyMotion(hand);
        handMotion.Begin(new Vector2(80, 80), 0, () => 0);
        for (int i = 0; i < 100 && handMotion.Active; i++) handMotion.Update(0);
        FailIf(handMotion.Active || hand.IsDead || hand.Remaining != handsBefore ||
            hand.State != WallmasterState.Waiting || hand.Visible || hand.TakeDeathPuff() is not null,
            "wallmaster_state_galeSeed incorrectly consumed a spawner hand or ran the normal death path.");

        LoadValidationRoom(1, 0x08);
        _saveData.SetRoomFlag(1, 0x08, OracleSaveData.RoomFlagVisited);
        _saveData.SetRoomFlag(1, 0xc1, OracleSaveData.RoomFlagVisited);
        FailIf(destinations.Get(_rooms, 0) != new GaleTreeWarp(0x08, 0x43, 0x17) ||
            destinations.Move(_rooms, 0, -1) != 5,
            "Past gale destinations failed their separate table or reverse visited-room wrap.");
        GD.Print("Validated Gale Seeds: source attributes, stationary throw, palettes, capture gates/ascent boundaries, both eras' tree flags, prompts, cancel, and enemy motion/RNG/cleanup.");
    }
}
