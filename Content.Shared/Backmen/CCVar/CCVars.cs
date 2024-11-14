using Robust.Shared.Configuration;

namespace Content.Shared.Backmen.CCVar;

// ReSharper disable once InconsistentNaming
[CVarDefs]
public sealed partial class CCVars
{
    public static readonly CVarDef<bool>
        GameDiseaseEnabled = CVarDef.Create("game.disease", true, CVar.SERVERONLY);

    /// <summary>
    /// Whether the Shipyard is enabled.
    /// </summary>
    public static readonly CVarDef<bool> Shipyard =
        CVarDef.Create("shuttle.shipyard", true, CVar.SERVERONLY);

    public static readonly CVarDef<bool>
        EconomyWagesEnabled = CVarDef.Create("economy.wages_enabled", true, CVar.SERVERONLY);

    public static readonly CVarDef<bool>
        WhitelistRolesEnabled = CVarDef.Create("game.whitelist_role_enabled", true, CVar.SERVER | CVar.REPLICATED);

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

    /*
    Atmos
    */
    /// <summary>
    ///     Whether pipes will unanchor on ANY conflicting connection. May break maps.
    ///     If false, allows you to stack pipes as long as new directions are added (i.e. in a new pipe rotation, layer or multi-Z link), otherwise unanchoring them.
    /// </summary>
    public static readonly CVarDef<bool> StrictPipeStacking =
        CVarDef.Create("atmos.strict_pipe_stacking", false, CVar.SERVERONLY);


    /*
    * AUTOVOTE SYSTEM
    */

    /// Enables the automatic voting system.
    public static readonly CVarDef<bool> AutoVoteEnabled =
        CVarDef.Create("vote.autovote_enabled", false, CVar.SERVERONLY);

    /// Automatically starts a map vote when returning to the lobby.
    /// Requires auto voting to be enabled.  
    public static readonly CVarDef<bool> MapAutoVoteEnabled =
        CVarDef.Create("vote.map_autovote_enabled", true, CVar.SERVERONLY);

    /// Automatically starts a gamemode vote when returning to the lobby.
    /// Requires auto voting to be enabled.  
    public static readonly CVarDef<bool> PresetAutoVoteEnabled =
        CVarDef.Create("vote.preset_autovote_enabled", true, CVar.SERVERONLY);
}
