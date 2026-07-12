using Godot;
using System.Collections.Generic;

namespace oracleofages;

public sealed class RoomEntityManager
{
    private readonly Node _worldRoot;
    private readonly NpcDatabase _npcs;
    private readonly List<NpcCharacter> _npcNodes = new();

    public List<NpcCharacter> Npcs => _npcNodes;

    public RoomEntityManager(Node worldRoot, NpcDatabase npcs)
    {
        _worldRoot = worldRoot;
        _npcs = npcs;
    }

    public void LoadRoom(int group, OracleRoomData room)
    {
        Clear();
        foreach (NpcDatabase.NpcRecord record in _npcs.GetRoomNpcs(group, room.Id))
        {
            var npc = new NpcCharacter
            {
                Name = $"Npc_{record.Id:x2}_{record.SubId:x2}",
                ZIndex = 9
            };
            npc.Initialize(record);
            _npcNodes.Add(npc);
            _worldRoot.AddChild(npc);
        }
    }

    public void Update(double delta, Vector2 playerPosition)
    {
        foreach (NpcCharacter npc in _npcNodes)
            npc.UpdateNpc(delta, playerPosition);
    }

    public bool BlocksLink(Vector2 linkCenter)
    {
        foreach (NpcCharacter npc in _npcNodes)
        {
            if (npc.BlocksLinkCenter(linkCenter))
                return true;
        }
        return false;
    }

    public NpcCharacter? FindTalkTarget(Player player)
    {
        foreach (NpcCharacter npc in _npcNodes)
        {
            if (npc.CanTalkTo(player))
                return npc;
        }
        return null;
    }

    public void Clear()
    {
        foreach (NpcCharacter npc in _npcNodes)
        {
            _worldRoot.RemoveChild(npc);
            npc.QueueFree();
        }
        _npcNodes.Clear();
    }
}
