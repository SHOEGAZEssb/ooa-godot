using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
public readonly record struct ChannelStart(int Channel, int Priority, int Bank, int Offset);
