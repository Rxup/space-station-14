using Content.Shared.Bed.Cryostorage;

namespace Content.Shared._Backmen.Cryostorage;

public sealed class MovedToStorageEvent : EntityEventArgs
{
    public Entity<CryostorageComponent> Storage;
}
