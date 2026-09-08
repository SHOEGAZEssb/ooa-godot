using Godot;

namespace oracleofages;

// enemyCode21 shares moblin_state_8 and the alternating arrow routine.
internal sealed partial class ArrowDarknutCharacter : ArrowMoblinCharacter
{
    protected override bool FollowsScentSeeds => false;
    protected override bool SupportsRecord(ImportedEnemyDefinition record) =>
        record.Id == 0x21 && record.SubId is 0 or 1;

    protected override int ChooseRouteAngle(OracleRandom random, Vector2 target) =>
        (random.Next().Value & EnemyBehaviorTables.Shared.ArrowDarknutDirectionMask[0].Value) == 0
            ? (OracleObjectMovement.Shared.RelativeAngle(Position, target) + 4) & 0x18
            : random.NextCardinalAngle();
}
