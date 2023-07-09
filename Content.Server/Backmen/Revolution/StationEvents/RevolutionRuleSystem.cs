using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.CrewManifest;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.Mind.Components;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Server.Players;
using Content.Server.Power.EntitySystems;
using Content.Server.Preferences.Managers;
using Content.Server.Roles;
using Content.Server.RoundEnd;
using Content.Server.Shuttles.Systems;
using Content.Server.Spawners.Components;
using Content.Server.Station.Components;
using Content.Server.StationEvents.Events;
using Content.Server.Traits.Assorted;
using Content.Server.Zombies;
using Content.Shared.Actions.ActionTypes;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Nuke;
using Content.Shared.Preferences;
using Content.Shared.Radio.Components;
using Content.Shared.Random.Helpers;
using Content.Shared.Roles;
using Robust.Server.GameObjects;
using Robust.Server.Maps;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.Revolution.StationEvents;

public sealed class RevolutionGamemode : StationEventSystem<RevolutionRuleComponent>
{
    [Dependency] private readonly FactionSystem _faction = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly EmergencyShuttleSystem _emergency = default!;
    [Dependency] private readonly CrewManifestSystem _crewManifestSystem = default!;
    [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RevolutionOperativeComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<RevolutionOperativeComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<RevolutionOperativeComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<RevolutionOperativeComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<RoundStartAttemptEvent>(OnStartAttempt);
        SubscribeLocalEvent<RulePlayerSpawningEvent>(OnPlayersSpawning);
    }

