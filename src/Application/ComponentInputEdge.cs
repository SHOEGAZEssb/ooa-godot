namespace oracleofages;

// Original updates already carry wGameKeysJustPressed through Input. Direct
// component callers can run repeatedly in one Godot frame; consume that host
// edge once without reconstructing edges from a component's last held state
// (which may predate a dialogue pause).
internal struct ComponentInputEdge
{
    private bool _observed;

    internal bool Read(bool justPressed)
    {
        if (Input.OriginalUpdateActive)
        {
            _observed = false;
            return justPressed;
        }
        bool result = justPressed && !_observed;
        _observed = justPressed;
        return result;
    }
}
