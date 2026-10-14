using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePirateShipBoarding()
    {
        foreach (bool linked in new[] { false, true })
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.WriteWramByte(WramAddress.wFileIsLinkedGame, linked ? (byte)1 : (byte)0);
            _inventory.GiveTreasure(TreasureId.Flippers, 0);
            _inventory.GiveTreasure(TreasureId.MermaidSuit, 0);
            _saveData.WriteWramByte(0xc6ec, 0xd6);
            _saveData.WriteWramByte(0xc6ed, 0x18);
            _saveData.WriteWramByte(0xc6ee, 0x48);
            _saveData.WriteWramByte(0xc6ef, 1);
            LoadValidationRoom(linked ? 0 : 1, 0xd6);
            _player.WarpTo(new(0x78, 0x18));
            FailIf(_currentRoom.GetTerrainInfo(_player.Position).Hazard != HazardType.Water,
                $"Pirate boarding fixture must start in swimmable Sea of Storms water: {_currentRoom.GetTerrainInfo(_player.Position)}.");
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            var ship = _entities.EntityAdapters<PirateShipRoomEntity>().Single();
            FailIf(_entities.InteractionSlot(ship.Node) != 1 || !ship.Node.Visible ||
                ((NpcCharacter)ship.Node).CurrentAnimationOpaquePixels == 0,
                "$c2:$00 did not render in reserved interaction $d1.");
            for (int update = 0; !IsTransitioning && update < 90; update++)
                StepGameplayUpdates(1, Vector2.Left, ["move_left"], ["move_left"], batched);
            FailIf(!IsTransitioning, $"Swimming into pirate ship did not board from actual sea geometry: {_player.Position}, ship={ship.Node.Position}.");
            for (int update = 0; IsTransitioning && update < 500; update++)
                StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(IsTransitioning || _activeGroup != 5 || _currentRoom.Id != 0xf8 ||
                _player.Position != new Vector2(0x68, 0x58),
                "$c2 boarding did not load source warp $5:f8/$56/transition $03.");
            FailIf(_entities.Entities<NpcCharacter>().Count(n => n.Record.Id is 0xc3 or 0xc4) != 5,
                "Boarding omitted captain or crew in $5:f8.");
        }
        // checkLoadPirateShip gates outdoors, underwater, era and gone flag.
        foreach (var fixture in new[] { (Group:0,Room:0xd6,Linked:false,Gone:false),
            (Group:1,Room:0xd6,Linked:true,Gone:false), (Group:3,Room:0xd6,Linked:false,Gone:false),
            (Group:1,Room:0xd6,Linked:false,Gone:true) })
        {
            ReinitializeGameplayForValidation();
            _saveData.WriteWramByte(WramAddress.wFileIsLinkedGame, fixture.Linked ? (byte)1 : (byte)0);
            _saveData.SetGlobalFlag(0x34, fixture.Gone);
            _saveData.WriteWramByte(0xc6ec, (byte)fixture.Room);
            LoadValidationRoom(fixture.Group, fixture.Room);
            StepGameplayUpdates(3, Vector2.Zero);
            FailIf(_entities.EntityAdapters<PirateShipRoomEntity>().Any(), "Pirate ship ignored source spawn gate.");
        }
        GD.Print("Validated visible pirate ship, reserved slot, real sea boarding, both eras and spawn gates.");
    }

    private void ValidatePirateCaptainExchange()
    {
        foreach (bool linked in new[] { false, true })
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.WriteWramByte(WramAddress.wFileIsLinkedGame, linked ? (byte)1 : (byte)0);
            _inventory.GiveTreasure(TreasureId.ZoraScale, 0);
            _dialogue.MessageSpeed = 4;
            LoadValidationRoom(5, 0xf8);
            _player.WarpTo(new(0x68, 0x58));
            var captain = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0xc3);
            var story = _roomEvents.Get<PirateCaptainEvent>();
            void Step(int count = 1, string? button = null) => StepGameplayUpdates(count, Vector2.Zero,
                button is null ? [] : [button], button is null ? [] : [button], batched);
            void Pump(Func<bool> done)
            {
                for (int update = 0; !done() && update < 4000; update++) Step(button: update % 2 == 0 ? "attack" : null);
                FailIf(!done(), $"Pirate captain did not reach text/script boundary: command={story.CurrentCommandIndex}, text={_dialogue.CurrentMessage}.");
            }
            Step(3);
            // roomsInAltWorldGroup5 marks $f8 as past already. $c3 only
            // ORs bit 7 for unlinked games; linked games do not clear it.
            FailIf(_currentRoom.TilesetFlags != 0x90,
                $"$c3 native initialization failed its unlinked past tileset mark: linked={linked}, batched={batched}, flags=${_currentRoom.TilesetFlags:x2}.");
            ApproachPirateActor(captain, batched);
            FailIf(!captain.CanTalkTo(_player) || captain.BlocksLinkCenter(_player.Position),
                "Captain interaction requires standing inside his hitbox.");
            void OpenOffer()
            {
                Step(button: "attack"); Step();
                FailIf(!_dialogue.ChoiceActive || !_player.CutsceneControlled ||
                    !_dialogue.CurrentMessage.Contains("Zora's Scale") ||
                    !_dialogue.CurrentMessage.StartsWith(linked ? "Ahoy!" : "Sailin'"),
                    "Captain lost source linked/unlinked prompt or choice/input ownership.");
                Pump(() => _dialogue.ChoiceCursorVisible);
            }
            OpenOffer();
            Step(button: "move_right"); Step(); Step(button: "attack");
            Pump(() => !_dialogue.IsOpen); Step(3);
            FailIf(_player.CutsceneControlled || !_inventory.HasTreasure(TreasureId.ZoraScale) ||
                _inventory.HasTreasure(TreasureId.TokayEyeball) || _runtimeState.ReadWramByte(0xcfc0) != 0,
                "Captain refusal changed rewards/signals or failed to release input.");
            OpenOffer();
            Step(); Step(button: "attack");
            Pump(() => _dialogue.IsOpen && _dialogue.CurrentMessage.StartsWith("Thank'ee!"));
            FailIf(!_inventory.HasTreasure(TreasureId.ZoraScale), "Captain took Zora Scale before crew signal.");
            Pump(() => !_dialogue.IsOpen);
            for (int update = 0; (_runtimeState.ReadWramByte(0xcfc0) & 1) == 0 && update < 5; update++) Step();
            var crew = _entities.EntityAdapters<PirateCrewRoomEntity>().ToArray();
            FailIf(crew.Length != 4 || crew.Any(c => c.State != 2) || (_runtimeState.ReadWramByte(0xcfc0) & 2) != 0,
                "Captain XOR bit 0 did not reach all four later crew slots in this update.");
            Step(26);
            FailIf((_runtimeState.ReadWramByte(0xcfc0) & 2) != 0 ||
                _entities.Entities<NpcCharacter>().Where(n => n.Record.Id == 0xc4).Any(n => n.ScriptDrawOffset.Y >= 0),
                "Pirate crew -$0200/gravity $28 landed before native update 27.");
            Step();
            FailIf((_runtimeState.ReadWramByte(0xcfc0) & 2) == 0 ||
                _entities.Entities<NpcCharacter>().Where(n => n.Record.Id == 0xc4).Any(n => n.ScriptDrawOffset.Y != 0),
                "Pirate crew failed to land and set shared bit 1 on update 27.");
            Step(33);
            FailIf(_dialogue.IsOpen || story.Counter != 1 || !_inventory.HasTreasure(TreasureId.ZoraScale),
                "Captain's source wait 60 resumed before the terminal update.");
            Step();
            FailIf(!_dialogue.IsOpen || _dialogue.CurrentMessage != "Aye, aye, sir!",
                "Captain did not show TX_3607 on wait 60's terminal update.");
            Pump(() => _dialogue.IsOpen && _dialogue.CurrentMessage.Contains("Tokay"));
            FailIf(_inventory.HasTreasure(TreasureId.ZoraScale) || _inventory.HasTreasure(TreasureId.TokayEyeball),
                "Captain reward explanation lost source treasure ordering.");
            Pump(() => _inventory.HasTreasure(TreasureId.TokayEyeball));
            FailIf(_saveData.HasGlobalFlag(0x34), "Captain marked pirates gone before reward presentation/wait.");
            Pump(() => IsTransitioning);
            FailIf(!_saveData.HasGlobalFlag(0x34), "Captain warp omitted GLOBALFLAG_PIRATES_GONE $34.");
            for (int update = 0; IsTransitioning && update < 500; update++) Step();
            FailIf(IsTransitioning || _activeGroup != (linked ? 0 : 1) || _currentRoom.Id != (linked ? 0xc8 : 0xd7),
                "Captain did not select source linked/unlinked warp.");
            Pump(() => _dialogue.IsOpen);
            FailIf(!_dialogue.CurrentMessage.Contains("Ye've really") || !_dialogue.CurrentMessage.Contains("We're off, now!"), "Pirate departure omitted TX_360c.");
            Pump(() => !_dialogue.IsOpen);
            Pump(() => !_entities.EntityAdapters<PirateShipRoomEntity>().Any());
            FailIf(_player.CutsceneControlled || !_saveData.HasRoomFlag(linked ? 0 : 1, linked ? 0xc8 : 0xd7, 0x40),
                "Ship departure failed to release input or persist room bit $40.");
            LoadValidationRoom(linked ? 0 : 1, linked ? 0xc8 : 0xd7); Step(4);
            FailIf(_entities.EntityAdapters<PirateShipRoomEntity>().Any(), "Completed departure repeated on re-entry.");
        }
        GD.Print("Validated captain refusal/retry, era dialogue, crew slot ordering and native jump, Scale/Eyeball handoff, both warps and departure persistence.");
    }

    private void ValidatePirateShipDeparture()
    {
        foreach (bool linked in new[] { false, true })
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.WriteWramByte(WramAddress.wFileIsLinkedGame, linked ? (byte)1 : (byte)0);
            _saveData.SetGlobalFlag(0x34);
            int group = linked ? 0 : 1, room = linked ? 0xc8 : 0xd7;
            LoadValidationRoom(group, room);
            _player.WarpTo(linked ? new(0x28, 0x58) : new(0x58, 0x48));
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            var ship = _entities.EntityAdapters<PirateShipRoomEntity>().Single();
            FailIf(ship.State != 1 || ship.Counter != 60 || !_player.CutsceneControlled,
                "Pirate departure initialization consumed its 60-update counter.");
            Vector2 initial = ship.Node.Position, link = _player.Position;
            StepGameplayUpdates(59, Vector2.Down, batched: batched);
            FailIf(_dialogue.IsOpen || ship.Counter != 1 || _player.Position != link || ship.Node.Position != initial,
                "Pirate departure moved or opened text before wait 60 expired.");
            StepGameplayUpdates(1, Vector2.Down, batched: batched);
            FailIf(!_dialogue.IsOpen || ship.State != 2 || ship.Counter != 128,
                "Pirate departure did not open text and load $80 on update 60.");
            StepGameplayUpdates(5, Vector2.Down, batched: batched);
            FailIf(ship.Counter != 128 || ship.Node.Position != initial, "Departure moved during active text.");
            _dialogue.Close();
            StepGameplayUpdates(127, Vector2.Down, batched: batched);
            Vector2 destination = initial + (linked ? Vector2.Up : Vector2.Left) * 127;
            destination = new((int)destination.X & 255, (int)destination.Y & 255);
            destination = new(destination.X >= 0xe0 ? destination.X - 256 : destination.X,
                destination.Y >= 0xe0 ? destination.Y - 256 : destination.Y);
            FailIf(ship.Counter != 1 || ship.Node.Position != destination ||
                _saveData.HasRoomFlag(group, room, 0x40) || _player.Position != link,
                $"Pirate departure lost SPEED_100 or its exact 128-update lifetime: counter={ship.Counter}, position={ship.Node.Position}/{destination}, Link={_player.Position}/{link}.");
            StepGameplayUpdates(1, Vector2.Down, batched: batched);
            FailIf(!_saveData.HasRoomFlag(group, room, 0x40) || _player.CutsceneControlled,
                "Pirate departure did not finish on update 128.");
            _saveData.SetRoomFlag(group, room, 0x40, false);
            LoadValidationRoom(group, room);
            ship = _entities.EntityAdapters<PirateShipRoomEntity>().Single();
            var presentation = ((IScreenTransitionPreloadRoomEntity)ship).PrepareForScreenTransition(_player,
                new System.Collections.Generic.List<RoomEntitySpawn>());
            FailIf(presentation != ScreenTransitionPresentation.Visible || ship.State != 1 || ship.Counter != 60,
                "Pirate departure preload did not perform state 0 without consuming its counter.");
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(ship.Counter != 59, "Preloaded pirate departure repeated its state 0 on the first eligible update.");
            LoadValidationRoom(0, 0x47); StepGameplayUpdates(2, Vector2.Zero);
            FailIf(_player.CutsceneControlled || _entities.RoomEntityFreezeActive(), "Cancelling pirate departure leaked input/object ownership.");
        }
        GD.Print("Validated both ship departures at 60/128 updates, dialogue freeze, movement masks, persistence and cancellation.");
    }

    private void ApproachPirateActor(NpcCharacter actor, bool batched)
    {
        foreach (int distance in new[] { 40, 32, 24 })
        foreach (Vector2I direction in new[] { Vector2I.Up, Vector2I.Left, Vector2I.Down, Vector2I.Right })
        {
            Vector2 start = actor.Position - (Vector2)direction * distance;
            if (start.X < 8 || start.Y < 8 || start.X >= _currentRoom.Width - 8 || start.Y >= _currentRoom.Height - 8 ||
                _collision.Collides(start) || _entities.BlocksLink(start) || _currentRoom.GetTerrainInfo(start).Collision != 0) continue;
            _player.WarpTo(start); _player.Face(direction);
            string action = direction == Vector2I.Up ? "move_up" : direction == Vector2I.Down ? "move_down" : direction == Vector2I.Left ? "move_left" : "move_right";
            for (int tick = 0; !actor.CanTalkTo(_player) && tick < 80; tick++)
                StepGameplayUpdates(1, direction, [action], [action], batched);
            if (actor.CanTalkTo(_player) && !_collision.Collides(_player.Position) && !_entities.BlocksLink(_player.Position)) return;
        }
        throw new InvalidOperationException($"Pirate ${actor.Record.Id:x2}:${actor.Record.SubId:x2} is unreachable through $5:f8 collision geometry.");
    }

    private void ValidatePirateCrewDialogueAndCancellation()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.ZoraScale, 0);
            LoadValidationRoom(5, 0xf8);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            string[] expected = ["'Ow long have we\nbeen adrift?", "thinkin' about a\nthing.", "giant ruins out", "ye can never\nreturn."];
            foreach (var actor in _entities.Entities<NpcCharacter>().Where(n => n.Record.Id == 0xc4).OrderBy(n => n.Record.SubId))
            {
                ApproachPirateActor(actor, batched);
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
                    FailIf(!_dialogue.IsOpen || !_dialogue.CurrentMessage.Contains(expected[actor.Record.SubId]),
                        $"Pirate $c4:${actor.Record.SubId:x2} omitted its TX_${0x3608 + actor.Record.SubId:x4} generic loop.");
                    _dialogue.Close(); StepGameplayUpdates(3, Vector2.Zero, batched: batched);
                }
            }
            var captain = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0xc3);
            ApproachPirateActor(captain, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            FailIf(!_player.CutsceneControlled || !_dialogue.ChoiceActive, "Captain cancellation did not begin a conversation.");
            LoadValidationRoom(0, 0x47); StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            FailIf(_player.CutsceneControlled || _roomEvents.Active || !_inventory.HasTreasure(TreasureId.ZoraScale) ||
                _inventory.HasTreasure(TreasureId.TokayEyeball) || _saveData.HasGlobalFlag(0x34),
                "Cancelling captain's offer leaked input or changed persistent treasures/flags.");
            LoadValidationRoom(5, 0xf8); StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            captain = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0xc3);
            ApproachPirateActor(captain, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            FailIf(!_dialogue.ChoiceActive, "Captain did not restart after cancellation and re-entry.");
        }
        GD.Print("Validated all four crew conversations through collision geometry, repeat interaction and captain cancellation/re-entry.");
    }
}
