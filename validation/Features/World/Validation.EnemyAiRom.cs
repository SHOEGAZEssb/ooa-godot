using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateEnemyAiRom()
    {
        LoadValidationRoom(0, 0x33);
        OracleRoomData room = _rooms.CurrentRoom;
        var database = new EnemyDatabase();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (int id in new[] { 0x08, 0x18 })
        foreach (int initialSeed in new[] { 0x0d37, 0x1234, 0x80ff, 0xffff })
        {
            var rom = new EnemyAiRom();
            var random = new OracleRandom();
            random.RestoreState(random.CaptureState() with { Rng1 = (byte)initialSeed, Rng2 = (byte)(initialSeed >> 8) });
            OracleRandomState seed = random.CaptureState();
            rom[0xff94] = seed.Rng1;
            rom[0xff95] = seed.Rng2;
            rom[0xd080] = 1;
            rom[0xd081] = (byte)id;
            rom.Word(0xd08a, 0x4880);
            rom.Word(0xd08c, 0x5880);
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
            {
                Vector2 point = new(x * 16 + 8, y * 16 + 8);
                rom[0xcf00 + y * 16 + x] = room.GetMetatile(point);
                rom[0xce00 + y * 16 + x] = (byte)room.GetTerrainInfo(point).Collision;
            }
            rom[0xcc33] = (byte)room.ActiveCollisions;
            EnemyCharacter enemy;
            if (id == 8)
            {
                var zora = new RiverZoraCharacter();
                zora.Initialize(database.ImportedEnemy(id), room, new(88.5f, 72.5f), random);
                enemy = zora;
            }
            else
            {
                var blob = new BuzzBlobCharacter();
                blob.Initialize(database.ImportedEnemy(id), room, new(88.5f, 72.5f), random);
                enemy = blob;
            }
            var spawns = new List<RoomEntitySpawn>();
            int shots = 0, splashes = 0;
            try
            {
                for (int update = 0; update < 600; update++)
                {
                    rom[0xcc00] = (byte)update;
                    spawns.Clear();
                    for (int slot = 0; slot < 16; slot++)
                    for (int offset = 0; offset < 0x40; offset++) rom[0xd0c0 + slot * 0x100 + offset] = 0;
                    for (int slot = 2; slot < 16; slot++)
                    for (int offset = 0; offset < 0x40; offset++) rom[0xd040 + slot * 0x100 + offset] = 0;
                    if (id == 0x18 && update == 160)
                    {
                        FailIf(!((BuzzBlobCharacter)enemy).BeginShock(), "Buzz Blob shock fixture was not eligible.");
                        rom[0xd0aa] = 0xa0; // collisionEffect36 -> ELECTRIC_SHOCK | $80
                        rom[0xd0a4] &= 0x7f;
                    }
                    Vector2? scent = id == 0x18 && update is >= 320 and < 355 ? new Vector2(112, 56) : null;
                    rom[0xccd9] = (byte)(scent.HasValue ? 1 : 0);
                    rom[0xffb2] = 56;
                    rom[0xffb3] = 112;
                    rom.Update();
                    if (enemy is RiverZoraCharacter zora) zora.UpdateFrame(Vector2.Zero, spawns);
                    else ((BuzzBlobCharacter)enemy).UpdateFrame(scent);
                    int state = enemy is RiverZoraCharacter z ? z.State : ((BuzzBlobCharacter)enemy).State;
                    int counter = enemy is RiverZoraCharacter rz ? rz.Counter : ((BuzzBlobCharacter)enemy).Counter;
                    OracleRandomState actual = random.CaptureState();
                    FailIf(state != rom[0xd084] || counter != rom[0xd086] ||
                        actual.Rng1 != rom[0xff94] || actual.Rng2 != rom[0xff95] || random.Calls != rom.RandomCalls ||
                        enemy.Position != new Vector2(rom.Word(0xd08c) / 256.0f, rom.Word(0xd08a) / 256.0f),
                        $"Enemy ${id:x2} update {update}: ROM state/counter=${rom[0xd084]:x2}/${rom[0xd086]:x2}, " +
                        $"XY=${rom.Word(0xd08c):x4}/${rom.Word(0xd08a):x4}, RNG=${rom[0xff95]:x2}{rom[0xff94]:x2}/{rom.RandomCalls}; " +
                        $"runtime=${state:x2}/${counter:x2}, XY={enemy.Position}, RNG=${actual.Rng2:x2}{actual.Rng1:x2}/{random.Calls}.");
                    int animationCounter = (int)typeof(EnemyAnimationPlayer).GetField("_frameCounter", flags)!.GetValue(enemy.Animation)!;
                    FailIf(animationCounter != rom[0xd0a0] || enemy.AnimationParameter != rom[0xd0a1] ||
                        enemy.Visible != ((rom[0xd09a] & 0x80) != 0) || enemy.CollisionEnabled != ((rom[0xd0a4] & 0x80) != 0),
                        $"Enemy ${id:x2} update {update}, seed=${initialSeed:x4}: ROM animation timer/parameter=${rom[0xd0a0]:x2}/${rom[0xd0a1]:x2}, " +
                        $"visible=${rom[0xd09a]:x2}, collision=${rom[0xd0a4]:x2}; runtime=${animationCounter:x2}/${enemy.AnimationParameter:x2}, visible={enemy.Visible}, collision={enemy.CollisionEnabled}.");
                    if (enemy is BuzzBlobCharacter buzz)
                        FailIf(buzz.Angle != rom[0xd089], $"Buzz Blob angle mismatch on update {update}, seed=${initialSeed:x4}.");
                    int fire = spawns.OfType<ZoraFireSpawn>().Count();
                    int splash = spawns.OfType<EnemySplashSpawn>().Count();
                    FailIf(fire != (rom[0xd0c0] == 0 ? 0 : 1) || splash != (rom[0xd240] == 0 ? 0 : 1),
                        $"Enemy ${id:x2} update {update}: projectile/splash allocation diverged.");
                    if (fire != 0)
                        FailIf(rom[0xd0c1] != 0x19 || rom[0xd0c2] != 1 ||
                            spawns.OfType<ZoraFireSpawn>().Single().Position != new Vector2(rom.Word(0xd0cc) / 256.0f, rom.Word(0xd0ca) / 256.0f),
                            "Zora projectile must be PART $19:$01 at the source position.");
                    shots += fire;
                    splashes += splash;
                }
                FailIf(id == 8 && (shots == 0 || splashes == 0), "River Zora ROM trace did not reach shooting and disappearance.");
            }
            finally { enemy.Free(); }
        }
        GD.Print("Validated River Zora and Buzz Blob updates against executed clean-US updateEnemies.");
    }
}
