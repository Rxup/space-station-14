using System.Text.Json.Serialization;

namespace Content.Server.Discord.Webhooks
{
    public struct Embed
    {
        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("color")]
        public int Color { get; set; } = 0;

        [JsonPropertyName("footer")]
        public EmbedFooter? Footer { get; set; } = null;

        public Embed()
        {
        }
    }
}