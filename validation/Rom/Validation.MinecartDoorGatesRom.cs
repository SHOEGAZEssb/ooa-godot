using Godot;
using System;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareMinecartDoorGatesRom()
    {
        int fixture = 0;
        foreach (int queued in new[] { 0,30,31 })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0x7c); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.SwitchHook,1);
            _inventory.EquipA(0); _inventory.EquipB(0);
            _player.WarpTo(new(184.25f,88.5f)); _player.Face(Vector2I.Left);
            FailIf(_collision.Collides(_player.Position),"Door gates must retain the original safe room$4:$7c Link floor.");
            // Declared pending one-shot opener at a distant doorway. Actual
            // cart allocation/order and subsequent riding are tested above.
            _currentRoom.SetPositionTileAndCollision(new(120,168),0x7c,0x0f,0);
            var door = _entities.Spawn<MinecartShutterRoomEntity>(new MinecartShutterOpenSpawn(0xa7,0x7c));
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,3,184,88);
            rom.Word(0xd00a,88*256+128); rom.Word(0xd00c,184*256+64);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xd240] = 1; rom[0xd241] = 0x1e; rom[0xd249] = 0x18; rom[0xd24b] = 0xa7;
            int slot = 0xd240;
            Func<bool> previous = _entities.PaletteFadeActiveSource;
            bool fading = false; _entities.PaletteFadeActiveSource = () => fading;
            var sounds = _sound.AttachPlayRequestAudit();
            void Step(int count = 1,bool press = false)
            {
                int tick = 0;
                StepGameplayUpdates(count,Vector2.Zero,press ? ["attack"] : [],press ? ["attack"] : [],batch,() => {
                rom.AdvanceDeathPrelude();
                rom.UpdateGameplay(press && tick++ == 0 ? 1 : 0,press ? 1 : 0,0xff,_entities.FrameCounter);
                if (!_player.IsDying) CompareSomariaMotionRom(rom,"Minecart door gate Link/room handoff");
                else
                {
                    FailIf(_player.HealthQuarters != rom[0xc6aa] || !_player.IsDying ||
                        _player.PrecisePosition != new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f) ||
                        _player.DeathAnimationActive != (rom[0xd004] == 3 && rom[0xd005] == 1) ||
                        _player.DeathAnimationCounter != rom[0xd020] || _player.DeathAnimationFrame != rom[0xd031] ||
                        _player.DeathSpinLoopsRemaining != rom[0xd006],
                        $"Minecart death runtime health={_player.HealthQuarters}/XY={_player.PrecisePosition}/active={_player.DeathAnimationActive}/counter={_player.DeathAnimationCounter}/frame={_player.DeathAnimationFrame}/loops={_player.DeathSpinLoopsRemaining}, " +
                        $"native health={rom[0xc6aa]}/XY={rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}/state=${rom[0xd004]:x2}:${rom[0xd005]:x2}/counter={rom[0xd020]}/frame={rom[0xd031]}/loops={rom[0xd006]}.");
                    for (int p = 0; p < 0xb0; p++)
                    {
                        Vector2 point = new((p&15)*16+8,(p>>4)*16+8);
                        if ((p&15) == 15) continue;
                        FailIf(_currentRoom.Layout[p] != rom[0xcf00+p] ||
                            _currentRoom.GetTerrainInfo(point).Collision != rom[0xce00+p] ||
                            _currentRoom.GetUnderlyingMetatile(point) != rom.Underlying(p),
                            $"Dying opener room buffers differ at${p:x2}.");
                    }
                }
                rom.AdvanceTileGraphics();
                bool alive = rom[slot] != 0 && rom[slot+1] == 0x1e;
                FailIf(door.Finished == alive || alive && SomariaPrivate<int>(door,"_counter") != rom[slot+6] ||
                    _rooms.PendingTileGraphics != ((rom[0xcce0]-rom[0xccdf])&31) ||
                    _entities.SwitchHook!.ExchangeState != rom[0xccdd],
                    $"Minecart door queued={queued}, batch={batch}: runtime state={door.State}/finished={door.Finished}/counter={SomariaPrivate<int>(door,"_counter")}/queue={_rooms.PendingTileGraphics}/Hook={_entities.SwitchHook.ExchangeState}, " +
                    $"native slot=${slot:x4}/state={rom[slot+4]}/enabled={rom[slot]}/counter={rom[slot+6]}/queue={(rom[0xcce0]-rom[0xccdf])&31}/Hook={rom[0xccdd]}.");
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                    random.Calls-seed.Calls != rom.RandomCalls || !sounds.Requests.SequenceEqual(rom.Sounds),
                    "Minecart door eligibility/rejected publication must retain ordered cues and RNG.");
                });
            }
            void Fade(bool active) { fading = active; rom[0xc4ab] = active ? (byte)1 : (byte)0; }
            try
            {
                _dialogue.ShowGameplayMessage("Pending opener pause",120); rom[0xcba0] = 1;
                Step(); Step(3);
                FailIf(door.State != MinecartShutterState.RunOpenerScript || rom[0xd244] != 1 || rom[0xd245] != 0,
                    $"Pending state0 text mismatch: runtime={door.State}, native=${rom[0xd244]:x2}:${rom[0xd245]:x2}, enabled=${rom[0xd240]:x2}, text=${rom[0xcba0]:x2}.");
                _dialogue.Close(); rom[0xcba0] = 0;
                Fade(true); Step(3);
                FailIf(door.State != MinecartShutterState.ReadyToOpen || rom[0xd246] != 0,
                    "Palette mode must hold the one-shot opening before interleaving starts.");
                Fade(false); Step(3);
                FailIf(door.State != MinecartShutterState.OpeningInterleaved || rom[0xd246] != 4,
                    "Palette release must start the six-update opening countdown.");
                Fade(true); Step(3);
                FailIf(rom[0xd246] != 4 || !_currentRoom.IsSolid(new(120,168)),
                    "A second palette mode must hold the existing interleave and collision.");
                Fade(false); Step(3);
                FailIf(rom[0xd246] != 1,"Door publication must wait for the terminal sixth counter update.");
                for (int write = 0; write < queued; write++)
                {
                    FailIf(!_rooms.TrySetTile(0x11,0xa0),"Door gate queue input must accept its declared writes.");
                    rom.SetTile(0x11,0xa0);
                }
                Step(); Step(8);
                FailIf(!door.Finished || _currentRoom.Layout[0xa7] != 0x5e ||
                    _currentRoom.IsSolid(new(120,168)) != (queued == 31),
                    "Full-queue rejection must retire the opener with interleaved layout but retained collision, without a later retry.");
                if (queued != 0)
                {
                    // Native lethal publication while pending, or during
                    // state2, distinguishes incstate from scriptend gates.
                    FailIf(!_rooms.TrySetTile(0xa7,0x7c),"Death opener must receive its declared closed doorway input.");
                    rom.SetTile(0xa7,0x7c);
                    door = _entities.Spawn<MinecartShutterRoomEntity>(new MinecartShutterOpenSpawn(0xa7,0x7c));
                    byte[] deathCreator = [0x01,0x00,0x1e,0x16,0xd0,0xcd,0xc5,0x24,0xc9];
                    for (int index = 0; index < deathCreator.Length; index++) rom[0xc100+index] = deathCreator[index];
                    SomariaPrivate<FrontendRom>(rom,"_rom").Call(0xc100,0);
                    slot = 0xd240; rom[slot+9] = 0x18; rom[slot+0xb] = 0xa7;
                    if (queued == 30) Step(4);
                    FailIf(!_player.ApplyDamage(_player.HealthQuarters),"Death opener input must publish real lethal Link damage.");
                    rom.ApplyLinkDamage(unchecked((byte)(-2*rom[0xc6aa]))); rom[0xd004] = 3;
                    Step(8); Step(3);
                    FailIf(door.Finished || rom[slot+4] != 1 ||
                        door.State != (queued == 31 ? MinecartShutterState.RunOpenerScript : MinecartShutterState.ScriptEnd) ||
                        _currentRoom.IsSolid(new(120,168)) != (queued == 31),
                        "Death must retain the native interaction at incstate or after terminal scriptend without undoing completed tile publication.");
                    continue;
                }
                _inventory.EquipA(TreasureId.SwitchHook); rom[0xc689] = 0x0a;
                Step(press:true);
                for (int wait = 0; rom[0xccdd] != 2 && wait < 60; wait++) Step();
                FailIf(rom[0xccdd] != 2,"Actual original room$4:$7c diamond contact must enter Hook exchange$02.");
                FailIf(!_rooms.TrySetTile(0xa7,0x7c),"Hook-gated opener must receive its declared closed doorway input.");
                rom.SetTile(0xa7,0x7c);
                slot = Enumerable.Range(0xd2,14).Select(page => (page<<8)|0x40).First(address => rom[address] == 0);
                door = _entities.Spawn<MinecartShutterRoomEntity>(new MinecartShutterOpenSpawn(0xa7,0x7c));
                FailIf(_entities.InteractionSlot(door) != (slot>>8)-0xd0,
                    "Hook-gated door must allocate after the live chain through the original first-free pool.");
                byte[] caller = [0x01,0x00,0x1e,0x16,0xd0,0xcd,0xc5,0x24,0xc9];
                for (int index = 0; index < caller.Length; index++) rom[0xc100+index] = caller[index];
                SomariaPrivate<FrontendRom>(rom,"_rom").Call(0xc100,0);
                rom[slot+9] = 0x18; rom[slot+0xb] = 0xa7;
                Step(5);
                FailIf(door.State != MinecartShutterState.Initialize || rom[slot+4] != 0 || rom[0xccdd] != 2,
                    "Hook exchange$02 must reject even pending door initialization through lift/swap/lowering.");
                for (int wait = 0; rom[slot+4] != 2 && wait < 80; wait++) Step();
                FailIf(door.State != MinecartShutterState.ReadyToOpen || rom[slot+4] != 2 || rom[slot+5] != 0,
                    "The first eligible post-exchange interaction pass must initialize, with interleave deferred to the next update.");
                Step(7); Step(3);
                FailIf(!door.Finished || _currentRoom.IsSolid(new(120,168)),
                    "Door opening must complete after real Hook ownership releases without a delayed duplicate.");
                // A retained non-exitable textbox publishes native$80 once
                // printing completes. Its printer has separate ROM coverage;
                // declare that boundary while executing the actual port owner.
                _dialogue.ShowGameplayMessageWithFlags(string.Empty,120,0x0b); rom[0xcba0] = 1;
                for (int wait = 0; !_dialogue.PrintingComplete && wait < 24; wait++) Step();
                FailIf(!_dialogue.IsOpen || !_dialogue.PrintingComplete,
                    "The actual non-exitable textbox must retain its completed printing state.");
                rom[0xcba0] = 0x80;
                FailIf(!_rooms.TrySetTile(0xa7,0x7c),"Retained-text opener must receive its declared closed doorway input.");
                rom.SetTile(0xa7,0x7c);
                slot = Enumerable.Range(0xd2,14).Select(page => (page<<8)|0x40).First(address => rom[address] == 0);
                door = _entities.Spawn<MinecartShutterRoomEntity>(new MinecartShutterOpenSpawn(0xa7,0x7c));
                for (int index = 0; index < caller.Length; index++) rom[0xc100+index] = caller[index];
                SomariaPrivate<FrontendRom>(rom,"_rom").Call(0xc100,0);
                rom[slot+9] = 0x18; rom[slot+0xb] = 0xa7;
                Step(); Step(3);
                FailIf(door.State != MinecartShutterState.ReadyToOpen || rom[slot+4] != 2 || rom[slot+5] != 0,
                    "Text$80 must permit state0's incstate while retaining the initialized state2 through later object updates.");
                _dialogue.Close(); rom[0xcba0] = 0;
                Step(7); Step(3);
                FailIf(!door.Finished || _currentRoom.IsSolid(new(120,168)),
                    "Closing retained text must release the pending opening once.");
            }
            finally { _entities.PaletteFadeActiveSource = previous; _dialogue.Close(); }
        }
    }
}
