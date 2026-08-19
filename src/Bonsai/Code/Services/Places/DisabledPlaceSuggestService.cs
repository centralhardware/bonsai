using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Bonsai.Code.Services.Places;

/// <summary>
/// Stub used when no geocoding provider is configured: the place fields work as plain text inputs.
/// </summary>
public class DisabledPlaceSuggestService: IPlaceSuggestService
{
    public bool IsEnabled => false;

    public Task<IReadOnlyList<PlaceSuggestionVM>> SuggestAsync(string query, CancellationToken token = default)
    {
        return Task.FromResult<IReadOnlyList<PlaceSuggestionVM>>([]);
    }
}
