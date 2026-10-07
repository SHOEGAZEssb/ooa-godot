using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareSpinnerGameplayRom()
    {
        var nativeImage = ValidationRom.LoadCleanUs();
        var nativeFrames = new Dictionary<int,string>();
        var visual = new DungeonInteractionVisualDatabase().Visual("spinner");
        foreach (int branch in new[] {0,1,2,3,4,5,6,7})
        foreach (bool batch in RomHostSchedules(branch is 5 or 6 ? 0 : branch))
        {
            bool initialRed = branch is 1 or 3 or 4,fullPool = branch == 2,pendingText = branch is 3 or 4;
            bool death = false;
            byte initialState = initialRed ? (byte)0xa1 : (byte)0xa0;
            int room = branch == 7 ? 0x52 : 0x60;
            ReinitializeGameplayForValidation(); _saveData.SetGlobalFlag(0x0f,branch == 7);
            _runtimeState.SetWramByte(OracleRuntimeState.SpinnerStateAddress,pendingText ? (byte)0xa0 : initialState);
            LoadValidationRoom(4,room); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0); _player.ApplicationUpdateOwned = true;
            var placement = new DungeonSpinnerDatabase().GetRoomRecords(4,room).Single();
            var spinner = new DungeonSpinnerRoomEntity(placement,_runtimeState,
                visual,_sound.PlaySound,_entities.BeginScreenShake);
            float startX = branch == 7 ? 120.75f : 120.25f;
            _entities.AddEntity(spinner); _player.WarpTo(new(startX,40.5f)); _player.Face(Vector2I.Down);
            FailIf(_collision.Collides(_player.Position),"Spinner approach must begin on the actual north floor of Moonlit$4:$60.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,2,120,40);
            rom.Word(0xd00c,(int)(startX*256)); rom.Word(0xd00a,40*256+128);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcc39] = 3; rom[0xcdd4] = initialState;
            rom[0xd240] = 1; rom[0xd241] = 0x7d; rom[0xd24b] = 0x57; rom[0xd24d] = 1;
            if (pendingText) _runtimeState.SetWramByte(OracleRuntimeState.SpinnerStateAddress,initialState);
            FailIf(_entities.InteractionSlot(spinner) != 2,"The room-specific spinner must reserve original INTERACTION$d2.");
            if (fullPool) for (int slot = 3; slot < 16; slot++)
            {
                _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(200,120),SoundId.MusNone));
                int address = (0xd0+slot)*256+0x40;
                rom[address] = 1; rom[address+1] = 5; rom[address+2] = 0x80; rom[address+0xb] = 120; rom[address+0xd] = 200;
            }
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            string NativeOam(int slot)
            {
                int pointer = rom.Word(slot+0x1e);
                if (nativeFrames.TryGetValue(pointer,out string? encoded)) return encoded;
                int offset = 0x14*0x4000+(pointer&0x3fff),count = nativeImage.Span[offset];
                FailIf(pointer is < 0x4000 or >= 0x8000 || count > 40,"Spinner native OAM pointer/count is invalid.");
                encoded = string.Join(';',Enumerable.Range(0,count).Select(cell =>
                    string.Join(',',Enumerable.Range(0,4).Select(b => nativeImage.Span[offset+1+cell*4+b]))));
                nativeFrames.Add(pointer,encoded); return encoded;
            }
            string FrameOam(EnemyAnimationPlayer animation) =>
                OracleGraphicsCache.GetAnimationDefinition(visual.Animations[animation.AnimationIndex])
                    .Frames[animation.FrameIndex].EncodedOam;
            void Step(int count = 1,int angle = 0xff) => StepGameplayUpdates(count,
                angle == 0xff ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(angle),batched:batch,afterUpdate:() => {
                if (death) rom.AdvanceDeathPrelude();
                rom.UpdateGameplay(0,angle switch {0=>0x40,16=>0x80,8=>0x10,24=>0x20,_=>0},angle,_entities.FrameCounter);
                rom.CreateMenuView().UpdateScreenShake();
                int nativePhase = rom[0xd244];
                if (nativePhase == 1 && (rom[0xcc95]&0x80) != 0) nativePhase = 2;
                int phase = spinner.Phase switch {
                    SpinnerPhase.Waiting=>1,SpinnerPhase.Touched=>2,SpinnerPhase.Turning=>3,SpinnerPhase.Exiting=>4,_=>-1};
                FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f) ||
                    phase != nativePhase || spinner.Red != (rom[0xd249] != 0) ||
                    _runtimeState.ReadWramByte(OracleRuntimeState.SpinnerStateAddress) != rom[0xcdd4] ||
                    _entities.ScreenShakeCounter != rom[0xcd18] || _entities.HorizontalScreenShakeCounter != rom[0xcd19] ||
                    phase == 1 && spinner.WaitCounter != rom[0xd246] || phase == 4 && spinner.ExitCounter != rom[0xd246] ||
                    !sounds.Requests.Where(cue=>cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                    $"Spinner update{++update}, batch={batch}: Link={_player.PrecisePosition}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}, phase={phase}/{nativePhase}, counter={spinner.WaitCounter}:{spinner.ExitCounter}/{rom[0xd246]}, state={rom[0xcdd4]:x2}, Linkstate={rom[0xd004]:x2}, signal={rom[0xcc95]:x2}, cues=[{string.Join(',',sounds.Requests)}]/[{string.Join(',',rom.Sounds)}].");
                var random = _random.CaptureState();
                if (death) FailIf(_player.HealthQuarters != rom[0xc6aa] ||
                    _player.IsDying != (rom[0xcdd5] != 0) ||
                    _player.DeathAnimationActive != (rom[0xd004] == 3 && rom[0xd005] == 1),
                    $"Spinner death update{update}: lethal publication or death dispatch differs.");
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls,
                    $"Spinner update{update} RNG differs: {random.Rng1:x2}/{random.Rng2:x2}/{random.Calls-seed.Calls} vs {rom[0xff94]:x2}/{rom[0xff95]:x2}/{rom.RandomCalls}.");
                var body = SomariaPrivate<EnemyAnimationPlayer>(spinner,"_spinnerAnimation");
                FailIf(body.CurrentParameter != rom[0xd261] || SomariaPrivate<int>(body,"_frameCounter") != rom[0xd260] ||
                    FrameOam(body) != NativeOam(0xd240),
                    $"Spinner update{update}: original parent animation parameter/counter differ.");
                var arrow = _entities.Entities<DungeonSpinnerArrowRoomEntity>().SingleOrDefault();
                bool nativeArrow = rom[0xd340] != 0 && rom[0xd341] == 0x7d;
                FailIf((arrow != null) != nativeArrow || fullPool && arrow != null,
                    "Spinner arrow allocation must fail once with a full pool and remain absent after filler slots expire.");
                if (arrow != null)
                {
                    var animation = SomariaPrivate<EnemyAnimationPlayer>(arrow,"_animation");
                    FailIf(_entities.InteractionSlot(arrow) != 3 || arrow.Position != new Vector2(rom[0xd34d],rom[0xd34b]) ||
                        arrow.Visible != ((rom[0xd35a]&0x80) != 0) || animation.CurrentParameter != rom[0xd361] ||
                        SomariaPrivate<int>(animation,"_frameCounter") != rom[0xd360] ||
                        FrameOam(animation) != NativeOam(0xd340) ||
                        (SomariaPrivate<bool>(arrow,"_red") ? 5 : 4) != (rom[0xd35c]&7),
                        $"Spinner branch{branch}, update{update}: arrow slot/position/visibility/palette/animation differ; OAM={FrameOam(animation)}/{NativeOam(0xd340)} at${rom.Word(0xd35e):x4}.");
                }
            });
            if (pendingText)
            {
                _dialogue.ShowGameplayMessage("Spinner state0",120); rom[0xcba0] = branch == 3 ? (byte)1 : (byte)0x80;
                Step(3);
                FailIf(!spinner.Red || spinner.WaitCounter != 0 || spinner.Arrow == null,
                    "Pending parent/arrow must initialize during text from the live spinner state, then retain script and animation counters.");
                _dialogue.Close(); rom[0xcba0] = 0;
            }
            if (branch is 5 or 6)
            {
                if (branch == 5) Step(8);
                else { Step(33); Step(33,16); }
                int heldCounter = spinner.WaitCounter;
                SpinnerPhase heldPhase = spinner.Phase;
                FailIf(branch == 5 && heldCounter == 0 || branch == 6 && heldPhase != SpinnerPhase.Touched,
                    "Spinner death must interrupt a running wait30 or the collision-reachable setanimation/incstate handoff.");
                FailIf(!_player.ApplyDamage(_player.HealthQuarters),"Spinner fixture must publish actual lethal damage.");
                rom.ApplyLinkDamage(unchecked((byte)(-2*rom[0xc6aa]))); rom[0xd004] = 3; death = true;
                Step(10); Step(3,16);
                FailIf(spinner.WaitCounter != heldCounter || spinner.Phase != heldPhase ||
                    _runtimeState.ReadWramByte(OracleRuntimeState.SpinnerStateAddress) != initialState,
                    "Original interactionRunScript death gate must hold the counter and pending incstate while the independent arrow continues.");
                continue;
            }
            Step(33); Step(32,16);
            FailIf(spinner.Phase != SpinnerPhase.Waiting,"The high-byte collision window must reject original delta-$10.");
            Step(1,16);
            FailIf(spinner.Phase != SpinnerPhase.Touched,$"Spinner branch{branch}: actual north-floor movement must enter the original touch handoff, Link={_player.PrecisePosition}.");
            for (int wait = 0; spinner.Phase != SpinnerPhase.Waiting && wait < 80; wait++) Step();
            FailIf(spinner.Phase != SpinnerPhase.Waiting || spinner.Red == initialRed,"The first native turn/forced exit must XOR only mask$01.");
            Step(32); // Original post-use wait, while Link remains outside the hitbox.
            for (int walk = 0; spinner.Phase == SpinnerPhase.Waiting && walk < 40; walk++) Step(1,initialRed ? 24 : 8);
            FailIf(spinner.Phase != SpinnerPhase.Touched,"The repeat action must approach through the original west floor.");
            for (int wait = 0; spinner.Phase != SpinnerPhase.Waiting && wait < 80; wait++) Step();
            FailIf(spinner.Phase != SpinnerPhase.Waiting || spinner.Red != initialRed || rom[0xcdd4] != initialState,
                "The repeated actual west approach must complete the clockwise turn and preserve unrelated spinner bits.");
            Step(2);
        }
    }
}
