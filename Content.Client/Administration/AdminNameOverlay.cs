using System.Numerics;
using Content.Client.Administration.Systems;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared;
using Robust.Shared.Enums;
using Robust.Shared.Configuration;

namespace Content.Client.Administration;

internal sealed class AdminNameOverlay : Overlay
{
    private readonly AdminSystem _system;
    private readonly IEntityManager _entityManager;
    private readonly IEyeManager _eyeManager;
    private readonly EntityLookupSystem _entityLookup;
    private readonly IUserInterfaceManager _userInterfaceManager;
    private readonly Font _font;

    public AdminNameOverlay(AdminSystem system, IEntityManager entityManager, IEyeManager eyeManager, IResourceCache resourceCache, EntityLookupSystem entityLookup, IUserInterfaceManager userInterfaceManager)
    {
        _system = system;
        _entityManager = entityManager;
        _eyeManager = eyeManager;
        _entityLookup = entityLookup;
        _userInterfaceManager = userInterfaceManager;
        ZIndex = 200;
        _font = new VectorFont(resourceCache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 10);
    }

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    // start-backmen: SAI

    private void DrawVisitedOwner(in Content.Shared.Administration.PlayerInfo playerInfo, in OverlayDrawArgs args)
    {
        var entity = _entityManager.GetEntity(playerInfo.NetEntityOwn);

        // Otherwise the entity can not exist yet
        if (entity == null || !_entityManager.EntityExists(entity))
        {
            return;
        }

        // if not on the same map, continue
        if (_entityManager.GetComponent<TransformComponent>(entity.Value).MapID != _eyeManager.CurrentMap)
        {
            return;
        }

        var aabb = _entityLookup.GetWorldAABB(entity.Value);

        // if not on screen, continue
        if (!aabb.Intersects(in args.WorldAABB))
        {
            return;
        }

        var lineoffset = new Vector2(0f, 11f);
        var screenCoordinates = _eyeManager.WorldToScreen(aabb.Center +
                                                            new Angle(-_eyeManager.CurrentEye.Rotation).RotateVec(
                                                                aabb.TopRight - aabb.Center)) + new Vector2(1f, 7f);
        if (playerInfo.Antag)
        {
            args.ScreenHandle.DrawString(_font, screenCoordinates + (lineoffset * 3), "ANTAG", Color.OrangeRed);
        }
        args.ScreenHandle.DrawString(_font, screenCoordinates+lineoffset, playerInfo.Username, playerInfo.Connected ? Color.Yellow : Color.White);
        args.ScreenHandle.DrawString(_font, screenCoordinates, playerInfo.CharacterName, playerInfo.Connected ? Color.Aquamarine : Color.White);

        args.ScreenHandle.DrawString(_font, screenCoordinates + (lineoffset * 2), "(In REMOTE)", Color.Cyan);
    }

    // end-backmen: SAI

    protected override void Draw(in OverlayDrawArgs args)
    {
        var viewport = args.WorldAABB;

        foreach (var playerInfo in _system.PlayerList)
        {
            DrawVisitedOwner(playerInfo,args); // backmen: SAI

            var entity = _entityManager.GetEntity(playerInfo.NetEntity);

            // Otherwise the entity can not exist yet
            if (entity == null || !_entityManager.EntityExists(entity))
            {
                continue;
            }

            // if not on the same map, continue
            if (_entityManager.GetComponent<TransformComponent>(entity.Value).MapID != args.MapId)
            {
                continue;
            }

            var aabb = _entityLookup.GetWorldAABB(entity.Value);

            // if not on screen, continue
            if (!aabb.Intersects(in viewport))
            {
                continue;
            }

            var uiScale = _userInterfaceManager.RootControl.UIScale;
            var lineoffset = new Vector2(0f, 11f) * uiScale;
            var screenCoordinates = _eyeManager.WorldToScreen(aabb.Center +
                                                              new Angle(-_eyeManager.CurrentEye.Rotation).RotateVec(
                                                                  aabb.TopRight - aabb.Center)) + new Vector2(1f, 7f);
            if (playerInfo.Antag)
            {
                args.ScreenHandle.DrawString(_font, screenCoordinates + (lineoffset * 2), "ANTAG", uiScale, Color.OrangeRed);
;
            }
            args.ScreenHandle.DrawString(_font, screenCoordinates+lineoffset, playerInfo.Username, uiScale, playerInfo.Connected ? Color.Yellow : Color.White);
            args.ScreenHandle.DrawString(_font, screenCoordinates, playerInfo.CharacterName, uiScale, playerInfo.Connected ? Color.Aquamarine : Color.White);
        }
    }
}
