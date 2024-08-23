/// Maded by Gorox. Discord - smeshinka112
using Content.Server.Backmen.XenoBiology.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;

namespace Content.Server.Backmen.XenoBiology.Components;

[RegisterComponent]
public sealed partial class XenoBiologyComponent : Component
{
    /// Начальное количество очков для деления
    [DataField("points"), ViewVariables(VVAccess.ReadWrite)]
    public int Points = 0;

    /// Сколько очков получает существо при атаке
    [DataField("pointsPerAttack"), ViewVariables(VVAccess.ReadWrite)]
    public int PointsPerAttack = 10;

    /// Сколько очков необходимо для деления
    [DataField("pointsThreshold"), ViewVariables(VVAccess.ReadWrite)]
    public int PointsThreshold = 200;

    /// Шанс мутации при делении
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Mutationchance = 0.3f;

    /// Прототип при удачной мутации
    [DataField("mutagen", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string Mutagen = "MobSlimesPet";

    /// Кем становится существо при делении, если имеет разум. Используйте прототип полиморфа
    [DataField("onMind", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string OnMind = "RandomSlimePerson";
}