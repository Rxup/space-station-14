namespace Content.Server.Backmen.Shipwrecked.Biome;

public sealed class UnLoadChunkEvent : CancellableEntityEventArgs
{
    public Vector2i Chunk { get; set; }

    public UnLoadChunkEvent(Vector2i chunk)
    {
        Chunk = chunk;
    }
}
