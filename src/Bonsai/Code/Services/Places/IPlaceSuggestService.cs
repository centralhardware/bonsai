using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Bonsai.Code.Services.Places;

/// <summary>
/// Provides place name suggestions for place-related facts.
/// </summary>
public interface IPlaceSuggestService
{
    /// <summary>
    /// Flag indicating that a geocoding provider is configured.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Returns the places matching the query.
    /// </summary>
    Task<IReadOnlyList<PlaceSuggestionVM>> SuggestAsync(string query, CancellationToken token = default);
}
