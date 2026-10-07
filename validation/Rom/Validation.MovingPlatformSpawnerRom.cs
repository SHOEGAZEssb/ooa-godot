using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareMovingPlatformSpawnerRom()
    {
        foreach (int branch in new[] { 0, 1, 2, 3, 4, 5 })
        foreach (bool batch in RomHostSchedules(branch))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0x16); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            float startY = branch == 4 ? 40.5f : 56.5f;
            _player.WarpTo(new(200.25f,startY)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position),"Platform trigger approach must start on original room$4:$16 floor.");
            // Isolate INTERAC$20:$05 and both actual PART$09 buttons. The
            // preceding chest script and trailing shutter producer are excluded.
            var spawner = new SpiritsGraveMovingPlatformSpawner(_entities.TriggerIsActive,_sound.PlaySound,30,
                _runtimeState,_entities.IsScriptTextActive,_entities.TryCreatePuzzlePuff,_entities.TryCreateMovingPlatform);
            _entities.AddEntity(spawner);
            var mechanics = new DungeonMechanicDatabase();
            foreach (var record in mechanics.GetRoomRecords(4,0x16).Where(row=>row.Id == 9))
                _entities.AddEntity(new GroundButtonRoomEntity(record,_currentRoom,mechanics,_entities.SetTrigger,
                    (packed,tile)=>_rooms.TrySetTile(packed,tile),_sound.PlaySound));
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,0,200,(int)startY) { HostilePartsEnabled = true };
            rom.Word(0xd00c,200*256+64); rom.Word(0xd00a,(int)(startY*256));
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcc39] = 1;
            rom[0xd240] = 1; rom[0xd241] = 0x20; rom[0xd242] = 5;
            foreach (int index in new[] {0,1})
            {
                int address = 0xd0c0+index*256;
                rom[address] = 1; rom[address+1] = 9; rom[address+2] = (byte)index;
                rom[address+0xb] = 40; rom[address+0xd] = (byte)(index == 0 ? 40 : 200);
            }
            _runtimeState.SetWramByte(0xcfc1,0x91); _runtimeState.SetWramByte(0xcfc2,0x82);
            rom[0xcfc1] = 0x91; rom[0xcfc2] = 0x82;
            int update = 0,scriptStart = 0;
            bool death = false;
            var sounds = _sound.AttachPlayRequestAudit();
            void Step(int count = 1,int angle = 0xff) => StepGameplayUpdates(count,
                angle == 0xff ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(angle),batched:batch,afterUpdate:() => {
                if (death) rom.AdvanceDeathPrelude();
                rom.UpdateGameplay(0,angle switch {0=>0x40,16=>0x80,_=>0},angle,_entities.FrameCounter,
                    () => rom[0xcc96] = rom[0xcc8d]);
                rom.AdvanceTileGraphics();
                int phase = SomariaPrivate<int>(spawner,"_state");
                int offset = phase switch {0=>0,1=>7,2=>13,3=>17,4=>22,_=>24};
                if (scriptStart == 0) scriptStart = rom.Word(0xd258)-offset;
                string context = $"Platform spawner branch{branch}, batch={batch}, update{++update}";
                FailIf(spawner.Finished != (rom[0xd240] == 0) || !spawner.Finished &&
                    (SomariaPrivate<int>(spawner,"_counter") != rom[0xd246] ||
                     rom.Word(0xd258) != scriptStart+offset || spawner.Position != new Vector2(rom[0xd24d],rom[0xd24b])) ||
                    _entities.ActiveTriggers != rom[0xcca0] ||
                    _runtimeState.ReadWramByte(0xcfc1) != rom[0xcfc1] || _runtimeState.ReadWramByte(0xcfc2) != rom[0xcfc2] ||
                    _player.PrecisePosition != new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f) ||
                    _player.HealthQuarters != rom[0xc6aa] ||
                    !sounds.Requests.Where(cue=>cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                    context+$": phase={phase}, pointer=${rom.Word(0xd258):x4}/${scriptStart+offset:x4}, counter={SomariaPrivate<int>(spawner,"_counter")}/{rom[0xd246]}, deleted={spawner.Finished}/{rom[0xd240]:x2}, cues=[{string.Join(',',sounds.Requests)}]/[{string.Join(',',rom.Sounds)}].");
                var puffs = _entities.Entities<PuzzlePuffEffect>();
                int[] nativePuffs = Enumerable.Range(0xd2,14).Select(page=>page*256+0x40)
                    .Where(address=>rom[address] != 0 && rom[address+1] == 5).ToArray();
                FailIf(puffs.Count != nativePuffs.Length,context+": checked puff allocation differs.");
                foreach (var puff in puffs)
                {
                    int address = (0xd0+_entities.InteractionSlot(puff))*256+0x40;
                    FailIf(!nativePuffs.Contains(address) || puff.Position != new Vector2(rom[address+0xd],rom[address+0xb]) ||
                        puff.Initialized != (rom[address+4] != 0) || puff.CurrentParameter != rom[address+0x21] ||
                        SomariaPrivate<int>(puff,"_animationCounter") != rom[address+0x20],
                        context+": physical puff slot/position/initialization/animation differs.");
                }
                var platforms = _entities.Entities<MovingPlatformRoomEntity>();
                int[] nativePlatforms = Enumerable.Range(0xd2,14).Select(page=>page*256+0x40)
                    .Where(address=>rom[address] != 0 && rom[address+1] == 0x79).ToArray();
                FailIf(platforms.Count != nativePlatforms.Length,context+": checked platform allocation differs.");
                foreach (var platform in platforms)
                {
                    int address = (0xd0+_entities.InteractionSlot(platform))*256+0x40;
                    FailIf(!nativePlatforms.Contains(address) || platform.PrecisePosition != new Vector2(rom.Word(address+0xc)/256f,rom.Word(address+0xa)/256f) ||
                        platform.Counter != rom[address+6] || platform.Moving != (rom[address+5] != 0) || platform.Angle != rom[address+9],
                        context+": spawned platform must initialize in its physical slot and follow the dungeon1 script.");
                }
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls,
                    context+": shared RNG order differs.");
            });
            void Fill(int free)
            {
                for (int index = 0; index < 13-free; index++)
                {
                    var puff = _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(224,144),SoundId.MusNone));
                    int address = (0xd0+_entities.InteractionSlot(puff))*256+0x40;
                    rom[address] = 1; rom[address+1] = 5; rom[address+2] = 0x80;
                    rom[address+0xb] = 144; rom[address+0xd] = 224;
                }
            }
            _dialogue.ShowGameplayMessage("Pending platform script",120); rom[0xcba0] = branch == 4 ? (byte)0x80 : (byte)1;
            var allowsScript = _entities.TextAllowsScriptSource;
            try { _entities.TextAllowsScriptSource = () => branch == 4; Step(3); }
            finally { _entities.TextAllowsScriptSource = allowsScript; }
            _dialogue.Close(); rom[0xcba0] = 0;
            for (int walk = 0; SomariaPrivate<int>(spawner,"_state") == 0 && walk < 30; walk++) Step(angle:0);
            FailIf(SomariaPrivate<int>(spawner,"_state") != 1 || (_entities.ActiveTriggers&2) == 0,
                "Actual right button must select the first setcoords yield before either puff is created.");
            if (branch is 1 or 2) Fill(branch == 2 ? 1 : 0);
            Step(2);
            FailIf(SomariaPrivate<int>(spawner,"_counter") != 30 ||
                sounds.Requests.Count(cue=>cue == SoundId.SndPoof) != (branch == 1 ? 0 : branch == 2 ? 1 : 2),
                "The two separately yielded checked puffs must install wait30 with their native sound order.");
            Step(5);
            if (branch == 5)
            {
                FailIf(!_player.ApplyDamage(_player.HealthQuarters),"Platform script death fixture must publish actual lethal damage.");
                rom.ApplyLinkDamage(unchecked((byte)(-2*rom[0xc6aa]))); rom[0xd004] = 3; death = true;
                Step(10); Step(3,16);
                FailIf(SomariaPrivate<int>(spawner,"_counter") != 25 || spawner.Finished ||
                    _entities.Entities<MovingPlatformRoomEntity>().Count != 0,
                    "Actual death must hold the running script wait before platform allocation while puff actors continue.");
                continue;
            }
            _dialogue.ShowGameplayMessage("Platform wait",120); rom[0xcba0] = 0x80;
            Step(3); _dialogue.Close(); rom[0xcba0] = 0;
            Step(24);
            FailIf(SomariaPrivate<int>(spawner,"_counter") != 1,"Platform creation must remain one update away after the retained wait.");
            if (branch == 3) Fill(0);
            Step(3);
            FailIf(!spawner.Finished || sounds.Requests.Count(cue=>cue == SoundId.SndSolvePuzzle) != 1 ||
                _entities.Entities<MovingPlatformRoomEntity>().Count != (branch == 3 ? 0 : 1),
                "Platform allocation, later solve sound and script deletion must occur on separate original updates.");
            Step(36); Step(16,16); Step(16,0);
            FailIf(_entities.Entities<MovingPlatformRoomEntity>().Count != (branch == 3 ? 0 : 1) ||
                sounds.Requests.Count(cue=>cue == SoundId.SndSolvePuzzle) != 1,
                "Released slots and repeated actual button pressure must not retry an ended platform script.");
            LoadValidationRoom(4,0x14); LoadValidationRoom(4,0x16);
            FailIf(_entities.Entities<MovingPlatformRoomEntity>().Count != 0 ||
                _entities.Entities<SpiritsGraveMovingPlatformSpawner>().Count != 1,
                "Room re-entry must reconstruct the uncompleted platform script without retaining its spawned actor.");
        }
    }
}
