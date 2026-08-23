using MelonLoader;
using ModsPanel;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;

namespace BoxroomRadioStreams
{
    /// <summary>
    /// Builds the station browser workflow while leaving rendering, focus,
    /// scrolling, cursor capture, and input locking to ModsPanel.
    /// </summary>
    internal static class StationMenu
    {
        private const string ApiRoot = "https://de1.api.radio-browser.info/json";
        private const int PageSize = 75;
        private static Interactable_Radio target;
        private static ModMenu menu;
        private static string stationSearch = "";
        private static string genreSearch = "";

        /// <summary>Opens the root screen for a specific placed radio.</summary>
        internal static void Open(Interactable_Radio radio)
        {
            target = radio;
            StationStore.Reload();
            menu = ModsUi.CreateMenu("MidgetBrony.BoxroomRadioStreams", "Internet Radio", "Icecast / SHOUTcast station picker");
            menu.Eyebrow = "BOXROOM RADIO";
            menu.AddHeading("Saved stations").AddLabel("Choose a favorite, or edit UserData/RadioStreams/stations.json.");
            foreach (Station station in StationStore.Stations) AddStationButton(station);
            if (StationStore.Stations.Count == 0) menu.AddLabel("No saved favorites yet.");

            menu.AddSpacer(12).AddHeading("Search the full directory")
                .AddTextInput("Station name", () => stationSearch, value => stationSearch = value, "Any station")
                .AddTextInput("Genre", () => genreSearch, value => genreSearch = value, "Rock, jazz, news, ambient…")
                .AddButton("Search stations", () => Search(0, false), "FULL RADIO-BROWSER DIRECTORY")
                .AddButton("Browse popular stations", () => Search(0, true), "NO SEARCH FILTER")
                .AddButton("Stop internet radio", () => { StreamPlayer.Stop(); menu.Close(); }, "RETURN RADIO TO OFF");
            menu.Show();
        }

        /// <summary>Adds a directory or favorite result using one visual format.</summary>
        private static void AddStationButton(Station station)
        {
            string detail = string.Join("  •  ", new[] { station.Country, station.Codec, station.Bitrate > 0 ? station.Bitrate + " KBPS" : null }.WhereNotEmpty());
            menu.AddButton(station.Name, () => OpenStation(station), detail);
        }

        /// <summary>Separates Play and Favorite so selection never starts audio unexpectedly.</summary>
        private static void OpenStation(Station station)
        {
            bool favorite = StationStore.Contains(station.Url);
            menu.Clear().AddHeading(station.Name)
                .AddLabel(string.Join("  •  ", new[] { station.Country, station.Codec, station.Bitrate > 0 ? station.Bitrate + " KBPS" : null }.WhereNotEmpty()))
                .AddButton("Play station", () => Play(station), "PLAY FROM THIS RADIO")
                .AddButton(favorite ? "Remove from favorites" : "Add to favorites", () => ToggleFavorite(station, favorite), "SAVED IN STATIONS.JSON")
                .AddSpacer(8).AddButton("Back", () => Open(target));
        }

        /// <summary>Persists the favorite change and refreshes the action label.</summary>
        private static void ToggleFavorite(Station station, bool favorite)
        {
            if (favorite) { StationStore.Remove(station.Url); ModsUi.ShowToast("Removed favorite: " + station.Name, 3f); }
            else { StationStore.Add(station); ModsUi.ShowToast("Favorite saved: " + station.Name, 3f); }
            OpenStation(station);
        }

        /// <summary>Closes UI before handing ownership to the positional player.</summary>
        private static void Play(Station station)
        {
            Interactable_Radio radio = target;
            menu.Close();
            StreamPlayer.Play(radio, station);
        }

        /// <summary>Starts a paged search and replaces stale menu contents with status.</summary>
        private static void Search(int offset, bool browse)
        {
            menu.Clear().AddHeading(browse ? "Popular stations" : "Station search")
                .AddLabel("Searching the full public radio directory…");
            MelonCoroutines.Start(LoadDirectory(offset, browse));
        }

        /// <summary>
        /// Queries the complete Radio Browser index. Search mode sends name and
        /// tag filters to the service; browse mode follows its popularity list.
        /// </summary>
        private static IEnumerator LoadDirectory(int offset, bool browse)
        {
            string url = browse
                ? ApiRoot + "/stations/topclick/" + PageSize + "?hidebroken=true&offset=" + offset
                : ApiRoot + "/stations/search?hidebroken=true&codec=MP3&order=clickcount&reverse=true" +
                  "&limit=" + PageSize + "&offset=" + offset +
                  "&name=" + UnityWebRequest.EscapeURL(stationSearch ?? "") +
                  "&tag=" + UnityWebRequest.EscapeURL(genreSearch ?? "");

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("User-Agent", "BOXROOM-RadioStreams/1.2");
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    menu.Clear().AddHeading("Directory unavailable").AddLabel(request.error).AddButton("Back", () => Open(target));
                    yield break;
                }
                try
                {
                    JArray rows = JArray.Parse(request.downloadHandler.text);
                    menu.Clear().AddHeading(browse ? "Popular MP3 stations" : "Search results")
                        .AddLabel(browse ? "Live Icecast and SHOUTcast streams from Radio Browser."
                            : "Name: " + (string.IsNullOrWhiteSpace(stationSearch) ? "ANY" : stationSearch) +
                              "  •  Genre: " + (string.IsNullOrWhiteSpace(genreSearch) ? "ANY" : genreSearch));
                    int count = 0;
                    foreach (JToken row in rows)
                    {
                        string codec = ((string)row["codec"] ?? "").ToUpperInvariant();
                        string streamUrl = (string)row["url_resolved"] ?? (string)row["url"];
                        // BOXROOM's current FMOD path is intentionally limited to
                        // direct MP3 streams. AAC/HLS results would fail later.
                        if (codec != "MP3" || !StationStore.IsHttpUrl(streamUrl)) continue;
                        AddStationButton(new Station { Name = (string)row["name"] ?? "Unnamed station", Url = streamUrl, Country = (string)row["countrycode"], Codec = codec, Bitrate = (int?)row["bitrate"] ?? 0 });
                        count++;
                    }
                    if (count == 0) menu.AddLabel("No compatible MP3 stations matched that search.");
                    if (rows.Count == PageSize) menu.AddSpacer(8).AddButton("Next " + PageSize + " results", () => Search(offset + PageSize, browse), "PAGE " + ((offset / PageSize) + 2));
                    if (offset > 0) menu.AddButton("Previous results", () => Search(Math.Max(0, offset - PageSize), browse));
                    menu.AddButton("New search", () => Open(target));
                }
                catch (Exception ex)
                {
                    menu.Clear().AddHeading("Directory response error").AddLabel(ex.Message).AddButton("Back", () => Open(target));
                }
            }
        }

        /// <summary>Prevents blank metadata fields from producing empty separators.</summary>
        private static IEnumerable<string> WhereNotEmpty(this IEnumerable<string> values)
        {
            foreach (string value in values) if (!string.IsNullOrWhiteSpace(value)) yield return value;
        }
    }
}
