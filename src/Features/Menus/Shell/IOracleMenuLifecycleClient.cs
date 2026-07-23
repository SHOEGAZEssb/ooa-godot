using Godot;
using System;

namespace oracleofages;

internal interface IOracleMenuLifecycleClient
{
    string MenuName { get; }
    void OpenAtWhite();
    void CloseAtWhite();
    void LifecycleClosed();
}
