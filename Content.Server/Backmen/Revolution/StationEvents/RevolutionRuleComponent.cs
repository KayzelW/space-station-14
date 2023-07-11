using Content.Server.NPC.Components;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;

namespace Content.Server.Backmen.Revolution.StationEvents;

[RegisterComponent]
public sealed class RevolutionRuleComponent:Component
{
    [DataField("operativePlayers")] public readonly Dictionary<string, IPlayerSession> OperativePlayers = new();

    [DataField("faction", customTypeSerializer: typeof(PrototypeIdSerializer<FactionPrototype>), required: true)]
    public string Faction = default!;

    [DataField("winType")] public WinType WinType = WinType.Neutral;

    [ViewVariables(VVAccess.ReadWrite),
     DataField("supervisorRoles", customTypeSerializer: typeof(PrototypeIdListSerializer<JobPrototype>))]
    public List<string> SupervisorRoles = new();

    public Dictionary<string,EntityUid> Leaders = new();

    public EntityUid Captain = EntityUid.Invalid;

    [DataField("minPlayers")] public int MinPlayers = 1;

    [DataField("maxOps")] public int MaxPlayers = 10;

    [DataField("revPerPlayer")] public double RevPerPlayer = 0.1;

    [ViewVariables(VVAccess.ReadWrite),
     DataField("capitanRole", customTypeSerializer: typeof(PrototypeIdSerializer<JobPrototype>))]
    public string CapitanRole = "JobCaptain";

    public EntityUid? TargetStation;
    public const string RevLeaderProto = "RevLeader";
    public const string RevMemberProto = "RevMember";
}

public enum WinType : byte
{
    /// <summary>
    /// Станция захвачена, экскадрон был уничтожен
    /// </summary>
    RevMajor,
    /// <summary>
    /// Станция была захвачена, связь с ЦК отключена
    /// </summary>
    RevMinor,
    /// <summary>
    /// Лидер революционеров был убит, все главы мертвы
    /// </summary>
    Neutral,
    ///<summary>
    /// Некоторые главы выжили
    ///</summary>
    CrewMinor,
    /// <summary>
    /// Все революционеры мертвы
    /// </summary>
    CrewMajor
}
