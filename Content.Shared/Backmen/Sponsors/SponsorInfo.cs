﻿using System.IO;
using System.Text.Json.Serialization;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Backmen.Sponsors;

[Serializable, NetSerializable]
public sealed class SponsorInfo
{
    [JsonPropertyName("tier")]
    public int? Tier { get; set; }

    [JsonPropertyName("oocColor")]
    public string? OOCColor { get; set; }

    [JsonPropertyName("priorityJoin")]
    public bool HavePriorityJoin { get; set; } = false;

    [JsonPropertyName("extraSlots")]
    public int ExtraSlots { get; set; }

    [JsonPropertyName("allowedMarkings")] // TODO: Rename API field in separate PR as breaking change!
    public string[] AllowedMarkings { get; set; } = [];

    [JsonPropertyName("loadouts")]
    public string[] Loadouts { get; set; } = [];

    [JsonPropertyName("openAllRoles")]
    public bool OpenAllRoles { get; set; } = false;

    [JsonPropertyName("ghostTheme")]
    public string? GhostTheme { get; set; }
}

/// <summary>
/// Server sends sponsoring info to client on connect only if user is sponsor
/// </summary>
public sealed class MsgSponsorInfo : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public SponsorInfo? Info;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        var isSponsor = buffer.ReadBoolean();
        buffer.ReadPadBits();
        if (!isSponsor)
            return;

        var length = buffer.ReadVariableInt32();
        using var stream = new MemoryStream();
        {
            buffer.ReadAlignedMemory(stream, length);
            serializer.DeserializeDirect(stream, out Info);
        }
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Info != null);
        buffer.WritePadBits();
        if (Info == null)
            return;

        using var stream = new MemoryStream();
        serializer.SerializeDirect(stream, Info);
        buffer.WriteVariableInt32((int) stream.Length);
        stream.TryGetBuffer(out var segment);
        buffer.Write(segment);
    }
}
