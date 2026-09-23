using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class SmasherBehaviorProfile(
    IReadOnlyList<EnemyBehaviorValue> state,
    IReadOnlyList<EnemyBehaviorValue> death,
    IReadOnlyList<EnemyBehaviorValue> dropWallProbes,
    IReadOnlyList<EnemyBehaviorValue> respawnPositions,
    IReadOnlyList<EnemyBehaviorValue> wanderAngles,
    IReadOnlyList<EnemyBehaviorValue> parentEffects,
    IReadOnlyList<EnemyBehaviorValue> ballEffects,
    IReadOnlyList<EnemyBehaviorValue> activeCollisions)
{
    internal IReadOnlyList<EnemyBehaviorValue> DropWallProbes => dropWallProbes;
    internal int BounceDroppedBall(Vector2 position, System.Func<Vector2, bool> collides)
    {
        int y = (byte)(int)position.Y, x = (byte)(int)position.X, walls = 0;
        for (int i = 0; i < 4; i++)
        {
            y = (byte)(y + dropWallProbes[i * 2].Value);
            x = (byte)(x + dropWallProbes[i * 2 + 1].Value);
            walls = (walls << 1) | (collides(new(x,y)) ? 1 : 0);
        }
        bool horizontal = (walls & 3) != 0, vertical = (walls & 12) != 0;
        return horizontal && vertical ? 0x0f : horizontal ? dropWallProbes[9].Value :
            vertical ? dropWallProbes[8].Value : 0xff;
    }
    internal int DeathFrames => death[0].Value;
    internal IReadOnlyList<EnemyBehaviorValue> ParentEffects => parentEffects;
    internal IReadOnlyList<EnemyBehaviorValue> BallEffects => ballEffects;
    internal IReadOnlyList<EnemyBehaviorValue> ActiveCollisions => activeCollisions;
    internal int ExpirationEvenTicks => state[0].Value;
    internal int RespawnFrames => state[1].Value;
    internal int RespawnZ => state[2].Value;
    internal int Gravity => state[3].Value;
    internal int WanderFrames => state[4].Value;
    internal int CarryFrames => state[5].Value;
    internal int HopSpeedZ => state[6].Value;
    internal int ThrowJumpSpeedZ => state[7].Value;
    internal int PickupOffsetX => state[8].Value;
    internal int PickupMinimumX => state[9].Value;
    internal int PickupSpanX => state[10].Value;
    internal int ArrivalBias => state[11].Value;
    internal int ArrivalSpan => state[12].Value;
    internal int CarriedZOffset => state[13].Value;
    internal int LiftSpeedZ => state[14].Value;
    internal int InitialBallOffsetX => state[15].Value;
    internal int HitInvincibility => state[16].Value;
    internal int HitKnockback => state[17].Value;
    internal int HitZBias => state[18].Value;
    internal int HitZSpan => state[19].Value;
    internal Vector2 RespawnPosition(byte random)
    {
        int offset = random & 0x0e;
        return new(respawnPositions[offset + 1].Value, respawnPositions[offset].Value);
    }
    internal int WanderAngle(byte random) => wanderAngles[random & 3].Value;
}
