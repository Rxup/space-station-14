using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Body.Systems;

public partial class SharedBodySystem
{
    private void InitializePartAppearances()
    {
        // Markings are handled by SharedVisualBodySystem in nubody.
    }

    protected virtual void ApplyPartMarkings(EntityUid target, EntityUid part)
    {
    }

    protected virtual void RemoveBodyMarkings(EntityUid target, EntityUid part, EntityUid body)
    {
    }

    public void ModifyMarkings(
        EntityUid body,
        EntityUid part,
        HumanoidProfileComponent profile,
        HumanoidVisualLayers category,
        string markingId)
    {
        // TODO: Wire to SharedVisualBodySystem when marking surgeries are migrated.
    }
}
