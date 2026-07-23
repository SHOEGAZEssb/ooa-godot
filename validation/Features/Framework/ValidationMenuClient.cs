using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace oracleofages;
internal sealed class ValidationMenuClient(string name) : IOracleMenuLifecycleClient
{
    public string MenuName { get; } = name;
    public int OpenAtWhiteCalls { get; private set; }
    public int CloseAtWhiteCalls { get; private set; }
    public int ClosedCalls { get; private set; }

    public void OpenAtWhite() => OpenAtWhiteCalls++;
    public void CloseAtWhite() => CloseAtWhiteCalls++;
    public void LifecycleClosed() => ClosedCalls++;
}
