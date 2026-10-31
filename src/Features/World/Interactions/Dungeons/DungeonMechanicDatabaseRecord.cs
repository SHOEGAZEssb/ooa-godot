namespace Oracles;

internal readonly record struct DungeonMechanicDatabaseRecord(int Group, int Room, int Order, int Id, int SubId, int PackedPosition, int Parameter, TriggerPredicate Predicate, bool CountSourceComplete);

internal enum TriggerPredicate
{
    None,
    BitSet,
    Exact
}