    private void OnPlayersSpawning(RulePlayerSpawningEvent ev)
    {
        var query = EntityQueryEnumerator<RevolutionRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var revolution, out var gameRule))
        {


            var allPlayers = _playerManager.ServerSessions.ToList();
            var playerList = new List<IPlayerSession>();
            var prefMemberList = new List<IPlayerSession>();
            var prefLeaderList = new List<IPlayerSession>();
            foreach (var player in allPlayers)
            {
                if (player.AttachedEntity != null && HasComp<HumanoidAppearanceComponent>(player.AttachedEntity))
                {
                    playerList.Add(player);

                    var pref = (HumanoidCharacterProfile) _prefs.GetPreferences(player.UserId).SelectedCharacter;
                    if (pref.AntagPreferences.Contains(RevolutionRuleComponent.RevMemberProto))
                        prefMemberList.Add(player);
                    if (pref.AntagPreferences.Contains(RevolutionRuleComponent.RevLeaderProto))
                        prefLeaderList.Add(player);
                }
            }

            if (playerList.Count == 0)
                return;

            var playersPerRev = revolution.RevPerPlayer;
            var maxMembers = revolution.MaxPlayers;

            var membersNum = Math.Max(1,
                (int) Math.Min(
                    Math.Floor(playerList.Count / playersPerRev), maxMembers));

            for (var i = 0; i < membersNum; i++)
            {
                IPlayerSession player;
                if (prefMemberList.Count == 0)
                {
                    if (playerList.Count == 0)
                    {
                        Logger.InfoS("preset", "Insufficient number of players. stopping selection.");
                        break;
                    }

                    player = _random.PickAndTake(playerList);
                    Logger.InfoS("preset", "Insufficient preferred revolutioners, picking at random.");
                }
                else
                {
                    player = _random.PickAndTake(prefMemberList);
                    playerList.Remove(player);
                    Logger.InfoS("preset", "Selected a revolutioner.");
                }

                var mind = player.Data.ContentData()?.Mind;
                if (mind == null)
                {
                    Logger.ErrorS("preset", "Failed getting mind for picked revolutioner.");
                    continue;
                }

                DebugTools.AssertNotNull(mind.OwnedEntity);
                _mindSystem.AddRole(mind,
                    new RevolutionersRole(mind, _prototypeManager.Index<AntagPrototype>(RevolutionRuleComponent.RevMemberProto)));

                var inCharacterName = string.Empty;
                if (mind.OwnedEntity != null)
                {

                }

                if (mind.Session != null)
                {
                    var message = Loc.GetString("revolution-member-role-greeting");
                    var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", message));

                    //gets the names now in case the players leave.
                    //this gets unhappy if people with the same name get chose. Probably shouldn't happen.
                    revolution.OperativePlayers.Add(inCharacterName, mind.Session);

                    // I went all the way to ChatManager.cs and all i got was this lousy T-shirt
                    // You got a free T-shirt!?!?
                    _chatManager.ChatMessageToOne(Shared.Chat.ChatChannel.Server, message,
                        wrappedMessage, default, false, mind.Session.ConnectedClient, Color.Plum);
                }
            }
        }
    }

    private void OnStartAttempt(RoundStartAttemptEvent ev)
    {
        var query = EntityQueryEnumerator<RevolutionRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var revolution, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(uid, gameRule))
                continue;

            var minPlayers = revolution.MinPlayers;
            if (!ev.Forced && ev.Players.Length < minPlayers)
            {
                _chatManager.SendAdminAnnouncement(Loc.GetString("revolution-not-enough-ready-players",
                    ("readyPlayersCount", ev.Players.Length), ("minimumPlayers", minPlayers)));
                ev.Cancel();
                continue;
            }

            if (ev.Players.Length != 0)
                continue;

            _chatManager.DispatchServerAnnouncement(Loc.GetString("revolution-no-one-ready"));
            ev.Cancel();
        }
    }

    private void OnMindAdded(EntityUid uid, RevolutionOperativeComponent component, MindAddedMessage args)
    {
        if (!TryComp<MindContainerComponent>(uid, out var mindContainerComponent) ||
            mindContainerComponent.Mind == null)
            return;

        var mind = mindContainerComponent.Mind;
        foreach (var revolutioners in EntityQuery<RevolutionRuleComponent>())
        {
            foreach (var rev in EntityQuery<RevolutionOperativeComponent>())
            {
                _mindSystem.AddRole(mind,
                    new RevolutionersRole(mind,
                        _prototypeManager.Index<AntagPrototype>(component.IsLeader ? RevolutionRuleComponent.RevLeaderProto : RevolutionRuleComponent.RevMemberProto)));
            }

            if (!_mindSystem.TryGetSession(mind, out var playerSession))
                return;
            if (revolutioners.OperativePlayers.ContainsValue(playerSession))
                return;
            if (GameTicker.RunLevel != GameRunLevel.InRound)
                return;

            if (revolutioners.TargetStation != null && !string.IsNullOrEmpty(Name(revolutioners.TargetStation.Value)))
            {
                _chatManager.DispatchServerMessage(playerSession, Loc.GetString("nukeops-welcome", ("station", revolutioners.TargetStation.Value)));
            }
        }
    }

    private void OnMobStateChanged(EntityUid uid, RevolutionOperativeComponent component, MobStateChangedEvent ev)
    {
        if(ev.NewMobState == MobState.Dead)
            CheckRoundShouldEnd();
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        var query = EntityQueryEnumerator<RevolutionRuleComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            switch (ev.New)
            {
                case GameRunLevel.InRound:
                    OnRoundStart(uid, component);
                    break;
                case GameRunLevel.PostRound:
                    OnRoundEnd(uid, component);
                    break;
            }
        }
    }

    private void OnRoundEnd(EntityUid uid, RevolutionRuleComponent? component)
    {
        if (!Resolve(uid, ref component))
            return;

        // If the win condition was set to operative/crew major win, ignore.
        if (component.WinType is WinType.RevMajor or WinType.CrewMajor)
            return;

        //Проверяем количество живых

        var allAlive = true;
        var isLeaderAlive = true;

        foreach (var (comp, state) in EntityQuery<RevolutionOperativeComponent, MobStateComponent>())
        {
            if (state.CurrentState is MobState.Alive)
                continue;

            allAlive = false;
            if (comp.IsLeader)
            {
                isLeaderAlive = false;
                break;
            }
        }

        var isCapitanAlive = false;
        var StationLeadersAlive = 0;
        var StationLeadersTotal = 0;
        if (component.TargetStation.HasValue)
        {
            {
                StationLeadersTotal = _crewManifestSystem.GetCrewManifest(component.TargetStation.Value).entries?.Entries.Count(x => component.SupervisorRoles.Contains(x.JobPrototype))??0;
                var players = EntityQuery<MindContainerComponent, MobStateComponent>();

                foreach (var (mind,state) in players)
                {
                    if (!mind.HasMind || mind.Mind?.CurrentJob == null)
                    {
                        continue;
                    }

                    if (state.CurrentState != MobState.Alive)
                    {
                        continue;
                    }

                    var job = mind.Mind!.CurrentJob.Prototype.ID;
                    if (component.SupervisorRoles.Contains(job))
                    {
                        StationLeadersAlive++;
                    }

                    if (component.CapitanRole == job)
                    {
                        isCapitanAlive = true;
                    }
                }
            }
        }

        //Проверяем список выживших на условия победы

        var telecomAlive = false;
        var telcomQuery = AllEntityQuery<TelecomServerComponent, TransformComponent>();
        var centcomms = _emergency.GetCentcommMaps();

        while (telcomQuery.MoveNext(out var telcom, out var telcomTransform))
        {

            if (!this.IsPowered(telcom.Owner, EntityManager))
                continue;

            if (!TryComp(component.TargetStation, out StationDataComponent? data))
                continue;

            foreach (var grid in data.Grids)
            {
                if (grid != telcomTransform.GridUid)
                    continue;

                //Если телекомы запитаны и раунд завершился, то делаем проверку на условие победы

                if (isCapitanAlive || StationLeadersAlive > StationLeadersTotal/2)
                {
                    SetWinType(uid, WinType.CrewMinor, component);
                    return;
                }

                telecomAlive = true;
            }
        }

        //Проверяем другие условия победы

        if (!isLeaderAlive && StationLeadersAlive == 0)
        {
            SetWinType(uid, WinType.Neutral, component);
        }
    }

    private void SetWinType(EntityUid uid, WinType type, RevolutionRuleComponent? component)
    {
        if (!Resolve(uid, ref component))
            return;

        component.WinType = type;

        if (type == WinType.CrewMajor || type == WinType.RevMajor)
            _roundEndSystem.EndRound();
    }

    private void OnRoundStart(EntityUid uid, RevolutionRuleComponent? component)
    {
        if (!Resolve(uid, ref component))
            return;

        // TODO: This needs to try and target a Nanotrasen station. At the very least,
        // we can only currently guarantee that NT stations are the only station to
        // exist in the base game.

        var eligible = EntityQuery<StationEventEligibleComponent, FactionComponent>()
            .Where(x =>
                _faction.IsFactionHostile(component.Faction, x.Item2.Owner, x.Item2))
            .Select(x => x.Item1.Owner)
            .ToList();

        if (!eligible.Any())
            return;

        component.TargetStation = _random.Pick(eligible);

        var filter = Filter.Empty();
        var query = EntityQueryEnumerator<RevolutionOperativeComponent, ActorComponent>();
        while (query.MoveNext(out _, out _, out var actor))
        {
            _chatManager.DispatchServerMessage(actor.PlayerSession, Loc.GetString("revolutioners-welcome", ("station", component.TargetStation.Value)));
            filter.AddPlayer(actor.PlayerSession);
        }
    }

    private void OnComponentRemove(EntityUid uid, RevolutionOperativeComponent component, ComponentRemove args)
    {
        CheckRoundShouldEnd();
    }

    private void CheckRoundShouldEnd()
    {
        throw new NotImplementedException();
    }

    private void OnComponentInit(EntityUid uid, RevolutionOperativeComponent component, ComponentInit args)
    {
        var query = EntityQueryEnumerator<RevolutionRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var ruleEnt, out var revolutioners, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(ruleEnt, gameRule))
                continue;

            // If entity has a prior mind attached, add them to the players list.
            if (!TryComp<MindContainerComponent>(uid, out var mindComponent))
                continue;

            var session = mindComponent.Mind?.Session;
            var name = MetaData(uid).EntityName;
            if (session != null)
                revolutioners.OperativePlayers.Add(name, session);
            RemComp<PacifistComponent>(uid);
            RemComp<PacifiedComponent>(uid);
        }
    }
}
