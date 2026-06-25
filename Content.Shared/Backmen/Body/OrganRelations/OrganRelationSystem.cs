using Robust.Shared.Utility;

namespace Content.Shared.Backmen.Body.OrganRelations;

public sealed class OrganRelationSystem : EntitySystem
{
    private EntityQuery<ChildOrganComponent> _child = default!;
    private EntityQuery<ParentOrganComponent> _parent = default!;

    public override void Initialize()
    {
        base.Initialize();

        _child = GetEntityQuery<ChildOrganComponent>();
        _parent = GetEntityQuery<ParentOrganComponent>();

        SubscribeLocalEvent<ParentOrganComponent, ComponentShutdown>(OnParentShutdown);
        SubscribeLocalEvent<ChildOrganComponent, ComponentShutdown>(OnChildShutdown);
    }

    private void OnChildShutdown(Entity<ChildOrganComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Parent is not { } parentUid)
            return;

        ParentOrganComponent? parentComp = null;
        if (!_parent.Resolve(parentUid, ref parentComp, logMissing: false))
            return;

        parentComp.Children.Remove(ent);
        Dirty(parentUid, parentComp);
    }

    private void OnParentShutdown(Entity<ParentOrganComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Children.Count == 0)
            return;

        foreach (var childUid in ent.Comp.Children)
        {
            ChildOrganComponent? childComp = null;
            if (!_child.Resolve(childUid, ref childComp, logMissing: false))
                continue;

            childComp.Parent = null;
            Dirty(childUid, childComp);
        }
    }

    public void Relate(Entity<ParentOrganComponent?> parent, Entity<ChildOrganComponent?> child)
    {
        if (!_parent.Resolve(parent, ref parent.Comp, logMissing: false)
            || !_child.Resolve(child, ref child.Comp, logMissing: false))
            return;

        DebugTools.Assert(child.Comp!.Parent == null);

        parent.Comp!.Children.Add(child);
        Dirty(parent, parent.Comp);

        child.Comp.Parent = parent;
        Dirty(child, child.Comp);
    }

    public void Orphan(Entity<ChildOrganComponent?> child)
    {
        if (!_child.Resolve(child, ref child.Comp, logMissing: false))
            return;

        if (child.Comp!.Parent is not { } parentUid)
            return;

        child.Comp.Parent = null;
        Dirty(child, child.Comp);

        ParentOrganComponent? parentComp = null;
        if (!_parent.Resolve(parentUid, ref parentComp, logMissing: false))
            return;

        parentComp.Children.Remove(child);
        Dirty(parentUid, parentComp);
    }

    public IEnumerable<Entity<ParentOrganComponent>> AllParents(Entity<ChildOrganComponent?> child)
    {
        if (!_child.Resolve(child, ref child.Comp, logMissing: false))
            yield break;

        while (child.Comp?.Parent is { } parent)
        {
            ParentOrganComponent? parentComp = null;
            if (!_parent.Resolve(parent, ref parentComp, logMissing: false))
                yield break;

            yield return (parent, parentComp);

            ChildOrganComponent? parentChild = null;
            if (!_child.Resolve(parent, ref parentChild, logMissing: false))
                yield break;

            child = (parent, parentChild);
        }
    }

    public IEnumerable<Entity<ChildOrganComponent>> AllChildren(Entity<ParentOrganComponent?> parent)
    {
        if (!_parent.Resolve(parent, ref parent.Comp, logMissing: false))
            yield break;

        foreach (var childUid in parent.Comp!.Children)
        {
            ChildOrganComponent? childComp = null;
            if (!_child.Resolve(childUid, ref childComp, logMissing: false))
                continue;

            yield return (childUid, childComp);

            foreach (var childChild in AllChildren(childUid))
                yield return childChild;
        }
    }
}
