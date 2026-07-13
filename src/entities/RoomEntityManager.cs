using Godot;
using System.Collections.Generic;

namespace oracleofages;

public sealed class RoomEntityManager
{
    private readonly Node _worldRoot;
    private readonly NpcDatabase _npcs;
    private readonly List<NpcCharacter> _npcNodes = new();
    private readonly List<NpcCharacter> _outgoingNpcNodes = new();
    private bool _screenTransitionActive;

    public List<NpcCharacter> Npcs => _npcNodes;
    public IReadOnlyList<NpcCharacter> OutgoingNpcs => _outgoingNpcNodes;
    public bool ScreenTransitionActive => _screenTransitionActive;

    public RoomEntityManager(Node worldRoot, NpcDatabase npcs)
    {
        _worldRoot = worldRoot;
        _npcs = npcs;
    }

    public void LoadRoom(int group, OracleRoomData room)
    {
        Clear();
        SpawnRoomNpcs(group, room);
    }

    public void BeginScreenTransition(int group, OracleRoomData room, Vector2 incomingOffset)
    {
        ClearNodes(_outgoingNpcNodes);
        _outgoingNpcNodes.AddRange(_npcNodes);
        _npcNodes.Clear();
        _screenTransitionActive = true;
        SpawnRoomNpcs(group, room);
        SetScreenTransitionOffsets(Vector2.Zero, incomingOffset);
    }

    public void SetScreenTransitionOffsets(Vector2 outgoingOffset, Vector2 incomingOffset)
    {
        if (!_screenTransitionActive)
            return;

        foreach (NpcCharacter npc in _outgoingNpcNodes)
            npc.SetTransitionDrawOffset(outgoingOffset);
        foreach (NpcCharacter npc in _npcNodes)
            npc.SetTransitionDrawOffset(incomingOffset);
    }

    public void FinishScreenTransition()
    {
        if (!_screenTransitionActive)
            return;

        ClearNodes(_outgoingNpcNodes);
        foreach (NpcCharacter npc in _npcNodes)
            npc.SetTransitionDrawOffset(Vector2.Zero);
        _screenTransitionActive = false;
    }

    private void SpawnRoomNpcs(int group, OracleRoomData room)
    {
        foreach (NpcDatabase.NpcRecord record in _npcs.GetRoomNpcs(group, room.Id))
        {
            var npc = new NpcCharacter
            {
                Name = $"Npc_{record.Id:x2}_{record.SubId:x2}",
                ZIndex = NpcCharacter.BehindLinkZIndex
            };
            npc.Initialize(record);
            _npcNodes.Add(npc);
            _worldRoot.AddChild(npc);
        }
    }

    public void Update(double delta, Vector2 playerPosition)
    {
        // The original engine marks outgoing objects as enabled $02 while
        // destination objects initialize as enabled $01. Neither set resumes
        // its normal interaction updates until the scrolling transition ends.
        if (_screenTransitionActive)
            return;

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
        ClearNodes(_outgoingNpcNodes);
        ClearNodes(_npcNodes);
        _screenTransitionActive = false;
    }

    private void ClearNodes(List<NpcCharacter> nodes)
    {
        foreach (NpcCharacter npc in nodes)
        {
            if (npc.GetParent() == _worldRoot)
                _worldRoot.RemoveChild(npc);
            npc.QueueFree();
        }
        nodes.Clear();
    }
}
