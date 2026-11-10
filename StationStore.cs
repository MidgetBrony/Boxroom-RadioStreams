using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace BoxroomRadioStreams
{
    /// <summary>A validated internet-radio station shown by the menu.</summary>
    internal sealed class Station
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public string Country { get; set; }
        public string Codec { get; set; }
        public int Bitrate { get; set; }
    }

    /// <summary>JSON root object; keeps the on-disk format extensible.</summary>
    internal sealed class StationFile
    {
        public List<Station> Stations { get; set; } = new List<Station>();
    }

    /// <summary>
    /// Owns favorite persistence. Directory results remain transient until the
    /// player explicitly adds them to this collection.
    /// </summary>
    internal static class StationStore
    {
        internal static readonly string DirectoryPath = Path.Combine(MelonEnvironment.UserDataDirectory, "RadioStreams");
        internal static readonly string FilePath = Path.Combine(DirectoryPath, "stations.json");
        internal static List<Station> Stations { get; private set; } = new List<Station>();

        internal static void Initialize()
        {
            Directory.CreateDirectory(DirectoryPath);
            if (!File.Exists(FilePath))
            {
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(new StationFile
                {
                    Stations = new List<Station>
                    {
                        // Keep a discoverable example while avoiding a bundled
                        // broadcaster whose URL or licensing could later change.
                        new Station { Name = "Example station - edit stations.json", Url = "https://your-station.example/stream", Codec = "MP3" }
                    }
                }, Formatting.Indented));
            }
            Reload();
        }

        /// <summary>Reloads and validates favorites after manual JSON edits.</summary>
        internal static void Reload()
        {
            try
            {
                StationFile file = JsonConvert.DeserializeObject<StationFile>(File.ReadAllText(FilePath));
                Stations = file?.Stations ?? new List<Station>();
                Stations.RemoveAll(s => s == null || string.IsNullOrWhiteSpace(s.Name) || !TryNormalizeHttpUrl(s.Url, out _));
                foreach (Station station in Stations)
                {
                    TryNormalizeHttpUrl(station.Url, out string normalized);
                    station.Url = normalized;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[RadioStreams] Could not read stations.json: " + ex.Message);
                Stations = new List<Station>();
            }
        }

        /// <summary>Matches favorites by stream URL, independent of casing.</summary>
        internal static bool Contains(string url) =>
            Stations.Exists(s => string.Equals(s.Url, url, StringComparison.OrdinalIgnoreCase));

        /// <summary>Adds a station once and immediately persists the file.</summary>
        internal static void Add(Station station)
        {
            Reload();
            if (!Contains(station.Url)) Stations.Add(station);
            Save();
        }

        /// <summary>Removes every entry that refers to the supplied stream URL.</summary>
        internal static void Remove(string url)
        {
            Reload();
            Stations.RemoveAll(s => string.Equals(s.Url, url, StringComparison.OrdinalIgnoreCase));
            Save();
        }

        private static void Save()
        {
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(
                new StationFile { Stations = Stations }, Formatting.Indented));
        }

        /// <summary>Accepts and canonicalizes only direct absolute HTTP(S) endpoints.</summary>
        internal static bool TryNormalizeHttpUrl(string value, out string normalized)
        {
            normalized = null;
            if (string.IsNullOrWhiteSpace(value) ||
                !Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                string.IsNullOrWhiteSpace(uri.Host))
            {
                return false;
            }

            normalized = uri.AbsoluteUri;
            return true;
        }

        internal static bool IsHttpUrl(string value) => TryNormalizeHttpUrl(value, out _);
    }
}
