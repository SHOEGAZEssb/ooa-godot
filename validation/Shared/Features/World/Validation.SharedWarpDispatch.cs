using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedWarpDispatch(FrontendValidationRoot root, bool batched)
    {
        var engine = root._gameplaySession!;
        var rooms = engine.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player;
        var transitions = engine.Transitions;
        var app = new ApplicationValidationFixture(root);
        OracleGame game = rooms.SaveData.Game;
        entities.Clear(); player.Inventory.EquipA(0); player.Inventory.EquipB(0);
        var floor = Enumerable.Range(0,80).Select(index => new Vector2(index % 10 * 16 + 8,index / 10 * 16 + 8))
            .First(point => point.X is >= 24 and <= 136 && point.Y is >= 24 and <= 104 &&
                !player.World.Collides(point) && !player.World.Collides(point - new Vector2(8,0)) &&
                player.World.GetActiveTerrain(point).Terrain.Hazard == HazardType.None &&
                player.World.GetActiveTerrain(point - new Vector2(8,0)).Terrain.Hazard == HazardType.None);
        int packed = rooms.CurrentRoom.GetPackedPosition(floor);
        foreach (var example in new (int Mode,int Parameter)[] { (0,0),(1,0),(4,0),(0x0a,0),(0x0c,4),(0x0c,7),(0x0d,0),(0x0e,0),(0x0f,0),(7,0) })
        {
            if (example.Mode == 7 && game == OracleGame.Seasons) continue;
            entities.Clear(); player.WarpTo(floor); player.Face(Vector2I.Left);
            rooms.SaveData.SetDeathRespawnPoint(rooms.ActiveGroup,rooms.CurrentRoom.Id,0,3,24,24);
            byte[] beforeCheckpoint = Enumerable.Range(0,6).Select(offset => rooms.SaveData.ReadWramByte(0xc62b + offset)).ToArray();
            var warp = new Warp(rooms.ActiveGroup,rooms.CurrentRoom.Id,packed,0,0,
                rooms.ActiveGroup,rooms.CurrentRoom.Id,packed,example.Mode == 0x0c ? 2 : example.Parameter,example.Mode);
            transitions.ApplyWarp(player,warp);
            entities.Clear();
            FailIf(player.FacingVector != Vector2I.Up || player.PrecisePosition != floor ||
                !beforeCheckpoint.SequenceEqual(Enumerable.Range(0,6).Select(offset => rooms.SaveData.ReadWramByte(0xc62b + offset))),
                $"{game}: mode 0x{example.Mode:x2} must not run Link's facing/shift/checkpoint handler during room loading.");
            // Declared post-load control write tests retaining the live facing,
            // independently of clearWramBank1's initial DIR_UP.
            player.Face(Vector2I.Left);
            rooms.RuntimeState.SetField("wcc50",(byte)example.Parameter);
            var native = new BraceletRom(rooms.CurrentRoom,floor,3,0xff,game:game);
            native.LoadSave(rooms.SaveData);
            native[0xcc2d] = (byte)rooms.ActiveGroup; native[0xcc32] = rooms.RuntimeState.ReadField("wRoomStateModifier");
            native.InitializeWarpArrival(example.Mode,example.Parameter);
            int probes = 0;
            entities.AddEntity(new ItemPhaseValidationEntity(() =>
            {
                if (probes++ != 0) return;
                FailIf(player.FacingVector != (Vector2I)OracleObjectMath.StrictCardinalVector(native[0xd008] * 8) ||
                    player.PrecisePosition != new Vector2(native.Word(0xd00c) / 256f,native.Word(0xd00a) / 256f),
                    $"{game}: mode 0x{example.Mode:x2} initializer must run in Link's phase before destination actors.");
            }));
            app.Step(1,Vector2.Zero,batched:batched);
            FailIf(!Enumerable.Range(0,6).All(offset => rooms.SaveData.ReadWramByte(0xc62b + offset) == native[0xc62b + offset]),
                $"{game}: mode 0x{example.Mode:x2} must publish its native checkpoint on the first Link dispatch.");
            if (example.Mode is 1 or 0x0e)
                player.SetScriptedPosition(player.PrecisePosition + Vector2.Down); // Declared post-initialization actor displacement.
            app.Step(32,Vector2.Zero,batched:batched);
            FailIf(transitions.IsTransitioning || probes != 33 ||
                !Enumerable.Range(0,6).All(offset => rooms.SaveData.ReadWramByte(0xc62b + offset) == native[0xc62b + offset]) ||
                example.Mode == 0x0f && player.Visible,
                $"{game}: completion must preserve native standing/hidden state and must not record a second displaced checkpoint.");
        }
        foreach (var example in new (int Source,int Arrival,string Handler)[] { (0,6,game == OracleGame.Ages ? "TimeWarp" : "Trampoline"),
            (0,0x0b,"SlowFall"),(8,0,"Subrosia"),(9,0,"Hole"),(0,2,"FadeOut") })
        {
            entities.Clear(); player.WarpTo(floor);
            root._scene.Dialogue.ShowMessage("Unsupported native sequence boundary.",floor.Y);
            var before = rooms.RuntimeState.CaptureState().Wram;
            int rng = entities.Random.Calls;
            bool rejected = false;
            try { transitions.ApplyWarp(player,new Warp(rooms.ActiveGroup,rooms.CurrentRoom.Id,packed,0,example.Source,
                rooms.ActiveGroup,rooms.CurrentRoom.Id,packed,0,example.Arrival)); }
            catch (NotSupportedException error) when (error.Message.Contains(example.Handler,StringComparison.Ordinal) &&
                error.Message.Contains("linkState0a",StringComparison.Ordinal)) { rejected = true; }
            FailIf(!rejected || transitions.IsTransitioning || !root._scene.Dialogue.IsOpen || entities.Random.Calls != rng ||
                !before.AsSpan().SequenceEqual(rooms.RuntimeState.CaptureState().Wram),
                $"{game}: native {example.Handler} must diagnose before mutating the ordinary room/Link/dialogue owner.");
            root._scene.Dialogue.Close();
        }
        entities.Clear(); player.WarpTo(floor);
    }
}
