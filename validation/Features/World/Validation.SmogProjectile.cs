using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSmogProjectile()
    {
        var data = new SmogProjectileDatabase();
        for (int collision = 0; collision < 32; collision++)
            FailIf(data.Enabled(collision) != (collision is 0 or 4 or 5 or 6) ||
                (data.Enabled(collision) && (data.Effect(0, collision) != (collision == 0 ? 2 : 0x1f) ||
                data.Effect(1, collision) != (collision == 0 ? 2 : 0))),
                $"PART$4a source mask/mode06/mode04 mismatch at collision${collision:x2}.");
        FailIf(data.Sprite != "spr_smog_projectiles" || data.TileBase != 0 || data.Palette != 4 ||
            data.Radius != new Vector2I(4,4) || data.RawDamage != 0xfe || data.Health != 1 || data.InitialCollisionMode != 0x86,
            "PART$4a must retain source partData $c7,$86,$44,$fe,$01,$00,$04,$00.");
        var intro = OracleGraphicsCache.GetAnimationDefinition(data.Animations[0]);
        var destroy = OracleGraphicsCache.GetAnimationDefinition(data.Animations[1]);
        FailIf(intro.Frames.Length != 6 || intro.LoopStart != 2 || intro.Frames.Any(frame => frame.Duration != 6 || frame.Parameter != 0) ||
            destroy.Frames.Length != 3 || destroy.Frames[0].Duration != 6 || destroy.Frames[1].Duration != 6 ||
            destroy.Frames[2].Duration != 127 || destroy.Frames[2].Parameter != 0xff,
            "Smog projectile intro must loop at frame2; destruction reaches its $ff parameter after two six-update frames.");
        Vector2I[] offsets = [new(0,-5),new(4,-5),new(4,0),new(4,4),new(0,4),new(-5,4),new(-5,0),new(-5,-5)];
        for (int angle = ObjectAngle.Up; angle < 32; angle++)
            FailIf(data.FrontOffset(angle) != offsets[((angle + 2) & 0x1c) >> 2], "PART$4a must use the source eight-way rounded front probe.");
        var room = Room060MovementFixture();
        for (int y = 8; y < 128; y += 16)
            for (int x = 8; x < 160; x += 16) room.SetPositionTileAndCollision(new(x,y),0x0c,0,0);
        var small = new SmogProjectilePart(data, room, new(40,40), 0);
        try
        {
            for (int tick = 1; tick <= 60; tick++)
            {
                small.UpdateFrame(new(140,40), Vector2I.Zero, 0, 2);
                int frame = tick < 36 ? tick / 6 : 2 + ((tick - 36) / 6) % 4;
                FailIf(small.Position != new Vector2(40 + (tick * 3 / 4),40) || small.AnimationFrame != frame ||
                    small.State != 1 || small.CollisionMode != 6 || !small.CollisionEnabled,
                    $"Small Smog projectile lost first-update movement/SPEED_c0 or intro-loop timing at update{tick}.");
            }
            small.PublishCollision(1);
            small.UpdateFrame(new(0,0), Vector2I.Zero, 0, 2);
            Vector2 stopped = small.Position;
            FailIf(small.State != 2 || small.CollisionEnabled || small.AnimationIndex != 1,
                "Small projectile must move before consuming retained contact and starting destruction.");
            for (int i = 0; i < 10; i++) small.UpdateFrame(Vector2.Zero,Vector2I.Zero,0x40,1);
            FailIf(small.Finished || small.Position != stopped, "Destruction state ignores boss/count gates and remains still through update11.");
            small.UpdateFrame(Vector2.Zero,Vector2I.Zero,0,2);
            FailIf(!small.Finished, "Small projectile destruction must finish on update12.");
        }
        finally { small.Free(); }
        foreach (int subid in new[] { 0, 1 })
        {
            // Raw collision byte $01 must obstruct the small projectile even
            // when a quadrant-aware Link collision test could pass here.
            room.SetPositionTileAndCollision(new(44,40),0x0c,1,0);
            var shot = new SmogProjectilePart(data,room,new(40,40),subid);
            try
            {
                shot.UpdateFrame(new(140,40),Vector2I.Zero,0,2);
                FailIf(shot.State != (subid == 0 ? 2 : 1) || shot.Position.X != (subid == 0 ? 40 : 41),
                    "Only small Smog projectiles check raw front collision after moving.");
                if (subid != 0)
                {
                    shot.PublishCollision(0x84);
                    shot.ClearHealthAndCollision();
                    for (int i = 0; i < 140; i++) shot.UpdateFrame(Vector2.Zero,new(32,0),0,0);
                    FailIf(shot.Finished || shot.State != 1 || shot.AnimationIndex != 3 || shot.AnimationFrame != 0 || shot.CollisionMode != 4,
                        "Large projectile ignores wall/contact/health status, keeps its directional pose, and does not delete for enemy count0.");
                }
            }
            finally { shot.Free(); }
        }
        foreach (var (position, inside) in new[] { (new Vector2(249,0),true),(new Vector2(248,0),false),
            (new Vector2(167,0),true),(new Vector2(168,0),false),(new Vector2(0,135),true),(new Vector2(0,136),false) })
        foreach (var camera in new[] { Vector2I.Zero, new Vector2I(32,16) })
        {
            var point = new Vector2((byte)((int)position.X+camera.X),(byte)((int)position.Y+camera.Y));
            var shot = new SmogProjectilePart(data,room,point,1);
            try
            {
                shot.UpdateFrame(point+Vector2.Down*20,camera,0x80,2);
                FailIf(shot.Finished == inside, "Smog uses camera-relative byte bounds [-7,167]x[-7,135], and roomflag$80 is not its deletion gate.");
            }
            finally { shot.Free(); }
        }
        foreach (var (roomFlags, enemies) in new[] { (0x40,2),(0,1) })
        {
            var shot = new SmogProjectilePart(data,room,new(40,40),1);
            var memory = new OracleRuntimeState();
            shot.BindMovementMemory(memory);
            for (int offset = 0; offset < 4; offset++) memory.SetWramByte(0xcec0 + offset, 0xa5);
            try
            {
                shot.UpdateFrame(new(140,40),Vector2I.Zero,roomFlags,enemies);
                FailIf(!shot.Finished || shot.Position != new Vector2(40,40), "Smog boss/count deletion precedes movement even on state0 fallthrough.");
                FailIf(Enumerable.Range(0, 4).Any(offset => memory.ReadWramByte(0xcec0 + offset) != 0xa5),
                    "Smog state0 aim/animation setup and early deletion must not execute the velocity helper.");
            }
            finally { shot.Free(); }
        }
        GD.Print("Validated isolated Smog projectile attributes, intro/destruction timing, movement, collision probes and camera/boss/count gates.");
    }
}
