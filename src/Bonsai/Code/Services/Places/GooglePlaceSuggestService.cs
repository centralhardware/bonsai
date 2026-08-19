using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Bonsai.Code.Services.Places;

/// <summary>
/// Suggests places using the Google Places API (Autocomplete).
/// </summary>
public class GooglePlaceSuggestService(HttpClient client, ILogger<GooglePlaceSuggestService> logger): IPlaceSuggestService
{
    private const string Endpoint = "https://places.googleapis.com/v1/places:autocomplete";
    private const int MinQueryLength = 3;

    public bool IsEnabled => true;

    /// <summary>
    /// Requests the matching places from Google.
    /// </summary>
    public async Task<IReadOnlyList<PlaceSuggestionVM>> SuggestAsync(string query, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < MinQueryLength)
            return [];

        var request = new AutocompleteRequest
        {
            Input = query.Trim(),
            LanguageCode = GetLanguageCode()
        };

        try
        {
            using var response = await client.PostAsJsonAsync(Endpoint, request, token);

            if (!response.IsSuccessStatusCode)
            {
                var details = await response.Content.ReadAsStringAsync(token);
                logger.LogWarning("Google Places API returned {Status}: {Details}", response.StatusCode, details);
                return [];
            }

            var result = await response.Content.ReadFromJsonAsync<AutocompleteResponse>(token);

            return (result?.Suggestions ?? [])
                   .Select(x => x.PlacePrediction)
                   .Where(x => x?.Text?.Text != null)
                   .Select(x => new PlaceSuggestionVM
                   {
                       Value = x.Text.Text,
                       Title = x.StructuredFormat?.MainText?.Text ?? x.Text.Text,
                       Description = x.StructuredFormat?.SecondaryText?.Text
                   })
                   .ToList();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to request place suggestions from Google Places API.");
            return [];
        }
    }

    /// <summary>
    /// Returns the language of the results (e.g. "ru" for the ru-RU locale).
    /// </summary>
    private static string GetLanguageCode()
    {
        return LocaleProvider.GetLocaleCode().Split('-')[0];
    }

    #region Contracts

    private class AutocompleteRequest
    {
        [JsonPropertyName("input")]
        public string Input { get; set; }

        [JsonPropertyName("languageCode")]
        public string LanguageCode { get; set; }
    }

    private class AutocompleteResponse
    {
        [JsonPropertyName("suggestions")]
        public IReadOnlyList<Suggestion> Suggestions { get; set; }
    }

    private class Suggestion
    {
        [JsonPropertyName("placePrediction")]
        public PlacePrediction PlacePrediction { get; set; }
    }

    private class PlacePrediction
    {
        [JsonPropertyName("text")]
        public LocalizedText Text { get; set; }

        [JsonPropertyName("structuredFormat")]
        public StructuredFormat StructuredFormat { get; set; }
    }

    private class StructuredFormat
    {
        [JsonPropertyName("mainText")]
        public LocalizedText MainText { get; set; }

        [JsonPropertyName("secondaryText")]
        public LocalizedText SecondaryText { get; set; }
    }

    private class LocalizedText
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }
    }

    #endregion
}
