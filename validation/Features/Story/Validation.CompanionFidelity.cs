using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void PrepareCompanionFidelityRoom()
    {
        foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
            CompanionRuntimeState.Clear(_runtimeState, id);
        CompanionRuntimeState.ForgetRemembered(_runtimeState);
        LoadValidationRoom(0, 0x2a);
        for (int y = 8; y < 128; y += 16)
        for (int x = 8; x < 160; x += 16)
            _rooms.CurrentRoom.SetPositionTileAndCollision(new Vector2(x, y), 0, 0, 0);
        _player.WarpTo(new Vector2(8, 8));
        _player.SetLocalRespawnCoordinates(new Vector2(40, 40));
        _saveData.WriteWramByte(0xc646, 0x80);
        _saveData.WriteWramByte(0xc647, 0x80);
        _saveData.WriteWramByte(0xc648, 0x80);
    }

    private IRoomEntity SpawnFidelityCompanion(int id, Vector2 position) => id switch
    {
        0x0b => _entities.Spawn<RickyCompanionRoomEntity>(new RickyCompanionSpawn(position, 2, 0, 0x2a)),
        0x0c => _entities.Spawn<DimitriCompanionRoomEntity>(new DimitriCompanionSpawn(position, 2, 0, 0x2a)),
        _ => _entities.Spawn<MooshCompanionRoomEntity>(new MooshCompanionSpawn(position, 2, 0, 0x2a))
    };

    private void ValidateCompanionWaitingFidelity()
    {
        foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
        {
            PrepareCompanionFidelityRoom();
            Vector2 point = new(72, 56);
            IRoomEntity companion = SpawnFidelityCompanion(id, point);
            _player.WarpTo(point);
            _player.ApplyInteractionInvincibility(60);
            for (int frame = 0; frame < 4; frame++) _entities.Update(1.0 / 60, _player);
            FailIf(_player.CompanionJumpActive || ((IPlayerRestriction)companion).DisablesMovement,
                $"Companion ${id:x2} accepted invulnerable Link in companionTryToMount.");
            _player.ResetEnemyInvincibility();
            _player.BeginCarriedObjectPose();
            for (int frame = 0; frame < 4; frame++) _entities.Update(1.0 / 60, _player);
            FailIf(_player.CompanionJumpActive || ((IPlayerRestriction)companion).DisablesMovement,
                $"Companion ${id:x2} accepted Link while carrying an object.");
            _player.EndCarriedObjectPose();
            _entities.Update(1.0 / 60, _player);
            FailIf(!((IPlayerRestriction)companion).DisablesMovement,
                $"Companion ${id:x2} rejected ordinary vulnerable Link.");

            PrepareCompanionFidelityRoom();
            companion = SpawnFidelityCompanion(id, point);
            _rooms.CurrentRoom.SetPositionTileAndCollision(point, 0xf3, 0, 0);
            int health = _player.HealthQuarters;
            for (int frame = 0; frame < 240 && companion.Node.Position != new Vector2(40, 40); frame++)
                _entities.Update(1.0 / 60, _player);
            FailIf(companion.Node.Position != new Vector2(40, 40) ||
                ((IPlayerRideableRoomEntity)companion).LinkRiding || _player.CompanionRideActive ||
                _player.HealthQuarters != health || _player.Position != new Vector2(8, 8),
                $"Unmounted companion ${id:x2} did not respawn alone from a hole.");
        }
        GD.Print("Validated all companions' vulnerable/unencumbered mount gates and unmounted hole respawn without Link damage or ownership transfer.");
    }

    private void ValidateMooshCliffFidelity()
    {
        PrepareCompanionFidelityRoom();
        _rooms.CurrentRoom.SetPositionTileAndCollision(new Vector2(72, 72), 0xd4, 3, 0);
        var moosh = _entities.Spawn<MooshCompanionRoomEntity>(new MooshCompanionSpawn(new Vector2(72, 64), 2, 0, 0x2a, Riding: true));
        CompanionRuntimeState.Begin(_runtimeState, 0x0d, 0x2a, moosh.Position, 2);
        Input.BeginOriginalUpdate(new ApplicationInputSnapshot(pressed: ["move_down"], justPressed: [], movement: Vector2.Down));
        try
        {
            _entities.Update(1.0 / 60, _player);
            _entities.Update(1.0 / 60, _player);
        }
        finally { Input.EndOriginalUpdate(); }
        FailIf(moosh.Phase != MooshCompanionPhase.CliffJump, "Moosh did not enter source state $07 at a downward cliff.");
        Vector2 initial = moosh.Position;
        _sound.ClearPlayRequestAudit();
        for (int frame = 0; frame < 19; frame++) _entities.Update(1.0 / 60, _player);
        FailIf(moosh.Position != initial || _sound.PlayRequestsFor(OracleSoundEngine.SndJump) != 0,
            "Moosh's $14 cliff anticipation moved or sounded early.");
        _entities.Update(1.0 / 60, _player);
        FailIf(moosh.Position != initial || moosh.AnimationIndex != 0x0b || _sound.PlayRequestsFor(OracleSoundEngine.SndJump) != 1,
            "Moosh's zero-counter cliff boundary lost SND_JUMP or animation $0b.");
        _entities.Update(1.0 / 60, _player);
        FailIf(moosh.Position.Y != initial.Y + 2 || moosh.ZFixed >= 0,
            "Moosh's cliff launch lost SPEED_200 or the native vertical arc.");
        for (int frame = 0; frame < 30 && moosh.Phase == MooshCompanionPhase.CliffJump; frame++)
            _entities.Update(1.0 / 60, _player);
        FailIf(moosh.Phase != MooshCompanionPhase.Riding || moosh.ZFixed >= 0,
            "Moosh did not resume state $05 with the unfinished falling arc after clearing the rear wall.");
        GD.Print("Validated Moosh state $07 cliff gate, 20-update anticipation, launch, rear-wall crossing and retained airborne arc.");
    }

    private void ValidateCompanionAttackFidelity()
    {
        PrepareCompanionFidelityRoom();
        var moosh = _entities.Spawn<MooshCompanionRoomEntity>(new MooshCompanionSpawn(new Vector2(72, 56), 1, 0, 0x2a, Riding: true));
        CompanionRuntimeState.Begin(_runtimeState, 0x0d, 0x2a, moosh.Position, 1);
        _rooms.CurrentRoom.SetPositionTileAndCollision(moosh.Position, 0x04, 0, 0);
        Input.BeginOriginalUpdate(new ApplicationInputSnapshot(pressed: ["move_right"], justPressed: [], movement: Vector2.Right));
        try
        {
            _entities.Update(1.0 / 60, _player);
            FailIf(_rooms.CurrentRoom.GetMetatile(moosh.Position) != 0x04,
                "Moosh broke ground on the angle-change yield before movement.");
            _entities.Update(1.0 / 60, _player);
            FailIf(_rooms.CurrentRoom.GetMetatile(moosh.Position) == 0x04,
                "Moosh movement did not invoke companionTryToBreakTileFromMoving.");
        }
        finally { Input.EndOriginalUpdate(); }

        PrepareCompanionFidelityRoom();
        var tornado = _entities.Spawn<RickyTornadoRoomEntity>(new RickyTornadoSpawn(new Vector2(64, 56), 1, 0, 0, 0x2a));
        _rooms.CurrentRoom.SetPositionTileAndCollision(new Vector2(72, 56), 0, 3, 0);
        _entities.Update(1.0 / 60, _player);
        _entities.Update(1.0 / 60, _player);
        FailIf(tornado.Finished, "Ricky's tornado stopped at a partial collision instead of testing the full $0f mask.");
        _rooms.CurrentRoom.SetPositionTileAndCollision(new Vector2(88, 56), 0, 0x0f, 0);
        _entities.Update(1.0 / 60, _player);
        FailIf(!tornado.Finished, "Ricky's tornado did not stop at full collision $0f.");

        PrepareCompanionFidelityRoom();
        var stomp = _entities.Spawn<MooshStompAttackRoomEntity>(new MooshStompAttackSpawn(new Vector2(72, 72), 0, 0x2a));
        _entities.Update(1.0 / 60, _player);
        FailIf(stomp.Position != _player.Position + new Vector2(0, 16), "Moosh ITEM_28 did not calculate position from current Link.");
        for (int frame = 0; frame < 19; frame++) _entities.Update(1.0 / 60, _player);
        FailIf(stomp.Finished, "Moosh ITEM_28 consumed its $14 counter on the initialization update.");
        _entities.Update(1.0 / 60, _player);
        FailIf(!stomp.Finished, "Moosh ITEM_28 exceeded its twenty active updates after initialization.");
        GD.Print("Validated Ricky tornado partial/full collision masks and Moosh ITEM_28 positioning and initialization/lifetime boundary.");
    }
}
