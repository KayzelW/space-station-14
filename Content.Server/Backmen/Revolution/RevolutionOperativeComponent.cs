using Content.Server.Backmen.Revolution.StationEvents;

namespace Content.Server.Backmen.Revolution;

[RegisterComponent]
public sealed class RevolutionOperativeComponent : Component
{
    public RevolutionRuleComponent? RuleComponent;
    public bool IsLeader = false;
}
