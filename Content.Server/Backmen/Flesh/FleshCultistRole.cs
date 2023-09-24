using Content.Server.Chat.Managers;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared.Roles;

namespace Content.Server.Backmen.Flesh
{
    public sealed class FleshCultistRole : Role
    {
        [Dependency] private readonly IEntityManager _entityManager = default!;
        public AntagPrototype Prototype { get; }

        public FleshCultistRole(Mind.Mind mind, AntagPrototype antagPrototype) : base(mind)
        {
            Prototype = antagPrototype;
            Name = Loc.GetString(antagPrototype.Name);
            Antagonist = antagPrototype.Antagonist;
        }

        public override string Name { get; }
        public override bool Antagonist { get; }

        public void GreetCultist(List<string> cultistsNames)
        {
            if (_entityManager.System<MindSystem>().TryGetSession(Mind, out var session))
            {
                var chatMgr = IoCManager.Resolve<IChatManager>();
                chatMgr.DispatchServerMessage(session, Loc.GetString("flesh-cult-role-greeting"));
                chatMgr.DispatchServerMessage(session, Loc.GetString("flesh-cult-role-cult-members", ("cultMembers", string.Join(", ", cultistsNames))));
            }
        }

        public void GreetCultistLeader(List<string> cultistsNames)
        {
            if (_entityManager.System<MindSystem>().TryGetSession(Mind, out var session))
            {
                var chatMgr = IoCManager.Resolve<IChatManager>();
                chatMgr.DispatchServerMessage(session, Loc.GetString("flesh-cult-role-greeting-leader"));
                chatMgr.DispatchServerMessage(session, Loc.GetString("flesh-cult-role-cult-members", ("cultMembers", string.Join(", ", cultistsNames))));
            }
        }
    }
}
