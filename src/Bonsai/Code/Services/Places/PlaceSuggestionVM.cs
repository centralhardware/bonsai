namespace Bonsai.Code.Services.Places;

/// <summary>
/// A single place suggested by the geocoding provider.
/// </summary>
public class PlaceSuggestionVM
{
    /// <summary>
    /// Full name of the place (inserted into the fact when picked).
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Short name of the place (e.g. the city itself, without the country).
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Clarifying part of the name (e.g. the region and the country).
    /// </summary>
    public string Description { get; set; }
}
