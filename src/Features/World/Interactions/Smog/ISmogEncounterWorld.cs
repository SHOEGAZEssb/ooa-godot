using Godot;

namespace oracleofages;

// Live owners expose native high bytes. Writing these coordinates must retain
// Link's low bytes; this interface does not hold a second copy of player state.
internal interface ISmogEncounterWorld
{
    int RoomFlags { get; }
    bool EntryBusy { get; }
    int EnemyCount { get; }
    Vector2I LinkHighPosition { get; set; }
    byte LinkZHigh { get; set; }
    bool LinkNormal { get; }
    bool LinkInAir { get; }
    void ApplyResetPenalty();
    // INTERAC$33 writes wDisabledObjects=$01 and wMenuDisabled=$01.
    // Enemies, parts and interactions continue to update under this mask.
    void LockLinkAndMenu();
    void EnableLinkCollisionsAndMenu();
    void SetResetFlag(bool enabled);
    void Spawn(SmogEnemySpawn spawn);
    void Puff(Vector2I position);
    byte Collision(int packedPosition);
    byte Tile(Vector2I position);
    bool SetTile(int packedPosition, int tile);
    void MergeClouds(int phase);
    void PlayResetSound();
    void DecrementEnemyCount();
}
