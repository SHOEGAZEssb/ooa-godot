using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSeasonsSword(ApplicationValidationFixture app, SeasonsOpeningSession session, bool batched)
    {
        Vector2? target = null;
        Vector2 far = default;
        // Native $c4 is the ordinary bush (mode$01, L1 source enabled,
        // replacement$04, drop table$81). Approach from dry floor on its right.
        for (int room = 0; room < 256 && target is null; room++)
        {
            session.LoadRoom(room,0,spawn:false);
            for (int y = 24; y < 104 && target is null; y += 16)
            for (int x = 24; x < 104; x += 16)
            {
                Vector2 bush = new(x,y), start = bush + Vector2.Right * 32;
                if (session.Room.GetMetatile(bush) != 0xc4) continue;
                bool clear = true;
                for (int step = 0; step <= 16; step++)
                    clear &= !session.Collision.Collides(start - Vector2.Right * step) &&
                        session.Link.World.GetActiveTerrain(start - Vector2.Right * step).Terrain.Hazard == HazardType.None;
                if (clear) { target = bush; far = start; break; }
            }
        }
        FailIf(target is null,"Seasons Sword fixture needs an unchanged native $c4 bush with a dry approach.");
        session.Inventory.GiveTreasure(TreasureId.Sword,1);
        session.Inventory.EquipA(TreasureId.Sword);
        session.Link.WarpTo(far);
        for (int update = 0; update < 24 && session.Link.Position.X > target!.Value.X + 16; update++)
            app.Step(1,Vector2.Left,batched:batched);
        app.Step(1,Vector2.Zero,batched:batched);
        session.Link.Face(Vector2I.Left);
        FailIf(session.Collision.Collides(session.Link.Position) || session.Link.Position.DistanceTo(target!.Value) > 17,
            "The Seasons Sword regression must reach the bush through its actual collision geometry.");
        var audit = session.Sound.AttachPlayRequestAudit();
        // Native child setup draws once for the slash cue, then drop table$81
        // uses probability2 and set1. Choose a valid source roll yielding a heart.
        byte[] probability = [0x08,0x10,0x08,0x20,0x80,0x01,0x80,0x10];
        bool found = false;
        for (int attempt = 0; attempt < 4096; attempt++)
        {
            var probe = new OracleRandom(); probe.RestoreState(session.Random.CaptureState());
            probe.Next(); int roll = probe.Next().Value & 63;
            if ((probability[roll / 8] & (1 << (roll & 7))) != 0 && (probe.Next().Value & 31) < 12) { found = true; break; }
            session.Random.Next();
        }
        FailIf(!found,"Native RNG setup did not find a Sword/drop boundary fixture.");
        int calls = session.Random.Calls;
        app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched);
        app.Step(16,Vector2.Zero,batched:batched);
        FailIf(session.Room.GetMetatile(target.Value) != 0x04 || session.Room.GetUnderlyingMetatile(target.Value) != 0x04 ||
            session.Entities.Entities<GrassDebrisEffect>().Count != 1 || audit.RequestsFor(SoundId.SndCutGrass) != 1 ||
            session.Random.Calls != calls + 3 || !session.Entities.Entities<ItemDropEffect>().Any(drop => drop.SubId == 1),
            $"Seasons real Sword input must use shared native combat, replacement, debris and ordered drop RNG: " +
            $"tile=${session.Room.GetMetatile(target.Value):x2}, rng={session.Random.Calls-calls}, grass={session.Entities.Entities<GrassDebrisEffect>().Count}, drops={session.Entities.Entities<ItemDropEffect>().Count}.");
        session.Entities.Clear();
        session.LoadRoom(0xb6,0,spawn:false);
        session.Link.WarpTo(new(0x48,0x78));
    }
}
