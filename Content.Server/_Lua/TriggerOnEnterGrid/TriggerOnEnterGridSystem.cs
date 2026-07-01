// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld Contributors
// See AGPLv3.txt for details.

using Content.Shared.Trigger.Systems;

namespace Content.Server._Lua.TriggerOnEnterGrid;

public sealed partial class TriggerOnEnterGridSystem : EntitySystem
{
    [Dependency] private TriggerSystem _trigger = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<TriggerOnEnterGridComponent, TransformComponent>();
        while (query.MoveNext(out var entity, out var component, out var xform))
        {
            switch (component.ReadyToTrigger)
            {
                case true when xform.GridUid.HasValue:
                    _trigger.Trigger(entity);
                    break;
                case false when !xform.GridUid.HasValue:
                    component.ReadyToTrigger = true;
                    break;
            }
        }
    }
}
