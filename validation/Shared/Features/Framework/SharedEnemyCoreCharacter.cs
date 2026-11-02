
namespace Oracles;

// Exercise record-neutral enemy mechanics without importing a species bundle.
internal sealed partial class SharedEnemyCoreCharacter : EnemyCharacter
{
    internal override bool InitializationPending => false;
    internal int Updates { get; private set; }
    private bool _hazards;

    internal void Initialize(string assetRoot, OracleRoomData room, Vector2 position, bool hazards = false)
    {
        InitializeEnemy(position,EnemyCharacterConfiguration.FromSprite(assetRoot,10,6,6,
            "spr_common_sprites",["127@8,4,0,0"],0,2));
        ConfigureSwordKnockback(room,EnemyKnockbackMotion.ScreenBoundary);
        _hazards = hazards;
        if (hazards) ConfigureHazards(room);
    }

    internal void SetHighRecoil()
    {
        KnockbackCounter = 0x89;
        KnockbackAngle = 8;
    }

    internal void Advance()
    {
        Updates++;
        if (!BeginFrame() && _hazards) CheckHazards();
    }
}
