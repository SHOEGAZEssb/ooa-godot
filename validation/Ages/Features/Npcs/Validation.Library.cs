using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    // Start on an open aisle and walk through the real collision geometry.
    // This is intentionally local to the actor under test, not a library solver.
    private void ApproachLibraryActor(NpcCharacter actor, bool batched,
        Action<Vector2>? afterUpdate = null, Action? positionArranged = null,
        Func<bool>? counterpartReachable = null)
    {
        Vector2I[] directions = [Vector2I.Up, Vector2I.Left, Vector2I.Down, Vector2I.Right];
        foreach (int distance in new[] { 40, 32, 24, 20, 18 })
        foreach (int offset in new[] { 0, -4, 4 })
        foreach (var direction in directions)
        {
            Vector2 start = actor.Position - (Vector2)direction * distance + new Vector2(direction.Y, direction.X) * offset;
            if (start.X < 8 || start.Y < 8 || start.X >= _currentRoom.Width - 8 || start.Y >= _currentRoom.Height - 8 ||
                _collision.Collides(start) || _entities.BlocksLink(start) || _currentRoom.GetTerrainInfo(start).Collision != 0) continue;
            _player.WarpTo(start); _player.Face(direction);
            positionArranged?.Invoke();
            StepGameplayUpdates(2, Vector2.Zero, batched: batched,
                afterUpdate: afterUpdate is null ? null : () => afterUpdate(Vector2.Zero));
            string action = direction == Vector2I.Up ? "move_up" : direction == Vector2I.Down ? "move_down" : direction == Vector2I.Left ? "move_left" : "move_right";
            for (int i = 0; i < 55 && (!actor.CanTalkTo(_player) || counterpartReachable?.Invoke() == false); i++)
                StepGameplayUpdates(1, direction, [action], [action], batched,
                    afterUpdate is null ? null : () => afterUpdate(direction));
            if (!actor.CanTalkTo(_player) || counterpartReachable?.Invoke() == false ||
                _collision.Collides(_player.Position) || _entities.BlocksLink(_player.Position)) continue;
            return;
        }
        throw new InvalidOperationException($"Library ${actor.Record.Id:x2}:${actor.Record.SubId:x2} is unreachable through room geometry. Link={_player.Position}; active={actor.Active}; sensitive={actor.ScriptButtonSensitive}.");
    }
    private void ValidateRoom5ecLibraryNpcs()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool clean in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _inventory.EquipA(0); _inventory.EquipB(0);
            _saveData.SetGlobalFlag(0x30, clean);
            LoadValidationRoom(5, 0xec);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            var actors = _entities.Entities<NpcCharacter>().Where(n => n.Record.Id == 0x52).ToArray();
            FailIf(!actors.Select(n => n.Record.SubId).SequenceEqual(new[] { 3, 4, 5, 6 }), "Room 5:ec lost source object order $52:$03-$06.");
            int[] xs = [0xc0,0x48,0x28,0x88], ys = [0x58,0x78,0x48,0x18], palettes = [2,0,0,1];
            int[] radiiY = [0x12,6,6,6];
            string[] polluted = ["Fairy Powder", "It's nonsense!", "vanished", "locked away"];
            string[] cleaned = ["seas are clean", "Fairy Queen", "hero who saved", "missing"];
            for (int i = 0; i < actors.Length; i++)
            {
                var actor = actors[i];
                int text = 0x3300 + i * 2 + (clean ? 1 : 0);
                FailIf(actor.Position != new Vector2(xs[i], ys[i]) || actor.TextId != text || actor.Record.Palette != palettes[i] ||
                    actor.ObjectCollisionBounds.Size != new Vector2(12, radiiY[i]*2) || !actor.Visible,
                    $"Room 5:ec $52:${i+3:x2} lost source position, text, palette, radius, or presentation.");
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    ApproachLibraryActor(actor, batched);
                    StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
                    FailIf(!_dialogue.IsOpen || actor.TextId != text ||
                        !_dialogue.CurrentMessage.Replace("\n", " ").Contains((clean ? cleaned : polluted)[i], StringComparison.OrdinalIgnoreCase),
                        $"Room 5:ec TX_{text:x4} did not open through reachable A-button contact.");
                    _dialogue.Close();
                    StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                    FailIf(actor.CurrentScriptAnimationSource != new LibraryDatabase().Animation(0x52, i is 0 or 3 ? 2 : 0),
                        $"Room 5:ec $52:${i+3:x2} did not restore var38 animation after text.");
                }
            }
            _saveData.SetGlobalFlag(0x30, !clean);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(actors[0].TextId != (clean ? 0x3301 : 0x3300), "Library dialogue must retain the pollution snapshot until re-entry.");
            LoadValidationRoom(5, 0xec); StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            FailIf(_entities.Entities<NpcCharacter>().First(n => n.Record.Id == 0x52).TextId != (clean ? 0x3300 : 0x3301),
                "Library re-entry did not reselect the live pollution flag.");
        }
        GD.Print("Validated room 5:ec library object order, geometry, palettes/radii, both pollution texts, repeat conversations, animation restoration and entry snapshot in split/batched gameplay.");
    }
    private void OpenLibraryKeeperPath(bool batched)
    {
        var book = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0xb4 && n.Record.SubId == 5);
        ApproachLibraryActor(book, batched);
        StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
        FailIf(!_dialogue.IsOpen, "Library final path book did not accept reachable A contact.");
        _dialogue.Close(); StepGameplayUpdates(22, Vector2.Zero, batched: batched);
    }
    private void ValidateRoom5e4FairyPowder()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool obtained in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _inventory.EquipA(0); _inventory.EquipB(0);
            _saveData.SetGlobalFlag(AgesGlobalFlag.FinishedGame, false);
            _saveData.SetRoomFlag(5, 0xe4, 0x20, obtained);
            _saveData.SetRoomFlag(5, 0xe4, 0x40);
            _inventory.LoseTreasure(TreasureId.FairyPowder);
            LoadValidationRoom(5, 0xe4);
            void Step(int count=1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            Step(3);
            var keeper = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x52 && n.Record.SubId == 2);
            var secret = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x52 && n.Record.SubId == 0);
            var powder = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x80);
            FailIf(secret.Active || !keeper.Active || powder.Active == obtained, "Room 5:e4 keeper/secret/decorative powder gates differ from source.");
            OpenLibraryKeeperPath(batched);
            ApproachLibraryActor(keeper, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            FailIf(!_dialogue.IsOpen || !_dialogue.CurrentMessage.Contains(obtained ? "I no longer have" : "You have done", StringComparison.OrdinalIgnoreCase),
                "Room 5:e4 keeper did not select TX_330b/$330d.");
            if (!obtained)
            {
                FailIf(_inventory.HasTreasure(TreasureId.FairyPowder) || _saveData.HasRoomFlag(5,0xe4,0x20), "Powder was granted before TX_330b closed.");
                _dialogue.Close(); Step();
                var host = _entities.EntityAdapters<LibraryActorRoomEntity>().Single(e => e.Npc == keeper).Host!;
                FailIf(host.Counter != 30, "Room 5:e4 upward animation must begin wait 30 in the same update.");
                Step(29); FailIf(_saveData.HasRoomFlag(5,0xe4,0x20) || host.Counter != 1, "Fairy powder room flag $20 was set early.");
                Step(); FailIf(!_saveData.HasRoomFlag(5,0xe4,0x20), "Fairy powder flag $20 missed the zero update.");
                Step(); FailIf(host.Counter != 30 || powder.Active, "Powder decoration did not delete at flag $20 or downward wait changed.");
                Step(29); FailIf(_inventory.HasTreasure(TreasureId.FairyPowder), "Fairy powder reward was early.");
                Step();
                for (int i=0; !_dialogue.IsOpen && i<40; i++) Step();
                FailIf(!_inventory.HasTreasure(TreasureId.FairyPowder) || !_dialogue.IsOpen, "Fairy powder native reward handoff failed.");
                _dialogue.Close();
                for (int i=0; !_dialogue.IsOpen && i<12; i++) Step();
                FailIf(!_dialogue.IsOpen || !_dialogue.CurrentMessage.Contains("Continue on", StringComparison.Ordinal), "Fairy powder follow-up TX_330c was lost.");
            }
            _dialogue.Close();
            for (int i=0; (_rooms.ActiveGroup!=5 || _rooms.CurrentRoom.Id!=0xec || _transitions.IsTransitioning) && i<220; i++) Step();
            // Begin the transition on the first post-text update.
            if (_rooms.CurrentRoom.Id==0xe4) { Step(); for(int i=0;_transitions.IsTransitioning&&i<220;i++)Step(); }
            FailIf(_rooms.ActiveGroup!=5 || _rooms.CurrentRoom.Id!=0xec || _transitions.IsTransitioning || _player.CutsceneControlled ||
                _inventory.HasTreasure(TreasureId.FairyPowder)==obtained,
                "Room 5:e4 must warp to 5:ec/$17, release input, and grant powder only on the first branch.");
            LoadValidationRoom(5,0xe4); Step(3); OpenLibraryKeeperPath(batched); ApproachLibraryActor(_entities.Entities<NpcCharacter>().Single(n=>n.Record.Id==0x52&&n.Record.SubId==2),batched);
            StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],batched);
            FailIf(!_dialogue.IsOpen || !_dialogue.CurrentMessage.Contains("I no longer have",StringComparison.OrdinalIgnoreCase),"Room 5:e4 repeated keeper conversation did not use completed branch.");
            _dialogue.Close(); LoadValidationRoom(5,0xec);
            FailIf(_player.CutsceneControlled,"Cancelling the room 5:e4 completed dialogue leaked control.");
        }
        GD.Print("Validated room 5:e4 powder/secret gates, reachable and repeated keeper conversation, both exact 30-update waits, reward/decoration lifetime and library warp in split/batched gameplay.");
    }
    private void ValidateRoom5e4BookPaths()
    {
        byte[][] paths = [[0x99,0x9a,0x9b,0x8b,0x7b,0x7c], [0x6d,0x5d,0x5c,0x4c,0x3c,0x3d],
            [0x2c,0x2b,0x1b,0x1a,0x19], [0x28,0x27,0x26,0x25,0x15,0x14,0x13],
            [0x22,0x23,0x33,0x43,0x42,0x41,0x51,0x61], [0x72,0x82,0x83,0x84,0x74,0x75,0x65,0x66,0x67]];
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); _inventory.EquipA(0); _inventory.EquipB(0);
            _saveData.SetRoomFlag(5,0xe4,0x40,false); _saveData.SetRoomFlag(5,0xe4,0x20,false);
            _saveData.SetGlobalFlag(AgesGlobalFlag.FinishedGame,false);
            _inventory.LoseTreasure(TreasureId.BookOfSeals);
            LoadValidationRoom(5,0xe4);
            void Step(int count=1)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched);
            void Press()=>StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],batched);
            Step(3);
            var podiums=_entities.EntityAdapters<LibraryActorRoomEntity>().Where(e=>e.Npc.Record.Id==0xb4).ToArray();
            FailIf(!podiums.Select(e=>e.Npc.Record.SubId).SequenceEqual(new[]{0,5,4,3,2,1}),"Room 5:e4 podium allocation must retain source $00,$05,$04,$03,$02,$01 order.");
            var book=podiums[0];
            FailIf(book.Npc.Visible || podiums.Skip(1).Any(p => !p.Npc.Visible),
                "Empty podium $00 must remain invisible while child books publish their state-one poses.");
            ApproachLibraryActor(book.Npc,batched); Press();
            FailIf(!_dialogue.IsOpen || !_dialogue.CurrentMessage.Contains("It's a podium",StringComparison.Ordinal) || _saveData.HasRoomFlag(5,0xe4,0x40),"Missing Book of Seals did not select TX_1211 without committing flag $40.");
            _dialogue.Close();Step(2);
            _inventory.GiveTreasure(TreasureId.BookOfSeals,0);
            ApproachLibraryActor(book.Npc,batched);Press();
            FailIf(!_dialogue.IsOpen,"Book of Seals placement prompt was not reachable.");
            _dialogue.SubmitChoiceForValidation(1);Step(2);
            FailIf(_saveData.HasRoomFlag(5,0xe4,0x40)||!_inventory.HasTreasure(TreasureId.BookOfSeals),"Declining book placement consumed the book or set flag $40.");
            ApproachLibraryActor(book.Npc,batched);Press();_dialogue.SubmitChoiceForValidation(0);Step();
            FailIf(!_saveData.HasRoomFlag(5,0xe4,0x40)||!_inventory.HasTreasure(TreasureId.BookOfSeals),"Book placement must yield after flag $40 before consuming the book.");
            Step();
            FailIf(_inventory.HasTreasure(TreasureId.BookOfSeals)||book.Host!.State!=3||book.Host.Counter!=2||!_dialogue.IsOpen||!book.Npc.Visible,
                "Book placement failed to consume $55 and activate path $00 with counter $02.");
            for(int sub=0;sub<6;sub++)
            {
                var current=podiums.Single(e=>e.Npc.Record.SubId==sub);
                if(sub!=0){ApproachLibraryActor(current.Npc,batched);Press();}
                FailIf(!_dialogue.IsOpen||current.Host!.State!=3||_entities.RuntimeState.ReadWramByte(0xcfd0)!=sub,
                    $"Book ${sub:x2} did not publish its shared $cfd0 selection.");
                _dialogue.Close();
                for(int index=0;index<paths[sub].Length;index++)
                {
                    byte position=paths[sub][index];
                    Step();FailIf(_currentRoom.GetPackedStorageMetatile(position)==0xd3,$"Book ${sub:x2} wrote ${position:x2} before its two-update interval.");
                    FailIf(sub == 0 && _entities.RuntimeState.ReadWramByte(AgesWramAddress.wMenuDisabled) != 0x81,
                        "Initial book construction must retain its menu mask $81 until its terminator.");
                    Step();FailIf(_currentRoom.GetPackedStorageMetatile(position)!=0xd3,$"Book ${sub:x2} omitted source path tile ${position:x2}.");
                }
                Step();FailIf(current.Host!.State!=3,"Book path completed before the separate terminator interval.");
                Step();FailIf(current.Host!.State!=4,$"Book ${sub:x2} missed its zero terminator.");
                for(int i=0;_player.CutsceneControlled&&i<40;i++)Step();
                FailIf(_player.CutsceneControlled||_entities.RuntimeState.ReadWramByte(AgesWramAddress.wDisabledObjects)!=0,
                    "Completed book paths leaked their input/object mask.");
                if(sub!=0) FailIf(paths[sub-1].Any(p=>_currentRoom.GetPackedStorageMetatile(p)!=0xf4),"Previous book failed to remove its whole path after another book activated.");
                // An active book shows its directions again without replacing tiles.
                ApproachLibraryActor(current.Npc,batched);Press();
                FailIf(!_dialogue.IsOpen||current.Host!.State!=4,"Repeated active-book interaction restarted the path.");
                _dialogue.Close();Step(2);
            }
            LoadValidationRoom(5,0xe4);Step(3);
            FailIf(_entities.EntityAdapters<LibraryActorRoomEntity>().Single(e=>e.Npc.Record.Id==0xb4&&e.Npc.Record.SubId==0).Host!.State!=1 ||
                _inventory.HasTreasure(TreasureId.BookOfSeals),"Room 5:e4 re-entry lost completed book placement or duplicated $55.");
            var fresh=_entities.Entities<NpcCharacter>().Single(n=>n.Record.Id==0xb4&&n.Record.SubId==0);
            ApproachLibraryActor(fresh,batched);Press();_dialogue.Close();Step(3);
            LoadValidationRoom(5,0xec);
            FailIf(_player.CutsceneControlled||_entities.RuntimeState.ReadWramByte(AgesWramAddress.wMenuDisabled)!=0,"Cancelling an unfinished library path leaked controls.");
        }
        GD.Print("Validated room 5:e4 missing/declined/accepted Book of Seals, ordered child podium slots, six source-derived paths, exact tile/terminator counters, repeated book use, re-entry and cancellation in split/batched gameplay.");
    }
    private void ValidateLibraryCancellation()
    {
        foreach (bool batched in new[] { false, true })
        {
            foreach (bool flagWritten in new[] { false, true })
            {
                ReinitializeGameplayForValidation(); _inventory.EquipA(0); _inventory.EquipB(0);
                _saveData.SetGlobalFlag(AgesGlobalFlag.FinishedGame, false);
                _saveData.SetRoomFlag(5, 0xe4, 0x20, false);
                _saveData.SetRoomFlag(5, 0xe4, 0x40);
                _inventory.LoseTreasure(TreasureId.FairyPowder);
                LoadValidationRoom(5, 0xe4);
                StepGameplayUpdates(3, Vector2.Zero, batched: batched);
                OpenLibraryKeeperPath(batched);
                var keeper = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x52 && n.Record.SubId == 2);
                ApproachLibraryActor(keeper, batched);
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
                _dialogue.Close();
                StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                var host = _entities.EntityAdapters<LibraryActorRoomEntity>().Single(e => e.Npc == keeper).Host!;
                FailIf(host.Counter != 30, "Powder interaction-mask fixture did not initialize wait $1e.");
                _entities.RuntimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0x83);
                StepGameplayUpdates(5, Vector2.Zero, batched: batched);
                FailIf(host.Counter != 30, "Old man $52:$02 must freeze when wDisabledObjects bit $02 is set.");
                _entities.RuntimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0x81);
                StepGameplayUpdates(flagWritten ? 32 : 11, Vector2.Zero, batched: batched);
                FailIf(_saveData.HasRoomFlag(5, 0xe4, 0x20) != flagWritten || !_player.CutsceneControlled,
                    "Powder cancellation fixture missed its source flag $20 boundary or input hold.");
                LoadValidationRoom(5, 0xec);
                StepGameplayUpdates(80, Vector2.Zero, batched: batched);
                FailIf(_player.CutsceneControlled || _inventory.HasTreasure(TreasureId.FairyPowder) ||
                    _saveData.HasRoomFlag(5, 0xe4, 0x20) != flagWritten ||
                    _entities.RuntimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != 0 ||
                    _entities.RuntimeState.ReadWramByte(AgesWramAddress.wMenuDisabled) != 0,
                    "Cancelling a powder wait leaked controls/reward or rolled back an already-written room flag $20.");
            }
            ReinitializeGameplayForValidation(); _inventory.EquipA(0); _inventory.EquipB(0);
            _saveData.SetGlobalFlag(AgesGlobalFlag.FinishedGame);
            _saveData.SetGlobalFlag(0x68, false); _saveData.SetGlobalFlag(0x72, false);
            _saveData.SetRoomFlag(5, 0xe4, 0x20); _saveData.SetRoomFlag(5, 0xe4, 0x40);
            _inventory.GiveTreasure(TreasureId.Shield, 1);
            LoadValidationRoom(5, 0xe4);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            OpenLibraryKeeperPath(batched);
            var secretKeeper = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x52 && n.Record.SubId == 0);
            ApproachLibraryActor(secretKeeper, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            _dialogue.SubmitChoiceForValidation(0);
            for (int i = 0; !_secretEntry.IsActive && i < 40; i++) StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(!_secretEntry.IsActive, "Library cancellation fixture did not acquire MENU_SECRET $04.");
            for (int i = 0; i < 22; i++) _secretEntry.Update(1.0 / 60);
            _secretEntry.Submit(new LinkedGameNpcDatabase().GenerateSecretValues(4, _saveData));
            for (int i = 0; i < 22; i++) _secretEntry.Update(1.0 / 60);
            for (int i = 0; !_transitions.NativePaletteFadeActive && i < 200; i++)
            {
                if (_dialogue.IsOpen) _dialogue.Close();
                StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            }
            FailIf(!_transitions.NativePaletteFadeActive || !_saveData.HasGlobalFlag(0x68) || _saveData.HasGlobalFlag(0x72),
                "Library cancellation fixture missed the energy swirl fade before DONE $72.");
            LoadValidationRoom(5, 0xec);
            StepGameplayUpdates(100, Vector2.Zero, batched: batched);
            FailIf(_transitions.NativePaletteFadeActive || _player.CutsceneControlled || _gameplayPause.IsLeased ||
                !_saveData.HasGlobalFlag(0x68) || _saveData.HasGlobalFlag(0x72) || _inventory.ShieldLevel != 1 ||
                _entities.RuntimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != 0 ||
                _entities.RuntimeState.ReadWramByte(AgesWramAddress.wMenuDisabled) != 0,
                "Cancelling the library energy swirl leaked a fade/control lease, granted a shield, or changed $68/$72.");
        }
        GD.Print("Validated library cancellation before/after powder flag $20 and during the secret energy fade, retaining committed flags and releasing masks without delayed rewards in split/batched gameplay.");
    }
    private void ValidateLibraryTransitionInitialization()
    {
        foreach (bool finished in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(AgesGlobalFlag.FinishedGame, finished);
            _saveData.SetRoomFlag(5, 0xe4, 0x20, finished);
            _saveData.SetRoomFlag(5, 0xe4, 0x40, false);
            LoadValidationRoom(5, 0xec);
            _entities.RuntimeState.SetWramByte(0xcfd0, 0xff);
            _entities.BeginScreenTransition(5, _world.LoadRoom(5, 0xe4), new(240, 0), _player);
            var incoming = _entities.Entities<NpcCharacter>();
            FailIf(incoming.Single(n => n.Record.Id == 0x52 && n.Record.SubId == 0).Visible != finished ||
                incoming.Single(n => n.Record.Id == 0x52 && n.Record.SubId == 2).Visible == finished ||
                incoming.Single(n => n.Record.Id == 0x80).Visible == finished ||
                incoming.Count(n => n.Record.Id == 0xb4) != 1 || incoming.Single(n => n.Record.Id == 0xb4).Visible ||
                _entities.RuntimeState.ReadWramByte(0xcfd0) != 0,
                "Room 5:e4 preload did not resolve deletion/visibility, clear $cfd0, or defer child podium creation.");
            LoadValidationRoom(5,0xe4);
            _saveData.SetGlobalFlag(0x30, finished);
            _entities.BeginScreenTransition(5, _world.LoadRoom(5, 0xec), new(240, 0), _player);
            var readers=_entities.Entities<NpcCharacter>().Where(n=>n.Record.Id==0x52).ToArray();
            FailIf(readers.Length!=4||readers.Any(n=>!n.Visible||n.TextId!=0x3300+(n.Record.SubId-3)*2+(finished?1:0)),
                "Room 5:ec preload exposed an unresolved library pose or pollution text.");
            LoadValidationRoom(5, 0xec);
            _saveData.SetRoomFlag(5, 0xe4, 0x40);
            _entities.BeginScreenTransition(5, _world.LoadRoom(5, 0xe4), new(240, 0), _player);
            var book = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0xb4);
            FailIf(book.Visible, "Podium state zero must remain hidden even after book placement.");
            _entities.Update(1.0 / 60.0, _player);
            FailIf(!book.Visible || _entities.Entities<NpcCharacter>().Count(n => n.Record.Id == 0xb4) != 1,
                $"The podium always bit must publish its state-one pose during scrolling without allocating children: visible={book.Visible}, count={_entities.Entities<NpcCharacter>().Count(n => n.Record.Id == 0xb4)}.");
        }
        GD.Print("Validated library destination state-zero poses/deletion, pollution snapshots, shared scratch reset and deferred podium children during scrolling preload.");
    }
    private void ValidateRoom5e4LibrarySecret()
    {
        foreach(bool batched in new[]{false,true})
        foreach(int level in new[]{0,1,2,3})
        {
            ReinitializeGameplayForValidation();_inventory.EquipA(0);_inventory.EquipB(0);
            if(level==0)_inventory.LoseTreasure(TreasureId.Shield);else _inventory.GiveTreasure(TreasureId.Shield,level);
            _saveData.SetGlobalFlag(AgesGlobalFlag.FinishedGame);
            _saveData.SetGlobalFlag(0x68,false);_saveData.SetGlobalFlag(0x72,false);
            _saveData.SetRoomFlag(5,0xe4,0x20);_saveData.SetRoomFlag(5,0xe4,0x40);
            LoadValidationRoom(5,0xe4);
            void Step(int count=1)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched);
            void Press()=>StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],batched);
            Step(3);
            NpcCharacter Actor()=>_entities.Entities<NpcCharacter>().Single(n=>n.Record.Id==0x52&&n.Record.SubId==0);
            var keeper=Actor();
            FailIf(!keeper.Active||_entities.Entities<NpcCharacter>().Single(n=>n.Record.Id==0x52&&n.Record.SubId==2).Active,
                "Room 5:e4 postgame must retain only the $52:$00 secret keeper.");
            OpenLibraryKeeperPath(batched);
            ApproachLibraryActor(keeper,batched);Press();
            FailIf(!_dialogue.ChoiceActive,"Library postgame TX_3310 choice was lost.");
            _dialogue.SubmitChoiceForValidation(1);Step();
            var host=_entities.EntityAdapters<LibraryActorRoomEntity>().Single(e=>e.Npc==keeper).Host!;
            FailIf(host.Counter!=30,"Library secret prompt did not initialize its 30-update wait after text.");
            Step(29);FailIf(_dialogue.IsOpen||host.Counter!=1,"Library secret refusal text was early.");Step();
            FailIf(!_dialogue.IsOpen||_dialogue.CurrentMessage!="Then be off!"||_saveData.HasGlobalFlag(0x68),"Refusing library secret did not use TX_3311 without setting BEGAN $68.");
            _dialogue.Close();
            for(int i=0;_rooms.CurrentRoom.Id!=0xec||_transitions.IsTransitioning;i++)
            {if(i>=250)throw new InvalidOperationException("Library refusal warp stalled.");Step();}
            LoadValidationRoom(5,0xe4);Step(3);OpenLibraryKeeperPath(batched);ApproachLibraryActor(Actor(),batched);Press();
            _dialogue.SubmitChoiceForValidation(0);
            for(int i=0;!_secretEntry.IsActive&&i<40;i++)Step();
            FailIf(!_secretEntry.IsActive||!_gameplayPause.IsLeased,"Library secret did not enter the shared MENU_SECRET lifecycle.");
            for(int i=0;i<22;i++)_secretEntry.Update(1.0/60);
            _secretEntry.Submit([0xff,0xff,0xff,0xff,0xff]);
            FailIf(!_secretEntry.IsActive||_saveData.HasGlobalFlag(0x68),"Invalid library secret advanced its quest.");
            _secretEntry.Submit(new LinkedGameNpcDatabase().GenerateSecretValues(4,_saveData));
            for(int i=0;i<22;i++)_secretEntry.Update(1.0/60);
            FailIf(_secretEntry.IsActive||_gameplayPause.IsLeased,"Valid library secret failed to release its menu lease.");
            for(int i=0;!_saveData.HasGlobalFlag(0x72)&&i<850;i++){if(_dialogue.IsOpen)_dialogue.Close();Step();}
            int expected=level==3?3:level+1;
            FailIf(!_saveData.HasGlobalFlag(0x68)||!_saveData.HasGlobalFlag(0x72)||_inventory.ShieldLevel!=expected,
                $"Library secret did not persist $68/$72 and upgrade shield level {level} to {expected}: actual={_inventory.ShieldLevel}, began={_saveData.HasGlobalFlag(0x68)}, done={_saveData.HasGlobalFlag(0x72)}.");
            for(int i=0;!_dialogue.IsOpen&&i<8;i++)Step();
            FailIf(!_dialogue.IsOpen||_dialogue.CurrentMessage.Contains("\\secret1",StringComparison.Ordinal)||_saveData.ReadWramByte(AgesWramAddress.wShortSecretIndex)!=0x14,
                "Library TX_3313 did not substitute the $14 return secret.");
            _dialogue.Close();
            for(int i=0;(_rooms.CurrentRoom.Id!=0xec||_transitions.IsTransitioning)&&i<250;i++)Step();
            FailIf(_rooms.CurrentRoom.Id!=0xec||_player.CutsceneControlled,"Library successful secret did not warp/release Link.");
            LoadValidationRoom(5,0xe4);Step(3);OpenLibraryKeeperPath(batched);ApproachLibraryActor(Actor(),batched);Press();Step();
            FailIf(!_dialogue.IsOpen||_dialogue.ChoiceActive||_inventory.ShieldLevel!=expected||_secretEntry.IsActive,
                "Completed library secret must regenerate TX_3314 without prompting or upgrading again.");
            _dialogue.Close();LoadValidationRoom(5,0xec);
            FailIf(_player.CutsceneControlled||_transitions.NativePaletteFadeActive,"Library secret cancellation leaked input or palette fade.");
        }
        GD.Print("Validated room 5:e4 postgame keeper gates, refusal warp, shared secret menu/invalid input, all shield upgrade branches, BEGAN/DONE and $14 return secret, repeat/cancel in split/batched gameplay.");
    }
}
