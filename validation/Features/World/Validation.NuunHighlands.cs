using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateNuunHighlands()
    {
        var data = new CarpenterDatabase();
        var quest = _roomEvents.Get<CarpenterEvent>();
        int[] rooms = [0x16, 0x17, 0x26, 0x27, 0x35, 0x36, 0x37];
        _saveData.SetLinkedGame(false);
        _saveData.SetGlobalFlag(data.Constant("bridge-flag"), false);
        foreach (int companion in new[] { 0x0b, 0x0c, 0x0d })
        {
            _inventory.AssignAnimalCompanion(companion);
            _runtimeState.SetWramByte(data.Constant("found-address"), 0);
            _runtimeState.SetWramByte(data.Constant("state-address"), 0);
            var workers = new List<(int Room, int Subid)>();
            foreach (int room in rooms)
            {
                LoadValidationRoom(0, room);
                var active = _entities.Entities<NpcCharacter>().Where(n => n.Active && n.Record.Id == 0x9a).ToArray();
                FailIf(active.Any(n => n.Record.SubId >> 4 != companion), $"Nuun $0:${room:x2} created the wrong companion's carpenter.");
                workers.AddRange(active.Select(n => (room, n.Record.SubId)));
                int flowers = companion == 0x0c && room is 0x16 or 0x27 ? 2 : 0;
                int ordinary = companion == 0x0b && room is 0x36 or 0x37 ? 1 : 0;
                int leevers = companion == 0x0d && room == 0x37 ? 3 : 0;
                FailIf(_entities.Entities<GopongaFlowerCharacter>().Count != flowers ||
                    _entities.Entities<OctorokCharacter>().Count != ordinary ||
                    _entities.Entities<BuzzBlobCharacter>().Count != ordinary * 2 ||
                    _entities.Entities<LeeverCharacter>().Count != leevers,
                    $"Nuun $0:${room:x2} companion ${companion:x2} ignored the ordered object's companion condition mask.");
            }
            FailIf(workers.Count != 3, $"Companion ${companion:x2} must have exactly three Nuun workers.");
            int found = 0;
            foreach (var worker in workers.OrderBy(w => w.Subid))
            {
                LoadValidationRoom(0, worker.Room);
                NpcCharacter npc = _entities.Entities<NpcCharacter>().Single(n => n.Active && n.Record.SubId == worker.Subid);
                _player.WarpTo(npc.Position + new Vector2(0, 16), recordSafe: false);
                StepRoomEventFrames(2);
                FailIf(!quest.TryInteractNpc(npc), "Nuun worker did not accept A.");
                for (int i = 0; i < 8 && !_dialogue.IsOpen; i++) StepRoomEventFrames(1);
                int idleText = 0x230a + (worker.Subid & 15);
                FailIf(!_dialogue.IsOpen || _dialogue.CurrentMessage != DialogueBox.PlainText(
                    data.Commands.OfType<CutsceneShowTextCommand>().First(c => c.TextId == idleText).Message),
                    $"Nuun worker ${worker.Subid:x2} lost its pre-search TX_{idleText:x4}.");
                _dialogue.Close();
                _runtimeState.SetWramByte(data.Constant("state-address"), 1);
                StepRoomEventFrames(3);
                FailIf(!quest.TryInteractNpc(npc), "Nuun worker did not resume its dialogue loop.");
                for (int i = 0; i < 8 && !_dialogue.IsOpen; i++) StepRoomEventFrames(1);
                FailIf(!_dialogue.IsOpen || !quest.BlocksGameplay, "Convincing a worker did not show TX_230f under disableinput.");
                _dialogue.Close();
                for (int i = 0; i < 200 && _runtimeState.ReadWramByte(data.Constant("found-address")) == found; i++) StepRoomEventFrames(1);
                found |= 1 << (worker.Subid & 15);
                FailIf(_runtimeState.ReadWramByte(data.Constant("found-address")) != found, "Worker departure did not set its low-nibble found bit.");
                if (found != 0x1c)
                {
                    FailIf(quest.BlocksGameplay || quest.MenusDisabled, "Returning a worker retained input/menu lock.");
                    LoadValidationRoom(0, worker.Room);
                    FailIf(_entities.Entities<NpcCharacter>().Any(n => n.Active && n.Record.SubId == worker.Subid), "Found Nuun worker respawned.");
                    _runtimeState.SetWramByte(data.Constant("state-address"), 0);
                }
            }
            for (int i = 0; i < 240 && _transitions.IsTransitioning; i++) _transitions.UpdateWarp(1.0 / 60.0);
            FailIf(_rooms.ActiveGroup != 0 || _rooms.CurrentRoom.Id != 0x25, "Last Nuun worker failed to warp Link to the boss.");
            for (int i = 0; i < 1800 && !_saveData.HasGlobalFlag(data.Constant("bridge-flag")); i++)
            {
                if (_dialogue.IsOpen) _dialogue.Close();
                StepRoomEventFrames(1);
            }
            FailIf(!_saveData.HasGlobalFlag(data.Constant("bridge-flag")), $"Companion ${companion:x2} search did not complete bridge construction.");
            _dialogue.Close();
            _saveData.SetGlobalFlag(data.Constant("bridge-flag"), false);
        }

        _runtimeState.SetWramByte(data.Constant("state-address"), 1);
        _runtimeState.SetWramByte(data.Constant("found-address"), 4);
        LoadValidationRoom(0, 0x35);
        _player.WarpTo(new Vector2(16, 72), recordSafe: false);
        StepRoomEventFrames(1);
        FailIf(!_dialogue.ChoiceActive, "Nuun exit did not ask whether to abandon the search.");
        _dialogue.SubmitChoiceForValidation(1);
        StepRoomEventFrames(1);
        FailIf(_player.Position.X != 18 || _player.InvincibilityFrames != -60 ||
            !CompanionRuntimeState.MountingDisabled(_runtimeState) || quest.SearchState != 1,
            "Continuing the search lost the US boundary push, invincibility, mounting lock, or search state.");
        StepRoomEventFrames(119);
        FailIf(!CompanionRuntimeState.MountingDisabled(_runtimeState), "Nuun mounting lock expired before update 120.");
        StepRoomEventFrames(1);
        FailIf(CompanionRuntimeState.MountingDisabled(_runtimeState), "Nuun mounting lock did not expire on update 120.");
        _player.WarpTo(new Vector2(16, 72), recordSafe: false);
        StepRoomEventFrames(1);
        _dialogue.SubmitChoiceForValidation(0);
        StepRoomEventFrames(1);
        FailIf(Enumerable.Range(0xcfd0, 16).Any(address => _runtimeState.ReadWramByte(address) != 0), "Abandoning Nuun search did not clear its entire 16-byte workspace.");
        GD.Print("Validated all seven Nuun rooms, three companion enemy/worker variants, worker return and bridge completion, and search-exit choices.");
    }

    private void ValidateNuunEnemyStates()
    {
        var database = new EnemyDatabase();
        var rng = new OracleRandom();
        var spawns = new List<RoomEntitySpawn>();
        var flower = new GopongaFlowerCharacter();
        flower.Initialize(database.ImportedEnemy(0x25), new Vector2(72, 72), rng);
        flower.PrepareForScreenTransition();
        for (int i = 0; i < 89; i++) flower.UpdateFrame(spawns);
        FailIf(flower.State != 8 || flower.Counter != 1 || rng.Calls != 0, "Goponga initial closed timer is not 90 updates.");
        flower.UpdateFrame(spawns);
        FailIf(flower.State != 9 || flower.Counter != 60 || flower.AnimationIndex != 1, "Goponga did not open for 60 updates.");
        for (int i = 0; i < 19; i++) flower.UpdateFrame(spawns);
        FailIf(rng.Calls != 0, "Goponga consumed RNG before counter 40.");
        flower.UpdateFrame(spawns);
        FailIf(rng.Calls != 1, "Goponga did not consume one RNG call at counter 40.");
        for (int i = 0; i < 40; i++) flower.UpdateFrame(spawns);
        FailIf(flower.State != 8 || flower.Counter != 120 || flower.AnimationIndex != 0 || rng.Calls != 1, "Goponga close cycle or RNG cadence changed.");
        flower.Free();

        _inventory.AssignAnimalCompanion(0x0c);
        LoadValidationRoom(0, 0x16);
        var target = _entities.Entities<GopongaFlowerCharacter>().First();
        var adapter = _entities.EntityAdapters<GopongaFlowerRoomEntity>().First();
        int health = target.Health;
        adapter.SetLinkSwordState(SwordActionState.Swing, 2);
        FailIf(!adapter.ApplySwordHit(target.CollisionBounds, target.Position, 4, EnemyKnockbackStrength.Normal, spawns) ||
            target.Health != health || !spawns.OfType<EnemyClinkSpawn>().Any(), "Goponga did not resist the level-2 sword with a clink.");
        adapter.SetLinkSwordState(SwordActionState.Spin, 1);
        FailIf(!adapter.ApplySwordHit(target.CollisionBounds, target.Position, 1, EnemyKnockbackStrength.High, spawns) ||
            target.Health != health - 1 || target.KnockbackCounter != 0 || target.InvincibilityCounter != 0x20,
            "Goponga spin damage did not use collisionEffect0b's no-recoil invincibility.");
        var edible = _entities.EntityAdapters<GopongaFlowerRoomEntity>().Last();
        var edibleNode = _entities.Entities<GopongaFlowerCharacter>().Last();
        var dimitriData = new DimitriDatabase();
        FailIf(!dimitriData.AcceptsMouthCollision(edible.DimitriCollisionType) ||
            !dimitriData.CanSwallow(edible.DimitriCollisionMode) ||
            !edible.TrySwallow(edibleNode.CollisionBounds) || !edibleNode.IsDead,
            "Dimitri could not swallow the source Goponga Flower collision mode $23.");

        _inventory.AssignAnimalCompanion(0x0d);
        LoadValidationRoom(0, 0x37);
        var room = _rooms.CurrentRoom;
        for (int y = 0; y < 8; y++) for (int x = 0; x < 10; x++)
            room.SetPositionTileAndCollision(new Vector2(x * 16 + 8, y * 16 + 8), 0, 0, 0);
        var red = new LeeverCharacter();
        var redRng = new OracleRandom();
        red.Initialize(database.ImportedEnemy(0x0b, 1), room, new Vector2(40, 40), redRng);
        red.PrepareForScreenTransition();
        for (int i = 0; i < 120 && red.State == LeeverState.Underground; i++) red.UpdateFrame(new Vector2(88, 88), Vector2I.Down, 0);
        FailIf(red.State != LeeverState.Emerging || redRng.Calls != 2 || red.Position.X > 120 || red.Position.Y > 120,
            "Red Leever did not use its single random $77-masked spawn candidate.");
        for (int i = 0; i < 100 && red.State != LeeverState.Chasing; i++) red.UpdateFrame(new Vector2(88, 88), Vector2I.Down, 0);
        int calls = redRng.Calls;
        red.UpdateFrame(new Vector2(88, 88), Vector2I.Down, 0);
        FailIf(redRng.Calls != calls + 1, "Red Leever chase did not consume its per-update retarget RNG.");
        red.Free();
        GD.Print("Validated Nuun Goponga 90/60/40/120 counters and red Leever random spawn/retarget consumption.");
    }

    private void ValidateNuunWaterfall()
    {
        _inventory.AssignAnimalCompanion(0x0c);
        var tutorials = new CompanionTutorialDatabase();
        foreach (int room in new[] { 0x27, 0x36, 0x37 })
        {
            var record = tutorials.GetRoomRecords(0, room).Single();
            _saveData.WriteWramByte(record.FlagAddress, (byte)(_saveData.ReadWramByte(record.FlagAddress) & ~(1 << record.FlagBit)));
            CompanionRuntimeState.Begin(_runtimeState, 0x0c, room, new Vector2(record.X, record.Y + 16), 0);
            LoadValidationRoom(0, room);
            _entities.Entities<DimitriCompanionRoomEntity>().Single().UpdatePlayerForcedMovement(_player);
            var tutorial = _entities.Entities<CompanionTutorialRoomEntity>().Single();
            var frame = new RoomEntityFrame(_player, 0, false);
            var effects = new List<RoomEntitySpawn>();
            tutorial.UpdateFrame(frame, effects);
            tutorial.UpdateFrame(frame, effects);
            FailIf(!tutorial.TextShown || !_dialogue.IsOpen || record.TextId != 0x2108,
                $"Nuun $0:${room:x2} did not show mounted waterfall tutorial TX_2108.");
            _dialogue.Close();
            CompanionRuntimeState.Update(_runtimeState, 0x0c, room, new Vector2(record.X, record.Y), 0);
            _player.WarpTo(new Vector2(record.LinkXMax, record.Y), recordSafe: false);
            tutorial.UpdateFrame(frame, effects);
            FailIf(tutorial.Finished, "Waterfall tutorial accepted the excluded upper X boundary.");
            _player.WarpTo(new Vector2(record.LinkXMin, record.Y), recordSafe: false);
            tutorial.UpdateFrame(frame, effects);
            FailIf(!tutorial.Finished || (_saveData.ReadWramByte(record.FlagAddress) & (1 << record.FlagBit)) == 0,
                "Waterfall tutorial did not persist completion at its inclusive X/Y boundary.");
            CompanionRuntimeState.Clear(_runtimeState, 0x0c);
        }
        // Suppress the already-covered tutorial while testing warp boundaries.
        _saveData.WriteWramByte(0xc649, (byte)(_saveData.ReadWramByte(0xc649) | 8));
        LoadValidationRoom(0, 0x37);
        _player.WarpTo(new Vector2(32, 24), recordSafe: false);
        var warp = _entities.Entities<WaterfallWarpRoomEntity>().Single();
        StepRoomEventFrames(1);
        FailIf(warp.State != 1 || _transitions.IsTransitioning, "Waterfall arrival overlap did not wait for Link to leave.");
        _player.WarpTo(new Vector2(100, 88), recordSafe: false);
        StepRoomEventFrames(1);
        FailIf(warp.State != 2 || !_entities.WarpTilesDisabled, "Waterfall warp did not arm and disable ordinary warp tiles.");
        _player.WarpTo(new Vector2(32, 24), recordSafe: false);
        StepRoomEventFrames(1);
        FailIf(_transitions.IsTransitioning, "Waterfall cave admitted Link on foot.");
        CompanionRuntimeState.Begin(_runtimeState, 0x0c, 0x37, new Vector2(32, 24), 0);
        _entities.Spawn<DimitriCompanionRoomEntity>(new DimitriCompanionSpawn(new Vector2(32, 24), 0, 0, 0x37, true));
        StepRoomEventFrames(1);
        FailIf(!_transitions.IsTransitioning, "Mounted Dimitri did not enter the waterfall cave.");
        for (int i = 0; i < 240 && _transitions.IsTransitioning; i++) _transitions.UpdateWarp(1.0 / 60.0);
        FailIf(_rooms.ActiveGroup != 5 || _rooms.CurrentRoom.Id != 0xb8 ||
            !_entities.Entities<DimitriCompanionRoomEntity>().Any(d => d.LinkRiding),
            "Waterfall warp did not retain mounted Dimitri at $5:$b8.");
        StepRoomEventFrames(1);
        FailIf(!_entities.ScreenTransitionsDisabled, "Waterfall cave did not disable ordinary screen exits.");
        var dimitri = _entities.Entities<DimitriCompanionRoomEntity>().Single();
        dimitri.SetScreenTransitionBoundaryCoordinate(false, 0xa8, _player);
        StepRoomEventFrames(1);
        FailIf(!_transitions.IsTransitioning, "Waterfall cave did not exit at companion Y=$a8.");
        for (int i = 0; i < 240 && _transitions.IsTransitioning; i++) _transitions.UpdateWarp(1.0 / 60.0);
        FailIf(_rooms.ActiveGroup != 0 || _rooms.CurrentRoom.Id != 0x37 ||
            !_entities.Entities<DimitriCompanionRoomEntity>().Any(d => d.LinkRiding) ||
            CompanionRuntimeState.Read(_runtimeState).Position != new Vector2(32, 40),
            "Waterfall return lost hardcoded warp B's $0e position adjustment or mounted Dimitri.");
        GD.Print("Validated Nuun waterfall overlap gate, mounted-only entry, cave exit and retained Dimitri round trip.");
    }
}
