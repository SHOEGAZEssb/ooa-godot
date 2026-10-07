using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMermaidBossKeyRom()
    {
        const BindingFlags flags = BindingFlags.Instance|BindingFlags.NonPublic;
        foreach (bool batch in new[] { false,true })
        foreach (int subid in new[] { 0x06,0x46 })
        foreach (bool opened in new[] { false,true })
        foreach (bool pressure in !opened && subid == 6 ? new[] { false,true } : new[] { false })
        {
            ReinitializeGameplayForValidation();
            if (opened) _saveData.SetRoomFlag(5,0x1c,0x20);
            LoadValidationRoom(5,0x1c);
            var controller = _entities.Entities<MermaidBossKeyRoomEntity>().Single();
            var levers = _entities.Entities<LeverRoomEntity>().OrderBy(actor => actor.Position.X).ToArray();
            var lever = levers[subid == 6 ? 0 : 1];
            var active = SomariaPrivate<List<IRoomEntity>>(_entities,"_activeEntities");
            var free = typeof(RoomEntityManager).GetMethod("FreeEntity",flags)!;
            // Retain the actual controller and both lever hitboxes/connections.
            // The door and cross-era key mirror are checked separately.
            foreach (var entity in active.Where(actor => actor != controller && actor is not LeverRoomEntity && actor is not LeverConnectionRoomEntity).ToArray())
            { active.Remove(entity); free.Invoke(_entities,[entity]); }
            var blockers = new List<MermaidBossKeyRoomEntity>();
            if (pressure)
            {
                var placement = new MermaidDungeonDatabase().GetRoomRecords(5,0x1c).Single(row => row.Kind == DungeonObjectKind.MermaidBossKey);
                for (int index = 0; index < 11; index++)
                {
                    var blocker = new MermaidBossKeyRoomEntity(placement,_runtimeState,_random,
                        () => false,() => 0,static (_,_) => { },static () => { },static _ => { },static () => { });
                    _entities.AddEntity(blocker); blockers.Add(blocker);
                }
                // Declare a full pool with an earlier occupant. Releasing it
                // tests the ROM's parent/connection role swap independently.
                var slots = SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_interactionSlots");
                slots[controller] = 3; slots[levers[0]] = 4; slots[levers[1]] = 5;
                slots[blockers[0]] = 2;
                for (int index = 1; index < blockers.Count; index++) slots[blockers[index]] = index+5;
            }
            var failure = new MermaidDungeonDatabase().BossKeyFailureEnemies;
            FailIf(failure.Id != 0x10 || failure.SubId != 1 || failure.Flags != 1 || failure.Count != 4,
                "objectData78db must decode $81:$10:$01 as four counted, nonpersistent falling Ropes.");
            _inventory.GiveTreasure(TreasureId.Bracelet,1); _inventory.EquipA(0); _inventory.EquipB(TreasureId.Bracelet);
            int x = subid == 6 ? 0x58 : 0x98;
            _player.WarpTo(new(x,0x60)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position) || _currentRoom.GetTerrainInfo(_player.Position).Hazard != HazardType.None,
                "Boss-key levers must be approached from their original accessible southern floor.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,0,x,0x60) { HostileEnemiesEnabled = true };
            rom.InitializeLinkGameplay(); rom[0xd009] = 0xff; rom[0xcc39] = 6;
            int p = 0xd040+_entities.InteractionSlot(controller)*256;
            rom[p] = 1; rom[p+1] = 0x90; rom[p+11] = 0x18; rom[p+13] = 0x78;
            foreach (var actor in levers)
            {
                int a = 0xd040+_entities.InteractionSlot(actor)*256;
                rom[a] = 1; rom[a+1] = 0x61; rom[a+2] = (byte)(actor == levers[0] ? 6 : 0x46);
                rom[a+11] = 0x40; rom[a+13] = (byte)actor.Position.X;
            }
            foreach (var blocker in blockers)
            {
                int a = 0xd040+_entities.InteractionSlot(blocker)*256;
                rom[a] = 1; rom[a+1] = 0x90;
            }
            var sounds = _sound.AttachPlayRequestAudit(); int previous = 0,update = 0;
            _entities.SetTrigger(6,true); rom[0xcca0] = 0x40;
            void Step(int count = 1,bool held = false,int angle = 0xff)
            {
                int keys = (held ? 2 : 0) | (angle == 0 ? 0x40 : angle == 16 ? 0x80 : 0);
                int edge = keys&~previous; previous = keys;
                Vector2 movement = angle == 0xff ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(angle);
                StepGameplayUpdates(count,movement,MenuRomActions(keys),MenuRomActions(edge),batch,() =>
                {
                    rom.UpdateGameplay(edge,keys,angle,_entities.FrameCounter); edge = 0; update++;
                    rom.CheckTileWarps(); // Same post-object scratch alias as GameRoot.
                    rom.AdvanceTileGraphics();
                    string context = $"Mermaid boss-key lever${subid:x2}, opened={opened}, pressure={pressure}, batch={batch}, update{update}";
                    Vector2 link = new(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f);
                    FailIf(_player.PrecisePosition != link ||
                        CarriedObjectMotion.DirectionIndex(_player.FacingVector) != rom[0xd008] ||
                        _player.PatchCollisionsEnabled != ((rom[0xd024]&0x80) != 0),
                        context+$": Link motion/facing/collision runtime={_player.PrecisePosition}/{_player.FacingVector}/{_player.PatchCollisionsEnabled}, native={link}/{rom[0xd008]}/${rom[0xd024]:x2}.");
                    FailIf(controller.Finished != (rom[p] == 0) || !controller.Finished && controller.State != rom[p+4] ||
                        _entities.RoomEnemyCount != rom[0xcdd1] || _entities.ActiveTriggers != rom[0xcca0],
                        context+$": controller/count/trigger runtime={controller.State}/{controller.Finished}/{_entities.RoomEnemyCount}/${_entities.ActiveTriggers:x2}, native={rom[p+4]}/{rom[p] == 0}/{rom[0xcdd1]}/${rom[0xcca0]:x2}.");
                    foreach (var actor in levers)
                    {
                        int a = 0xd040+_entities.InteractionSlot(actor)*256;
                        int signal = actor == levers[0] ? 0xccab : 0xccac;
                        FailIf(actor.Position != new Vector2(rom[a+13],rom[a+11]) || actor.PullDistance != rom[signal],
                            context+": native short lever position or independent pull signal differs.");
                    }
                    var children = _entities.Entities<LeverConnectionRoomEntity>();
                    int nativeChildren = Enumerable.Range(0xd2,14).Count(page => rom[(page<<8)+0x40] != 0 &&
                        rom[(page<<8)+0x41] == 0x61 && rom[(page<<8)+0x42] == 0x80);
                    FailIf(children.Count != nativeChildren,context+": checked lever-child allocation differs.");
                    foreach (var child in children)
                    {
                        int a = 0xd040+_entities.InteractionSlot(child)*256;
                        // Uninitialized connections have not copied X yet.
                        FailIf(rom[a+2] != 0x80 || rom[a+4] != 0 &&
                            child.Position != new Vector2(rom[a+13],rom[a+11]),context+$": native connection slot${a:x4} runtime={child.Position}, native={rom[a+13]},{rom[a+11]}/${rom[a+2]:x2}/${rom[a+4]:x2}.");
                    }
                    var enemySlots = SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_enemySlots");
                    foreach (var rope in _entities.Entities<RopeCharacter>())
                    {
                        var actor = enemySlots.Keys.Single(entity => entity.Node == rope);
                        int a = 0xd080+enemySlots[actor]*256;
                        FailIf(rom[a] == 0 || (rom[a+1]&0x7f) != 0x10 || rom[a+2] != 1 ||
                            rope.Position != new Vector2(rom[a+13],rom[a+11]),context+": falling Rope ordered allocation/position differs.");
                    }
                    var rng = _random.CaptureState();
                    FailIf(rng.Rng1 != rom[0xff94] || rng.Rng2 != rom[0xff95] || rng.Calls-seed.Calls != rom.RandomCalls,
                        context+$": global RNG runtime=${rng.Rng1:x2}/${rng.Rng2:x2}/{rng.Calls-seed.Calls}, native=${rom[0xff94]:x2}/${rom[0xff95]:x2}/{rom.RandomCalls}.");
                    if (controller.State == 2)
                    {
                        for (int index = 0; index < 256; index++)
                            FailIf(rng.PlacementBuffer[index] != rom.FloorPermutation(index),context+": regenerated shared placement permutation differs.");
                        for (int address = 0xcec0; address < 0xcee0; address++)
                            FailIf(_runtimeState.ReadWramByte(address) != rom[address],context+$": placement scratch${address:x4} runtime=${_runtimeState.ReadWramByte(address):x2}, native=${rom[address]:x2}, exclusion=${_runtimeState.ReadWramByte(WramAddress.wWarpDestPos):x2}/${rom[0xcc4a]:x2}.");
                        FailIf(_runtimeState.ReadWramByte(WramAddress.wWarpDestPos) != rom[0xcc4a],context+": active tile exclusion differs.");
                    }
                    FailIf(_currentRoom.GetPackedStorageMetatile(0x17) != rom[0xcf17] ||
                        !sounds.Requests.SequenceEqual(rom.Sounds),context+": chest tile or ordered cues differ.");
                });
            }
            void ClearWave()
            {
                // Declare the counted defeat handoff; combat/death effects are
                // tested separately. This keeps the puzzle test bounded.
                foreach (var actor in active.Where(actor => actor.Node is EnemyCharacter).ToArray())
                { active.Remove(actor); free.Invoke(_entities,[actor]); }
                for (int slot = 0xd0; slot < 0xe0; slot++)
                    for (int offset = 0x80; offset < 0xc0; offset++) rom[(slot<<8)+offset] = 0;
                rom[0xcdd1] = 0;
            }
            Step();
            if (pressure)
            {
                FailIf(levers.Any(actor => actor.Visible) || _entities.Entities<LeverConnectionRoomEntity>().Count != 0,
                    "A full interaction pool must leave both levers in retrying state0.");
                foreach (var blocker in blockers)
                {
                    int a = 0xd040+_entities.InteractionSlot(blocker)*256;
                    for (int offset = 0; offset < 0x40; offset++) rom[a+offset] = 0;
                    active.Remove(blocker); free.Invoke(_entities,[blocker]);
                }
                Step();
                FailIf(_entities.InteractionSlot(levers[0]) != 2 || levers[0].Visible,
                    "An earlier free slot must become the new parent, deferring its initialization until next pass.");
                Step();
                FailIf(!levers[0].Visible,"The swapped parent must initialize next update without allocating a second child.");
            }
            Step(24,angle:0); Step(1,true); Step(1,true);
            FailIf(!lever.Grabbed,"D6 Bracelet approach must grab the selected lever through actual collision.");
            int remaining = 100;
            while ((lever.PullDistance&0x80) == 0 && remaining-- > 0) Step(1,true,16);
            FailIf(lever.PullDistance != 0x88,"D6 lever length selector must publish $08 with bit7.");
            Step(1,true);
            if (opened)
            {
                FailIf(!controller.Finished || _entities.ActiveTriggers != 1 ||
                    _entities.Entities<RopeCharacter>().Count != 0 || sounds.Requests.Any(cue => cue is SoundId.SndError or SoundId.SndSolvePuzzle),
                    "Opened boss-key room must still await a full lever, release the whole trigger byte and delete without failure/chest effects.");
                Step(); Step(50); Step(24,angle:0); Step(1,true); Step(1,true); Step(30,true,16);
                continue;
            }
            FailIf(controller.State != 2 || _entities.Entities<RopeCharacter>().Count != 4 || rom[p+7] != 1,
                "First full pull must fail without chance RNG and allocate four falling Ropes.");
            _dialogue.ShowGameplayMessage("Lever failure pause",120); rom[0xcba0] = 1;
            Step(4,true); _dialogue.Close(); rom[0xcba0] = 0;
            Step(3,true);
            FailIf(controller.State != 2,"A held full lever must wait for all counted enemies.");
            // Leave one counted Rope: partial defeat cannot retry the chance.
            var survivor = active.First(actor => actor.Node is RopeCharacter);
            foreach (var actor in active.Where(actor => actor.Node is RopeCharacter && actor != survivor).ToArray())
            {
                var slots = SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_enemySlots");
                int a = 0xd080+slots[actor]*256;
                for (int offset = 0; offset < 0x40; offset++) rom[a+offset] = 0;
                active.Remove(actor); free.Invoke(_entities,[actor]);
            }
            rom[0xcdd1] = 1; Step(3,true);
            FailIf(controller.State != 2,"The last counted Rope must retain the failed-pull wait.");
            ClearWave(); Step(1,true);
            FailIf(controller.State != 1,"Final counted completion returns to waiting without testing chance in the same update.");
            // Native RNG decides subsequent retries; no seed forcing.
            for (int attempt = 0; !controller.Finished && attempt < 32; attempt++)
            {
                Step(1,true);
                if (controller.State == 2) { Step(2,true); ClearWave(); Step(1,true); }
                else if (controller.State == 3)
                {
                    FailIf(rom[0xcf17] == 0xf1,"Successful chance must defer chest creation to the next update.");
                    Step(1,true);
                }
            }
            FailIf(!controller.Finished || rom[0xcf17] != 0xf1 || _entities.ActiveTriggers != 1,
                "Boss-key success must create the original chest and replace all active trigger bits.");
            Step(); Step(50);
            FailIf(lever.PullDistance != 0,"Released short lever must retract before regrabbing.");
            Step(24,angle:0); Step(1,true); Step(1,true); Step(30,true,16); Step(1,true);
            FailIf(sounds.Requests.Count(cue => cue == SoundId.SndSolvePuzzle) != 1,
                "Pulling again after completion must not recreate the chest or rerun chance RNG.");
        }
        GD.Print("Validated clean-US D6 two short Bracelet levers, actual approach/regrab, first failure/four falling Ropes, shared RNG/placement/exclusion, text/count handoff, deferred chest and completed repeat under split/batched gameplay.");
    }
}
