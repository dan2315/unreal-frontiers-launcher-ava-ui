using System.Collections.Generic;
using System.Text.Json.Serialization;

public class ObsoleteFiles
{
    [JsonPropertyName("obsolete_files")]
    public List<string> Files { get; set; }
}