namespace Content.Shared._Backmen.Disease;

public abstract class SharedBkRottingSystem : EntitySystem
{
    public virtual string RequestPoolDisease()
    {
        // server-only handling
        return "";
    }
}
