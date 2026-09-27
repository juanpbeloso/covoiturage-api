namespace SubiteAPI.Options;

public class GoogleMapsOptions
{
    public const string SectionName = "GoogleMaps";

    /// <summary>API key de servidor (Directions). No exponerla en la app.</summary>
    public string ApiKey { get; set; } = string.Empty;
}
