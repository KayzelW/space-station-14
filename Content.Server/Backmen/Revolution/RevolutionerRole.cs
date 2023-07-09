using Content.Server.Roles;
using Content.Shared.Roles;

namespace Content.Server.Backmen.Revolution;

public sealed class RevolutionersRole : AntagonistRole
{
    public RevolutionersRole(Content.Server.Mind.Mind mind, AntagPrototype antagPrototype) : base(mind, antagPrototype) { }
}
