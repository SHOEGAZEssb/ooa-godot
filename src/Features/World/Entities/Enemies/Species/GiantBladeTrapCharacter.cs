using Godot;
using System;

namespace oracleofages;

// giantBladeTrap.s $2a:$03 accelerates along walls, choosing a counterclockwise
// turn, then its reverse, then forward when the first two directions are solid.
internal sealed partial class GiantBladeTrapCharacter : EnemyCharacter
{
    private readonly GiantBladeTrapBehaviorProfile _behavior = EnemyBehaviorTables.Shared.GiantBladeTrap;
    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
    internal ImportedEnemyDefinition Record { get; private set; }
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    internal int Angle { get; private set; }
    internal int Speed { get; private set; }
    internal override bool InitializationPending => State == 0;

    internal void Initialize(ImportedEnemyDefinition record, OracleRoomData room,
        Vector2 position, OracleRandom random)
    {
        if (record is not { Id: EnemyId.GiantBladeTrap, SubId: 3 })
            throw new ArgumentException("giantBladeTrap.s requires imported $2a:$03.", nameof(record));
        Record = record;
        _room = room;
        _random = random;
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(record));
        Visible = false;
    }

    internal void UpdateFrame()
    {
        // enemyCode2a returns only for status $01/$02; JUST_HIT and recoil
        // continue its AI. Ordinary collision rows supply no target recoil.
        if (BeginFrame(continueDuringHitAndKnockback: true)) return;
        switch (State)
        {
            case 0:
                _random.Next(); // enemyStandardUpdate's common var3d byte
                // rst_jumpTable leaves this routine's low address byte in A
                // for ecom_setSpeedAndState8; the first moving update replaces it.
                Speed = _behavior.InitialSpeed;
                State = 8;
                Visible = true;
                ZIndex = ObjectDrawPriority.BehindLinkZIndex;
                return;
            case 8:
                State = 9;
                Angle = _behavior.InitialAngle;
                Counter = _behavior.AccelerationUpdates;
                return;
            case 9:
                if (Counter != 0) Speed = _behavior.Speeds[(--Counter & 0xf0) >> 4].Value;
                if (!Blocked())
                    Position += MovementDelta(Speed, Angle);
                else
                {
                    State = 10;
                    // The source rounds only high bytes; retain fractions.
                    Position = new(RoundHigh(Position.X), RoundHigh(Position.Y));
                    Counter = _behavior.TurnWaitUpdates;
                }
                QueueRedraw();
                return;
            case 10:
                Counter = (Counter - 1) & 0xff;
                if (Counter != 0) return;
                Angle = (Angle - 8) & 0x1f;
                if (Blocked())
                {
                    Angle ^= 0x10;
                    if (Blocked()) Angle = (Angle + 8) & 0x1f;
                }
                State = 9;
                Counter = _behavior.AccelerationUpdates;
                return;
            default:
                throw new NotSupportedException($"giantBladeTrap.s $2a:$03 state ${State:x2} is not represented.");
        }
    }

    private static float RoundHigh(float value)
    {
        int high = Mathf.FloorToInt(value);
        return ((high + 2) & 0xf8) + value - high;
    }

    private bool Blocked()
    {
        // Two raw collision bytes ahead, indexed by angle/2, rather than
        // quadrant collision or the small trap's generic adjacent probes.
        for (int probe = 0; probe < 2; probe++)
        {
            int index = (Angle >> 1) + probe * 2;
            int y = (Mathf.FloorToInt(Position.Y) + _behavior.ProbeOffsets[index].Value) & 0xff;
            int x = (Mathf.FloorToInt(Position.X) + _behavior.ProbeOffsets[index + 1].Value) & 0xff;
            if (x >= _room.Width || y >= _room.Height ||
                _room.GetTerrainInfo(new Vector2(x, y)).Collision != 0) return true;
        }
        return false;
    }

    internal override bool TakeSwordHit(Vector2 _, int __) => AcceptArmoredSwordHit(_behavior.SwordInvincibility);
    internal override bool TakeBurnHit(int _) => false;
    internal bool TakeDeflectionHit() => AcceptArmoredSwordHit(20);
}
