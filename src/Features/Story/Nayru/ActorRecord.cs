using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
public readonly record struct ActorRecord(int Index, int Id, int SubId, int Y, int X, int Var03, string Name, string SpriteName, int TileBase, int Palette, int DefaultAnimation, string[] Animations, int InitialAnimation, string ExtraSprite)
{
    public string Animation(int index)
    {
        if (index < 0 || index >= Animations.Length || string.IsNullOrEmpty(Animations[index]))
            throw new InvalidOperationException($"Initial Nayru actor {Name} ${Id:x2}:${SubId:x2} has no animation ${index:x2}.");
        return Animations[index];
    }

    public NpcRecord ToNpcRecord(int group, int room)
    {
        string animation = Animation(DefaultAnimation);
        return new NpcRecord(group, room, Id, SubId, Y, X, Var03, 0, SpriteName, TileBase, Palette, DefaultAnimation, false, animation, animation, animation, animation, string.Empty);
    }
}
