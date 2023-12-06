using System.Numerics;
using Content.Client.Items.Systems;
using Content.Shared.Item;
using Content.Shared.Storage;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client.UserInterface.Systems.Storage.Controls;

public sealed class ItemGridPiece : Control
{
    private readonly IEntityManager _entityManager;
    private readonly StorageUIController _storageController;

    private readonly List<(Texture, Vector2)> _texturesPositions = new();

    public readonly EntityUid Entity;
    public ItemStorageLocation Location;

    public event Action<GUIBoundKeyEventArgs, ItemGridPiece>? OnPiecePressed;
    public event Action<GUIBoundKeyEventArgs, ItemGridPiece>? OnPieceUnpressed;

    #region Textures
    private readonly string _centerTexturePath = "Storage/piece_center";
    private Texture? _centerTexture;
    private readonly string _topTexturePath = "Storage/piece_top";
    private Texture? _topTexture;
    private readonly string _bottomTexturePath = "Storage/piece_bottom";
    private Texture? _bottomTexture;
    private readonly string _leftTexturePath = "Storage/piece_left";
    private Texture? _leftTexture;
    private readonly string _rightTexturePath = "Storage/piece_right";
    private Texture? _rightTexture;
    private readonly string _topLeftTexturePath = "Storage/piece_topLeft";
    private Texture? _topLeftTexture;
    private readonly string _topRightTexturePath = "Storage/piece_topRight";
    private Texture? _topRightTexture;
    private readonly string _bottomLeftTexturePath = "Storage/piece_bottomLeft";
    private Texture? _bottomLeftTexture;
    private readonly string _bottomRightTexturePath = "Storage/piece_bottomRight";
    private Texture? _bottomRightTexture;
    #endregion

    public ItemGridPiece(Entity<ItemComponent> entity, ItemStorageLocation location,  IEntityManager entityManager)
    {
        IoCManager.InjectDependencies(this);

        _entityManager = entityManager;
        _storageController = UserInterfaceManager.GetUIController<StorageUIController>();

        Entity = entity.Owner;
        Location = location;

        Visible = true;
        MouseFilter = MouseFilterMode.Pass;

        OnThemeUpdated();
    }

