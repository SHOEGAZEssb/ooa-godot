using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateRoomObjectLoadLifetimeRom()
    {
        var policy = NativeObjectStreamDatabase.For(AgesGameContent.AssetRoot);
        var native = new FrontendRom();
        native[0xcc05] = 0x35;
        native.InitializeRoomObjectLoadFlags();
        FailIf(native[0xcc05] != 0xff || OracleRuntimeState.CreateInitialized(OracleGame.Ages).ReadField("wcc05") != native[0xcc05],
            "initializeGame must initialize Ages wcc05=$ff from the native writer.");
        var memory = OracleRuntimeState.CreateInitialized(OracleGame.Ages);
        foreach (int mode in new[] { 0, 1, 2, 8 })
        for (int flags = 0; flags <= 0xff; flags++)
        {
            native[0xcc05] = (byte)flags; native[0xcd00] = (byte)mode;
            memory.SetField("wcc05", (byte)flags); memory.SetField("wScrollMode", (byte)mode);
            native.CompleteRoomObjectLoadFlags();
            policy.CompleteRoomInitialization(memory);
            int expected = mode == 1 && (flags & 0x80) != 0 ? 0xff : flags;
            FailIf(native[0xcc05] != expected || memory.ReadField("wcc05") != native[0xcc05],
                $"cutscene00 mode${mode:x2}/wcc05=${flags:x2}: completion must preserve bit-7-clear masks and wait for scroll mode $01.");
        }

        foreach (bool seeds in new[] { false, true })
        {
            var save = OracleSaveData.CreateStandardGame(OracleGame.Ages);
            save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);
            if (seeds) _inventory.GiveTreasure(0x24, 0);
            LoadValidationRoom(1, 0x81);
            var roomEvent = _roomEvents.Get<DekuForestSoldierEvent>();
            var data = new DekuForestSoldierEventDatabase();
            foreach (bool completed in new[] { false, true })
            foreach (bool full in new[] { false, true })
            foreach (byte flags in new byte[] { 0, 0x7f, 0xff })
            {
                roomEvent.Cancel();
                _entities.Clear();
                save.SetRoomFlag(1, 0x81, 0x40, completed);
                _runtimeState.SetField("wcc05", flags);
                _runtimeState.SetField("wDiggingUpEnemiesForbidden", 0);
                native = new FrontendRom();
                for (int address = 0xc5b0; address < 0xcb00; address++) native[address] = save.ReadWramByte(address);
                native[0xcc2d] = 1; native[0xcc30] = 0x81; native[0xcc05] = flags;
                if (full)
                    for (int slot = 2; slot < 16; slot++)
                    {
                        native[0xd040 + slot * 256] = 1;
                        _entities.Spawn<NpcCharacter>(new CutsceneNpcSpawn(data.CreateSoldierRecord(), $"Occupied${slot:x2}"));
                    }
                native.Call(0x5872, 0x12); // Actual runRoomSpecificCode group/room dispatch.
                roomEvent.Start(_rooms.CurrentRoom);
                bool allocated = seeds && !completed && !full;
                FailIf(_runtimeState.ReadField("wcc05") != native[0xcc05] ||
                    _runtimeState.ReadField("wDiggingUpEnemiesForbidden") != native[0xccde] ||
                    roomEvent.HasState != allocated ||
                    _entities.Entities<NpcCharacter>().Count != (full ? 14 : allocated ? 1 : 0) ||
                    allocated && (native[0xd240] != 1 || native[0xd241] != 0x40 || native[0xd242] != 0x0a) ||
                    native.RandomCalls != 0,
                    $"roomSpecificCode3 seeds={seeds}/flag40={completed}/full={full}/wcc05=${flags:x2}: predicate, allocation or pre-allocation side effect differs.");
                byte afterStart = _runtimeState.ReadField("wcc05");
                roomEvent.Cancel();
                FailIf(roomEvent.HasState || _runtimeState.ReadField("wcc05") != afterStart,
                    "Cancelling soldier $40:$0a must not manufacture its script-completion gate write.");
            }
        }

        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(GlobalFlag.IntroDone);
            _inventory.GiveTreasure(0x24, 0);
            _saveData.SetRoomFlag(1, 0x81, 0x40, false);
            _runtimeState.SetField("wcc05", 0xff);
            LoadValidationRoom(1, 0x81);
            FailIf(_runtimeState.ReadField("wcc05") != 0xfd ||
                _runtimeState.ReadField("wDiggingUpEnemiesForbidden") != 1,
                "The real room lifecycle must retain roomSpecificCode3 writes before its first object update.");
            _player.WarpTo(new Vector2(0x58, 0x2b), recordSafe: false);
            native = new FrontendRom(); native[0xcc05] = 0xfd; native[0xcd00] = 1;
            StepGameplayUpdates(3, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                native.CompleteRoomObjectLoadFlags();
                FailIf(_runtimeState.ReadField("wcc05") != native[0xcc05] ||
                    !_roomEvents.Get<DekuForestSoldierEvent>().HasState ||
                    _runtimeState.ReadField("wDiggingUpEnemiesForbidden") != 1,
                    $"Soldier room entry lost its native post-object gate reset or pre-stream actor (batch={batched}).");
            });
            _runtimeState.SetField("wcc05", 0x7d);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            FailIf(_runtimeState.ReadField("wcc05") != 0x7d,
                "Ordinary gameplay must retain a bit-7-clear object-load mask.");
            void ApproachSoldierTrigger()
            {
                _player.WarpTo(new Vector2(0x58, 0x30), recordSafe: false);
                FailIf(_collision.Collides(_player.Position) || _collision.Collides(new Vector2(0x58, 0x2a)),
                    "The soldier regression's approach must follow the room's actual open entrance geometry.");
                StepGameplayUpdates(10, Vector2.Up, batched: batched);
                FailIf(!_roomEvents.Get<DekuForestSoldierEvent>().BlocksGameplay || !_player.CutsceneControlled,
                    "Walking through the room's native y=$2a trigger did not start the soldier escort.");
            }
            ApproachSoldierTrigger();
            LoadValidationRoom(0, 0x45);
            FailIf(_roomEvents.Get<DekuForestSoldierEvent>().HasState || _player.CutsceneControlled ||
                _runtimeState.ReadField("wcc05") != 0x7d || _saveData.HasRoomFlag(1, 0x81, 0x40),
                "Leaving the active escort must retire its event/control without its script-end gate or room-flag write.");
            LoadValidationRoom(1, 0x81);
            ApproachSoldierTrigger();
            int guard = 0;
            while (!_dialogue.IsOpen && guard++ < 160)
                StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            FailIf(!_dialogue.IsOpen || _runtimeState.ReadField("wcc05") != 0x7d,
                "The repeated escort did not reach TX_590b through the real object loop while retaining its pointer gate.");
            StepGameplayUpdates(6, Vector2.Zero, batched: batched);
            FailIf(_runtimeState.ReadField("wcc05") != 0x7d,
                "Dialogue must not apply the soldier's script-completion gate write.");
            _dialogue.Close();
            guard = 0;
            while (_rooms.CurrentRoom.Id == 0x81 && guard++ < 40)
                StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            FailIf(_rooms.ActiveGroup != 1 || _rooms.CurrentRoom.Id != 0x46 ||
                !_saveData.HasRoomFlag(1, 0x81, 0x40) || _runtimeState.ReadField("wcc05") != 0x7f,
                "The completed escort must set wcc05 bit 1 before the $00/$03 warp without resetting a bit-7-clear mask.");
            guard = 0;
            while (IsTransitioning && guard++ < 160)
                StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            FailIf(IsTransitioning || _runtimeState.ReadField("wcc05") != 0x7f,
                "The escort destination must complete its real transition without overwriting the retained load mask.");
            LoadValidationRoom(1, 0x81);
            FailIf(_roomEvents.Get<DekuForestSoldierEvent>().HasState ||
                _entities.Entities<NpcCharacter>().Any(npc => npc.Record.Id == 0x40 && npc.Record.SubId == 0x0a),
                "The completed room flag must suppress the pre-stream soldier on a later visit.");
        }
        GD.Print("Validated native Ages load-flag boot, all $100 masks in four scroll modes, soldier room-specific predicates/full-pool failure/cancellation, and real split/batched room-entry updates.");
    }
}
