using Godot;
using System;

namespace oracleofages;
public readonly record struct PushableTileRecord(byte InteractionParameter, byte SourceReplacement, byte DestinationTile, byte PropertyFlags)
{
    public bool RequiresBracelet => (InteractionParameter & 0x40) != 0;
    public bool AllowsEveryDirection => (InteractionParameter & 0x80) != 0;
    public int RequiredDirection => (InteractionParameter >> 4) & 0x03;
    public bool Heavy => (PropertyFlags & 0x20) != 0;
    public bool PlaysSecretSound => (PropertyFlags & 0x80) != 0;
}
