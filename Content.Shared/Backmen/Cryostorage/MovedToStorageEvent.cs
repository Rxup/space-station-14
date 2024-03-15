using Content.Shared.Bed.Cryostorage;

namespace Content.Shared.Backmen.Cryostorage;

public sealed class MovedToStorageEvent : EntityEventArgs
{
    public Entity<CryostorageComponent> Storage;
}
