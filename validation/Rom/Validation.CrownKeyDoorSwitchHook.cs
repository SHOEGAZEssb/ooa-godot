using Godot;
using System;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownKeyDoorSwitchHook()
    {
        const BindingFlags flags = BindingFlags.Instance|BindingFlags.NonPublic;
        var state = typeof(DungeonKeyDoorController).GetField("_openingState",flags)!;
        int fixture = 0;
        foreach (bool initializeDuringLift in new[] { false,true })
        foreach (bool cancel in new[] { false,true })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0x7c); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.SwitchHook,1); _inventory.EquipA(TreasureId.SwitchHook); _inventory.EquipB(0);
            Vector2 start = new(184.25f,88.5f),doorPosition = new(120,168);
            _player.WarpTo(start); _player.Face(Vector2I.Left);
            FailIf(_collision.Collides(start) || _currentRoom.GetMetatile(new(136,88)) != 0xdb,
                "Reserved door/Hook handoff requires unchanged room4:7c floor and its actual diamond.");
            var oldScreen = _entities.WorldToScreen;
            _entities.WorldToScreen = p => p-new Vector2(80,48);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,3,184,88);
            rom.Word(0xd00a,(int)(start.Y*256)); rom.Word(0xd00c,(int)(start.X*256));
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xffaa] = 48; rom[0xffac] = 80;
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1,bool press = false) =>
                StepSomariaMotionRom(rom,count,batch,held:press ? 1 : 0,pressed:press ? 1 : 0,afterUpdate:() => {
                    rom.AdvanceTileGraphics();
                    string context = $"Reserved door/Hook pending={initializeDuringLift}, cancel={cancel}, batch={batch}, update={++update}";
                    CompareKeyDoorGameplayRom(rom,seed,sounds.Requests,context,dungeon:4);
                    var hook = _entities.SwitchHook!.Item;
                    bool native = rom[0xd600] != 0 && rom[0xd601] == 0x0a;
                    FailIf((hook is { Finished:false }) != native || _player.IsUsingSwitchHook != (rom[0xd200] != 0) ||
                        _entities.SwitchHook.ExchangeState != rom[0xccdd] || (_player.SwitchHookZFixed&0xffff) != rom.Word(0xd00e),
                        context+": native parent/weapon/exchange/Link Z differs.");
                    if (native)
                        FailIf(hook!.State != rom[0xd604] || hook.Substate != rom[0xd605] || hook.Counter != rom[0xd606] ||
                            hook.ZHigh != (sbyte)rom[0xd60f] || hook.PrecisePosition != new Vector2(rom.Word(0xd60c)/256f,rom.Word(0xd60a)/256f),
                            context+": native live Hook state/counter/fixed XY/Z differs.");
                });
            try
            {
                Step(20); Step(press:true);
                for (int wait = 0; _entities.SwitchHook!.ExchangeState != 1 && wait < 50; wait++) Step();
                FailIf(_entities.SwitchHook!.ExchangeState != 1,"Actual diamond contact must enter the original latch state$01.");
                Step(initializeDuringLift ? 18 : 15);
                // Declared reserved0 allocation away from the real Hook target.
                // Actual key debit/approach have separate gameplay scenarios;
                // this fixture isolates the original allocated-handler gate.
                FailIf(!_rooms.KeyDoors.TryGet(_currentRoom.ActiveCollisions,0x70,out var door),
                    "Allocated opener must use the imported upward key-door tile$70.");
                FailIf(!_rooms.TrySetTile(0xa7,0x70),"Declared door write must fit the canonical tile queue.");
                rom.SetTile(0xa7,0x70);
                typeof(DungeonKeyDoorController).GetField("_door",flags)!.SetValue(_keyDoors,door);
                typeof(DungeonKeyDoorController).GetField("_doorCenter",flags)!.SetValue(_keyDoors,doorPosition);
                typeof(DungeonKeyDoorController).GetField("_opening",flags)!.SetValue(_keyDoors,true);
                state.SetValue(_keyDoors,Enum.Parse(state.FieldType,"Initialize"));
                rom[0xd040] = 1; rom[0xd041] = 0x1e; rom[0xd04b] = 0xa7; rom[0xd049] = 0;
                if (!initializeDuringLift)
                {
                    Step(2);
                    FailIf(_entities.SwitchHook.ExchangeState != 1 || _keyDoors.OpeningCounter != 6,
                        "Latch$01 must admit reserved0 initialization and its six-update interleave.");
                    Step();
                }
                int heldCounter = initializeDuringLift ? 0 : 6;
                FailIf(_entities.SwitchHook.ExchangeState != 2 || _keyDoors.OpeningCounter != heldCounter,
                    "ITEM must publish exchange$02 before the same update's reserved0 dispatch.");
                Step(cancel ? 5 : 32);
                FailIf(_keyDoors.OpeningCounter != heldCounter ||
                    state.GetValue(_keyDoors)!.ToString() != (initializeDuringLift ? "Initialize" : "Animate"),
                    "Lift/swap/lowering must hold pending state0 and the existing door counter.");
                if (cancel)
                {
                    // Declared parent loss: itemCode0aPost clears exchange
                    // after the already-frozen INTERACTION pass. No claim
                    // about damage/room-reload cancellation eligibility.
                    _entities.SwitchHook.ClearParent(); rom.ClearItemParents(); Step();
                    FailIf(_entities.SwitchHook.ExchangeState != 0 || _keyDoors.OpeningCounter != heldCounter,
                        "Post-pass cancellation must leave this update's reserved0 counter frozen.");
                }
                Step();
                FailIf(_entities.SwitchHook.ExchangeState != 0 ||
                    _keyDoors.OpeningCounter != (initializeDuringLift ? 0 : 5),
                    "Reserved0 must resume on the first update sampling exchange$00.");
                if (initializeDuringLift)
                {
                    FailIf(state.GetValue(_keyDoors)!.ToString() != "Ready","Deferred state0 must execute once after exchange release.");
                    Step();
                    FailIf(_keyDoors.OpeningCounter != 6,"Deferred opener must begin the complete six-update interleave.");
                }
                Step(initializeDuringLift ? 5 : 4);
                FailIf(!_keyDoors.Opening || _keyDoors.OpeningCounter != 1 || !_currentRoom.IsSolid(doorPosition),
                    "Resumed opener must retain collision through its penultimate update.");
                Step(); Step(3);
                FailIf(_keyDoors.Opening || _currentRoom.IsSolid(doorPosition),
                    "Reserved0 must complete and stay retired after its final eligible counter update.");
            }
            finally { _entities.WorldToScreen = oldScreen; }
        }
    }
}
