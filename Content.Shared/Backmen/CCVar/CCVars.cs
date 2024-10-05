using Robust.Shared.Configuration;

namespace Content.Shared.Backmen.CCVar;

// ReSharper disable once InconsistentNaming
[CVarDefs]
public sealed class CCVars
{
    public static readonly CVarDef<bool>
        GameDiseaseEnabled = CVarDef.Create("game.disease", true, CVar.SERVERONLY);

    /*
     * GPT
     */
    public static readonly CVarDef<bool>
        GptEnabled = CVarDef.Create("gpt.enabled", false, CVar.SERVERONLY);

    public static readonly CVarDef<string>
        GptModel = CVarDef.Create("gpt.model", "gpt-3.5-turbo-0613", CVar.SERVERONLY);

    public static readonly CVarDef<string>
        GptApiUrl = CVarDef.Create("gpt.api", "https://api.openai.com/v1/", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    public static readonly CVarDef<string>
        GptApiToken = CVarDef.Create("gpt.token", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    public static readonly CVarDef<string>
        GptApiGigaToken = CVarDef.Create("gpt.giga_token", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /*
     * Queue
     */

    /// <summary>
    ///     Controls if the connections queue is enabled. If enabled stop kicking new players after `SoftMaxPlayers` cap and instead add them to queue.
    /// </summary>
    public static readonly CVarDef<bool>
        QueueEnabled = CVarDef.Create("queue.enabled", false, CVar.SERVERONLY);

    public static readonly CVarDef<bool>
        QueueAltEnabled = CVarDef.Create("queue.alt_servers", false, CVar.SERVERONLY);

    /*
     * Discord Auth
     */

    /// <summary>
    ///     Enabled Discord linking, show linking button and modal window
    /// </summary>
    public static readonly CVarDef<bool> DiscordAuthEnabled =
        CVarDef.Create("discord_auth.enabled", false, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    ///     URL of the Discord auth server API
    /// </summary>
    public static readonly CVarDef<string> DiscordAuthApiUrl =
        CVarDef.Create("discord_auth.api_url", "", CVar.SERVERONLY);

    /// <summary>
    ///     Secret key of the Discord auth server API
    /// </summary>
    public static readonly CVarDef<string> DiscordAuthApiKey =
        CVarDef.Create("discord_auth.api_key", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    public static readonly CVarDef<bool> DiscordAuthIsOptional =
        CVarDef.Create("discord_auth.is_opt", false, CVar.SERVER | CVar.REPLICATED);

    /**
     * Sponsors
     */

    /// <summary>
    ///     URL of the sponsors server API.
    /// </summary>
    public static readonly CVarDef<string> SponsorsApiUrl =
        CVarDef.Create("sponsor.api_url", "", CVar.SERVERONLY);

    public static readonly CVarDef<string> SponsorsSelectedGhost =
        CVarDef.Create("sponsor.ghost", "", CVar.REPLICATED | CVar.CLIENT | CVar.ARCHIVE);


    public static readonly CVarDef<bool>
        EconomyWagesEnabled = CVarDef.Create("economy.wages_enabled", true, CVar.SERVERONLY);

    public static readonly CVarDef<bool>
        WhitelistRolesEnabled = CVarDef.Create("game.whitelist_role_enabled", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Whether the Shipyard is enabled.
    /// </summary>
    public static readonly CVarDef<bool> Shipyard =
        CVarDef.Create("shuttle.shipyard", true, CVar.SERVERONLY);

    /*
     * Glimmer
     */

    /// <summary>
    ///    Whether glimmer is enabled.
    /// </summary>
    public static readonly CVarDef<bool> GlimmerEnabled =
        CVarDef.Create("glimmer.enabled", true, CVar.REPLICATED);

    /// <summary>
    ///     Passive glimmer drain per second.
    ///     Note that this is randomized and this is an average value.
    /// </summary>
    public static readonly CVarDef<float> GlimmerLostPerSecond =
        CVarDef.Create("glimmer.passive_drain_per_second", 0.1f, CVar.SERVERONLY);

    /// <summary>
    ///     Whether random rolls for psionics are allowed.
    ///     Guaranteed psionics will still go through.
    /// </summary>
    public static readonly CVarDef<bool> PsionicRollsEnabled =
        CVarDef.Create("psionics.rolls_enabled", true, CVar.SERVERONLY);

    /// <summary>
    /// Shipwrecked
    /// </summary>
    public static readonly CVarDef<int> ShipwreckedMaxPlayers =
        CVarDef.Create("shipwrecked.max_players", 15);

    /*
 * FleshCult
 */

    public static readonly CVarDef<int> FleshCultMinPlayers =
        CVarDef.Create("fleshcult.min_players", 25, CVar.SERVERONLY);

    public static readonly CVarDef<int> FleshCultMaxCultist =
        CVarDef.Create("fleshcult.max_cultist", 6, CVar.SERVERONLY);

    public static readonly CVarDef<int> FleshCultPlayersPerCultist =
        CVarDef.Create("fleshcult.players_per_cultist", 7, CVar.SERVERONLY);

    /*
     * bloodsucker
     */

    public static readonly CVarDef<int> BloodsuckerMaxPerBloodsucker =
        CVarDef.Create("bloodsucker.max", 5, CVar.SERVERONLY);

    public static readonly CVarDef<int> BloodsuckerPlayersPerBloodsucker =
        CVarDef.Create("bloodsucker.players_per", 10, CVar.SERVERONLY);

    /*
     * Blob
     */

    public static readonly CVarDef<int> BlobMax =
        CVarDef.Create("blob.max", 3, CVar.SERVERONLY);

    public static readonly CVarDef<int> BlobPlayersPer =
        CVarDef.Create("blob.players_per", 20, CVar.SERVERONLY);

    public static readonly CVarDef<bool> BlobCanGrowInSpace =
        CVarDef.Create("blob.grow_space", true, CVar.SERVER);

    /*
     * SpecForces
     */
    public static readonly CVarDef<int> SpecForceDelay =
        CVarDef.Create("specforce.delay", 2, CVar.SERVERONLY);

    /*
     * Ghost Respawn
     */

    public static readonly CVarDef<float> GhostRespawnTime =
        CVarDef.Create("ghost.respawn_time", 15f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<int> GhostRespawnMaxPlayers =
        CVarDef.Create("ghost.respawn_max_players", 40, CVar.SERVERONLY);

    /*
     * Immersive
     */

    public static readonly CVarDef<bool> ImmersiveEnabled =
        CVarDef.Create("immersive.enabled", true, CVar.SERVERONLY);

    /*
     * Bind Standing - Ataraxia
     */

    public static readonly CVarDef<bool> AutoGetUp =
        CVarDef.Create("laying.auto_get_up", true, CVar.CLIENT | CVar.ARCHIVE | CVar.REPLICATED);

    /// <summary>
    ///     When true, entities that fall to the ground will be able to crawl under tables and
    ///     plastic flaps, allowing them to take cover from gunshots.
    /// </summary>
    public static readonly CVarDef<bool> CrawlUnderTables =
        CVarDef.Create("laying.crawlundertables", true, CVar.REPLICATED);

    public static readonly CVarDef<bool> OfferModeIndicatorsPointShow =
        CVarDef.Create("hud.offer_mode_indicators_point_show", true, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<bool> HoldLookUp =
        CVarDef.Create("white.hold_look_up", false, CVar.CLIENT | CVar.ARCHIVE);

    #region Supermatter System

        /// <summary>
        ///     With completely default supermatter values, Singuloose delamination will occur if engineers inject at least 900 moles of coolant per tile
        ///     in the crystal chamber. For reference, a gas canister contains 1800 moles of air. This Cvar directly multiplies the amount of moles required to singuloose.
        /// </summary>
        public static readonly CVarDef<float> SupermatterSingulooseMolesModifier =
            CVarDef.Create("supermatter.singuloose_moles_modifier", 1f, CVar.SERVER);

        /// <summary>
        ///     Toggles whether or not Singuloose delaminations can occur. If both Singuloose and Tesloose are disabled, it will always delam into a Nuke.
        /// </summary>
        public static readonly CVarDef<bool> SupermatterDoSingulooseDelam =
            CVarDef.Create("supermatter.do_singuloose", true, CVar.SERVER);

        /// <summary>
        ///     By default, Supermatter will "Tesloose" if the conditions for Singuloose are not met, and the core's power is at least 4000.
        ///     The actual reasons for being at least this amount vary by how the core was screwed up, but traditionally it's caused by "The core is on fire".
        ///     This Cvar multiplies said power threshold for the purpose of determining if the delam is a Tesloose.
        /// </summary>
        public static readonly CVarDef<float> SupermatterTesloosePowerModifier =
            CVarDef.Create("supermatter.tesloose_power_modifier", 1f, CVar.SERVER);

        /// <summary>
        ///     Toggles whether or not Tesloose delaminations can occur. If both Singuloose and Tesloose are disabled, it will always delam into a Nuke.
        /// </summary>
        public static readonly CVarDef<bool> SupermatterDoTeslooseDelam =
            CVarDef.Create("supermatter.do_tesloose", true, CVar.SERVER);

        /// <summary>
        ///     When true, bypass the normal checks to determine delam type, and instead use the type chosen by supermatter.forced_delam_type
        /// </summary>
        public static readonly CVarDef<bool> SupermatterDoForceDelam =
            CVarDef.Create("supermatter.do_force_delam", false, CVar.SERVER);

        /// <summary>
        ///     If supermatter.do_force_delam is true, this determines the delamination type, bypassing the normal checks.
        /// </summary>
        public static readonly CVarDef<string> SupermatterForcedDelamType =
            CVarDef.Create("supermatter.forced_delam_type", "Singulo", CVar.SERVER);

        /// <summary>
        ///     Directly multiplies the amount of rads put out by the supermatter. Be VERY conservative with this.
        /// </summary>
        public static readonly CVarDef<float> SupermatterRadsModifier =
            CVarDef.Create("supermatter.rads_modifier", 1f, CVar.SERVER);

        #endregion

    #region Surgery

    public static readonly CVarDef<bool> CanOperateOnSelf =
        CVarDef.Create("surgery.can_operate_on_self", false, CVar.SERVERONLY);

    #endregion

    #region Mood System

    public static readonly CVarDef<bool> MoodEnabled =
        CVarDef.Create("mood.enabled", true, CVar.SERVER);

    public static readonly CVarDef<bool> MoodIncreasesSpeed =
        CVarDef.Create("mood.increases_speed", true, CVar.SERVER);

    public static readonly CVarDef<bool> MoodDecreasesSpeed =
        CVarDef.Create("mood.decreases_speed", true, CVar.SERVER);

    public static readonly CVarDef<bool> MoodModifiesThresholds =
        CVarDef.Create("mood.modify_thresholds", false, CVar.SERVER);

    #endregion
}
