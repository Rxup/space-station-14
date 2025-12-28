namespace Content.Shared._Backmen.Weapons.Common;

public sealed class UniqueActionEvent(EntityUid userUid) : HandledEntityEventArgs
{
    public readonly EntityUid UserUid = userUid;
}
