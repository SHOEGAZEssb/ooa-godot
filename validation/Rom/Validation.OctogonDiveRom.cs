using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateOctogonDiveRom()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batched in new[] { false, true })
        foreach (bool underwaterBoss in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.Flippers,0);
            _inventory.GiveTreasure(TreasureId.MermaidSuit,0);
            _inventory.EquipA(0); _inventory.EquipB(0);
            // Already entered encounter; fresh intro/shutters are a separate
            // boundary. Keep actual room collision and both native boss forms.
            byte[] shared = [1,(byte)(underwaterBoss ? 1 : 0),0xff,0x28,0x28,0x78,0xff,(byte)(underwaterBoss ? 1 : 0)];
            for (int i = 0; i < shared.Length; i++) _runtimeState.SetWramByte(0xcfd0+i,shared[i]);
            LoadValidationRoom(5,0x36); _player.WarpTo(new(0x78,0x98)); _player.Face(Vector2I.Up);
            var free = typeof(RoomEntityManager).GetMethod("FreeEntity",flags)!;
            void KeepBoss()
            {
                var active = SomariaPrivate<List<IRoomEntity>>(_entities,"_activeEntities");
                foreach (var entity in active.Where(entity => entity is not OctogonRoomEntity &&
                    entity is not OctogonPartRoomEntity && entity is not OctogonInteractionRoomEntity).ToArray())
                { active.Remove(entity); free.Invoke(_entities,[entity]); }
            }
            KeepBoss();
            FailIf(_collision.Collides(_player.Position),"Octogon dive must start on reachable original boss-room water.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,0,0x78,0x98)
            { HostileEnemiesEnabled = true, HostilePartsEnabled = true };
            rom.InitializeLinkGameplay(); rom.CreateMenuView().LoadDungeon(0x0c);
            // A stationary arrived Link retains ANGLE_NONE; native state1 is
            // declared here rather than replaying an unrelated entrance warp.
            rom[0xd009] = 0xff;
            // Somaria's default fixture omits object parsing. This route owns
            // a live encounter, so preserve both original parser enable bits.
            rom[0xcc05] = 3;
            FailIf(rom[0xcc3b] != 1 || rom[0xcc3a] != 0x08,
                $"Octogon's surface room$5:$36 must occupy present Mermaid floor1/map cell$08: ROM=${rom[0xcc3b]:x2}/${rom[0xcc3a]:x2}, dungeon=${rom[0xcc39]:x2}.");
            for (int i = 0; i < shared.Length; i++) rom[0xcfd0+i] = shared[i];
            byte[] headers = [0xc8,0xc9,0xca,0x8e,0xa5,0x8f,0xa7];
            for (int i = 0; i < headers.Length; i++) rom[0xcc08+i*2] = headers[i];
            var enemySlots = SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_enemySlots");
            var main = enemySlots.Single(); int p = 0xd080+main.Value*256;
            rom[p] = 1; rom[p+1] = 0x7d;
            rom[p+11] = (byte)main.Key.Node.Position.Y; rom[p+13] = (byte)main.Key.Node.Position.X;
            rom[0xcdd1] = 1;
            int heldBefore = 0, update = 0, loads = 0;
            var checkedTextures = new HashSet<(ulong, int)>();
            var checkedOamTextures = new HashSet<(ulong, string, int)>();
            void CompareBoss()
            {
                foreach (var pair in enemySlots.Where(pair => pair.Key.Node is OctogonCharacter))
                {
                    var actor = (OctogonCharacter)pair.Key.Node; int a = 0xd080+pair.Value*256;
                    ValidateOctogonDrawTexture(actor,rom[a+0x1c]&7,checkedTextures);
                    if (!actor.ShellForm && actor.Visible)
                        ValidateOctogonOamTexture(actor,actor.CurrentDrawTexture,OctogonNativeOam(rom.Word(a+0x1e)),
                            rom[a+0x1d],rom[a+0x1c]&7,checkedOamTextures);
                    // parseObjectData writes identity/placement only. The
                    // managed node carries its definition before native state0
                    // loads health; compare live HP after that first dispatch.
                    FailIf(rom[a] == 0 || actor.State != rom[a+4] || actor.Depth != rom[a+3] ||
                        actor.State != 0 && actor.Health != rom[a+41] ||
                        actor.CollisionType != rom[a+36] || actor.Counter1 != rom[a+6] || actor.Counter2 != rom[a+7] ||
                        actor.Position != new Vector2(rom.Word(a+12)/256f,rom.Word(a+10)/256f),
                        $"Octogon dive update{update} room${_currentRoom.Id:x2}: body/shell handoff differs: runtime={actor.State}/{actor.Depth}/{actor.Health}/{actor.Counter1}/{actor.Counter2}/{actor.Position}, ROM={rom[a+4]}/{rom[a+3]}/{rom[a+41]}/{rom[a+6]}/{rom[a+7]}/{rom.Word(a+12)/256f},{rom.Word(a+10)/256f}; native slots="+
                        string.Join(',',Enumerable.Range(0,16).Where(i => rom[0xd080+i*256] != 0).Select(i => $"{i:x2}:{rom[0xd081+i*256]:x2}"))+
                        $", count={rom[0xcdd1]}, flags=${rom[0xca2d]:x2}, WRAM=${rom[0xff70]:x2}, room flags=${rom[0xcc34]:x2}, collision=${rom[0xcc33]:x2}, tile/solid=$97:${rom[0xcf97]:x2}/${rom[0xce97]:x2}, $28:${rom[0xcf28]:x2}/${rom[0xce28]:x2}, placement="+
                        string.Join(',',Enumerable.Range(0,8).Select(i => rom[0xcec0+i].ToString("x2")))+".");
                }
                for (int i = 0; i < shared.Length; i++)
                    FailIf(_runtimeState.ReadWramByte(0xcfd0+i) != rom[0xcfd0+i],
                        $"Octogon dive update{update}: shared encounter byte${0xcfd0+i:x4} differs.");
                var rng = _random.CaptureState();
                FailIf(rng.Rng1 != rom[0xff94] || rng.Rng2 != rom[0xff95] || rng.Calls-seed.Calls != rom.RandomCalls,
                    $"Octogon dive update{update}: global RNG differs, runtime={rng.Calls-seed.Calls}, ROM={rom.RandomCalls}.");
            }
            void Step(int count = 1, int held = 0)
            {
                int pressed = held&~heldBefore; heldBefore = held;
                Vector2 movement = (held&0x40) != 0 ? Vector2.Up : (held&0x80) != 0 ? Vector2.Down : Vector2.Zero;
                int angle = movement == Vector2.Up ? 0 : movement == Vector2.Down ? 16 : 0xff;
                StepGameplayUpdates(count,movement,MenuRomActions(held),MenuRomActions(pressed),batched,() =>
                {
                    rom.AdvanceWarpPalette();
                    int oldRoom = rom[0xcc30];
                    if (rom[0xc2ef] == 3) rom.UpdateFadeOutWarp();
                    else
                    {
                        rom.UpdateGameplay(pressed,held,angle,_entities.FrameCounter);
                        if (rom[0xcc4b] != 0) rom.ApplyRequestedWarp();
                    }
                    pressed = 0;
                    if (rom[0xcc30] != oldRoom)
                    {
                        loads++; KeepBoss();
                        // The bounded route omits shutters/reward/static drops
                        // in both walks; body-generated attacks remain live.
                        for (int i = 0; i < 16; i++)
                        {
                            int a = 0xd040+i*256;
                            if (rom[a+1] is not (0x8e or 0x91))
                                for (int offset = 0; offset < 64; offset++) rom[a+offset] = 0;
                            int enemy = 0xd080+i*256;
                            if (rom[enemy+1] != 0x7d)
                                for (int offset = 0; offset < 64; offset++) rom[enemy+offset] = 0;
                        }
                    }
                    FailIf(_rooms.ActiveGroup != 5 || _currentRoom.Id != rom[0xcc30] ||
                        _player.PrecisePosition != new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f),
                        $"Octogon dive update{update}: room/fixed Link position differs: runtime=$5:${_currentRoom.Id:x2}/{_player.PrecisePosition}, ROM=${rom[0xcc2d]:x2}:${rom[0xcc30]:x2}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}; swim={_player.TopDownSwimmingState}/${rom[0xcc5d]:x2}, flags=${_currentRoom.TilesetFlags:x2}, tile=${_terrain.GetActiveTerrain(_player.Position).Terrain.Tile:x2}/${rom[0xcf00+rom[0xcc99]]:x2}, speed={_player.TopDownSwimSpeedRaw}/${rom[0xd010]:x2}, angle=${rom[0xd009]:x2}, native flags=${rom[0xd02f]:x2}.");
                    CompareBoss(); update++;
                });
            }
            Step(2);
            FailIf(!_player.TopDownSwimming,"Original Octogon water must initialize swimming through Link's dispatch.");
            Step(60);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                int before = loads;
                Step(1,2); Step();
                for (int i = 0; _transitions.IsTransitioning && i < 90; i++) Step();
                FailIf(loads != before+1 || _currentRoom.Id != 0x2d || _transitions.IsTransitioning,
                    "Actual Mermaid Suit dive must descend from room$5:$36 to$5:$2d and finish its fade.");
                Step(4);
                FailIf(!_entities.Entities<OctogonCharacter>().Any(actor => !actor.ShellForm && actor.Depth == (underwaterBoss ? 1 : 0)),
                    "The dive must retain Octogon's declared layer through native underwater initialization.");
                Step(1,2);
                for (int i = 0; _transitions.IsTransitioning && i < 90; i++) Step();
                FailIf(loads != before+2 || _currentRoom.Id != 0x36 || _transitions.IsTransitioning,
                    "Allowed original surfacing mask must return Link and the encounter to room$5:$36.");
                Step(4);
            }
        }
        GD.Print("Validated executed clean-US Octogon-room native water entry, actual dive/surface/repeat and live body/shell encounter handoff through the gameplay loop.");
    }
}