    protected override void OnThemeUpdated()
    {
        base.OnThemeUpdated();

        _centerTexture = Theme.ResolveTextureOrNull(_centerTexturePath)?.Texture;
        _topTexture = Theme.ResolveTextureOrNull(_topTexturePath)?.Texture;
        _bottomTexture = Theme.ResolveTextureOrNull(_bottomTexturePath)?.Texture;
        _leftTexture = Theme.ResolveTextureOrNull(_leftTexturePath)?.Texture;
        _rightTexture = Theme.ResolveTextureOrNull(_rightTexturePath)?.Texture;
        _topLeftTexture = Theme.ResolveTextureOrNull(_topLeftTexturePath)?.Texture;
        _topRightTexture = Theme.ResolveTextureOrNull(_topRightTexturePath)?.Texture;
        _bottomLeftTexture = Theme.ResolveTextureOrNull(_bottomLeftTexturePath)?.Texture;
        _bottomRightTexture = Theme.ResolveTextureOrNull(_bottomRightTexturePath)?.Texture;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        // really just an "oh shit" catch.
        if (!_entityManager.EntityExists(Entity))
        {
            Dispose();
            return;
        }

        if (_storageController.IsDragging && _storageController.CurrentlyDragging == this)
            return;

        var adjustedShape = _entityManager.System<ItemSystem>().GetAdjustedItemShape((Entity, null), Location.Rotation, Vector2i.Zero);
        var boundingGrid = adjustedShape.GetBoundingBox();
        var size = _centerTexture!.Size * 2 * UIScale;

        var hovering = !_storageController.IsDragging && UserInterfaceManager.CurrentlyHovered == this;
        //yeah, this coloring is kinda hardcoded. deal with it. B)
        Color? colorModulate = hovering  ? null : Color.FromHex("#a8a8a8");

        _texturesPositions.Clear();
        for (var y = boundingGrid.Bottom; y <= boundingGrid.Top; y++)
        {
            for (var x = boundingGrid.Left; x <= boundingGrid.Right; x++)
            {
                if (!adjustedShape.Contains(x, y))
                    continue;

                var offset = size * 2 * new Vector2(x - boundingGrid.Left, y - boundingGrid.Bottom);
                var topLeft = PixelPosition + offset.Floored();

                if (GetTexture(adjustedShape, new Vector2i(x, y), Direction.NorthEast) is {} neTexture)
                {
                    var neOffset = new Vector2(size.X, 0);
                    handle.DrawTextureRect(neTexture, new UIBox2(topLeft + neOffset, topLeft + neOffset + size), colorModulate);
                }
                if (GetTexture(adjustedShape, new Vector2i(x, y), Direction.NorthWest) is {} nwTexture)
                {
                    _texturesPositions.Add((nwTexture, Position + offset / UIScale));
                    handle.DrawTextureRect(nwTexture, new UIBox2(topLeft, topLeft + size), colorModulate);
                }
                if (GetTexture(adjustedShape, new Vector2i(x, y), Direction.SouthEast) is {} seTexture)
                {
                    var seOffset = size;
                    handle.DrawTextureRect(seTexture, new UIBox2(topLeft + seOffset, topLeft + seOffset + size), colorModulate);
                }
                if (GetTexture(adjustedShape, new Vector2i(x, y), Direction.SouthWest) is {} swTexture)
                {
                    var swOffset = new Vector2(0, size.Y);
                    handle.DrawTextureRect(swTexture, new UIBox2(topLeft + swOffset, topLeft + swOffset + size), colorModulate);
                }
            }
        }

        // typically you'd divide by two, but since the textures are half a tile, this is done implicitly
        var iconOffset = new Vector2((boundingGrid.Width + 1) * size.X ,
            (boundingGrid.Height + 1) * size.Y);

        _entityManager.System<SpriteSystem>().ForceUpdate(Entity);
        handle.DrawEntity(Entity,
            PixelPosition + iconOffset,
            Vector2.One * 2 * UIScale,
            Angle.Zero,
            overrideDirection: Direction.South);
    }

    protected override bool HasPoint(Vector2 point)
    {
        foreach (var (texture, position) in _texturesPositions)
        {
            if (!new Box2(position, position + texture.Size * 4).Contains(point))
                continue;

            return true;
        }

        return false;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        OnPiecePressed?.Invoke(args, this);
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        OnPieceUnpressed?.Invoke(args, this);
    }

    private Texture? GetTexture(IReadOnlyList<Box2i> boxes, Vector2i position, Direction corner)
    {
        var top = !boxes.Contains(position - Vector2i.Up);
        var bottom = !boxes.Contains(position - Vector2i.Down);
        var left = !boxes.Contains(position + Vector2i.Left);
        var right = !boxes.Contains(position + Vector2i.Right);

        switch (corner)
        {
            case Direction.NorthEast:
                if (top && right)
                    return _topRightTexture;
                if (top)
                    return _topTexture;
                if (right)
                    return _rightTexture;
                return _centerTexture;
            case Direction.NorthWest:
                if (top && left)
                    return _topLeftTexture;
                if (top)
                    return _topTexture;
                if (left)
                    return _leftTexture;
                return _centerTexture;
            case Direction.SouthEast:
                if (bottom && right)
                    return _bottomRightTexture;
                if (bottom)
                    return _bottomTexture;
                if (right)
                    return _rightTexture;
                return _centerTexture;
            case Direction.SouthWest:
                if (bottom && left)
                    return _bottomLeftTexture;
                if (bottom)
                    return _bottomTexture;
                if (left)
                    return _leftTexture;
                return _centerTexture;
            default:
                return null;
        }
    }

    public static Vector2 GetCenterOffset(Entity<ItemComponent?> entity, ItemStorageLocation location, IEntityManager entMan)
    {
        var boxSize = entMan.System<ItemSystem>().GetAdjustedItemShape(entity, location).GetBoundingBox().Size;
        var actualSize = new Vector2(boxSize.X + 1, boxSize.Y + 1);
        return actualSize * new Vector2i(8, 8);
    }
}
