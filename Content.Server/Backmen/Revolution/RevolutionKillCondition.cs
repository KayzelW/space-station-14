using Content.Server.Mind.Components;
using Content.Server.Objectives.Conditions;
using Content.Server.Objectives.Interfaces;

namespace Content.Server.Backmen.Revolution;

public sealed class RevolutionKillCondition : KillPersonCondition
{
    [DataField("targetJob")]
    public string? TargetJob { get; set; }

public override IObjectiveCondition GetAssigned(Content.Server.Mind.Mind mind)
{
    var entityManager = IoCManager.Resolve<IEntityManager>();
        if (!IoCManager.Resolve<IEntityManager>().TryGetComponent<RevolutionOperativeComponent>(mind.OwnedEntity, out var rev) || TargetJob == null)
        {
            return new EscapeShuttleCondition();
        }


        if (rev.RuleComponent == null || rev.RuleComponent.Leaders.ContainsKey(TargetJob))
        {
            return new DieCondition();
        }

        if (!entityManager.TryGetComponent<MindContainerComponent>(rev.RuleComponent.Leaders[TargetJob], out var mindContainer))
        {
            return new EscapeShuttleCondition();
        }
        return new RevolutionKillCondition
        {
            Target = mindContainer.Mind
        };
    }
}


