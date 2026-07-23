using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
public readonly record struct ImpaStoneEventRecord(ImpaStoneActorRecord Actor, ImpaStoneTimingRecord Timing, ImpaStoneTexts Texts, ImpaStoneSounds Sounds);
