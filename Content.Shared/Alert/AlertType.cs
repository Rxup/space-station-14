namespace Content.Shared.Alert
{
    /// <summary>
    /// Every kind of alert. Corresponds to alertType field in alert prototypes defined in YML
    /// NOTE: Using byte for a compact encoding when sending this in messages, can upgrade
    /// to ushort
    /// </summary>
    public enum AlertType : byte
    {
        Error,
        LowOxygen,
        LowNitrogen,
        LowPressure,
        HighPressure,
        Fire,
        Cold,
        Hot,
        Weightless,
        Stun,
        Handcuffed,
        Ensnared,
        Buckled,
        HumanCrit,
        HumanDead,
        HumanHealth,
        BorgBattery,
        BorgBatteryNone,
        PilotingShuttle,
        Peckish,
        Starving,
        Thirsty,
        Parched,
        Charge, // Parkstation-IPC
        Stamina,
        ShadowkinPower,
        Pulled,
        Pulling,
        Magboots,
        Internals,
        Toxins,
        Muted,
        BlobResource,
        BlobHealth,
        VowOfSilence,
        VowBroken,
        Essence,
        MutationPoint,
        Corporeal,
        Bleed,
        Pacified,
        Debug1,
        Debug2,
        Debug3,
        Debug4,
        Debug5,
        Debug6,
        SuitPower,
        BorgHealth,
        BorgCrit,
        BorgDead,
        Deflecting
    }

}
