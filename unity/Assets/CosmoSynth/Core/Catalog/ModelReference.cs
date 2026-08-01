using System;

namespace CosmoSynth.Core
{
    /// <summary>
    /// A shipped mesh to draw in place of the default sphere, and how big it really is.
    ///
    /// Only spacecraft use this. A planet is a sphere to well within what any renderer can
    /// show, so generating one is both cheaper and more accurate than shipping geometry; a
    /// spacecraft is an irregular assembly of booms and dishes that no procedure will invent.
    ///
    /// <see cref="LengthMetres"/> is what the model's longest axis means in reality. It is
    /// carried here rather than baked into the mesh so the honest size is available to the UI
    /// even though the drawn size is not honest: a 13 m probe placed against a 143 000 km
    /// planet is far below one pixel at any scale that shows both, so the renderer enforces a
    /// minimum apparent size and the panel says what the real dimension is.
    /// </summary>
    [Serializable]
    public struct ModelReference
    {
        /// <summary>Path under StreamingAssets, e.g. "CosmoSynth/models/voyager-1.csmesh".</summary>
        public string Path;

        /// <summary>Real-world length of the model's longest axis, metres.</summary>
        public double LengthMetres;

        /// <summary>Who made the model and under what terms.</summary>
        public string Attribution;

        /// <summary>Where it came from, for the provenance panel.</summary>
        public string SourceUrl;

        public ModelReference(string path, double lengthMetres, string attribution = null,
            string sourceUrl = null)
        {
            Path = path;
            LengthMetres = lengthMetres;
            Attribution = attribution;
            SourceUrl = sourceUrl;
        }

        public bool IsPresent => !string.IsNullOrEmpty(Path);

        public JsonValue ToJson()
        {
            var json = JsonValue.Object();
            json["path"] = JsonValue.String(Path);
            json["lengthMetres"] = JsonValue.Number(LengthMetres);
            if (!string.IsNullOrEmpty(Attribution)) json["attribution"] = JsonValue.String(Attribution);
            if (!string.IsNullOrEmpty(SourceUrl)) json["sourceUrl"] = JsonValue.String(SourceUrl);
            return json;
        }

        public static ModelReference FromJson(JsonValue json) =>
            new ModelReference(
                json.RequireString("path"),
                json["lengthMetres"].AsDouble(),
                json["attribution"].AsString(),
                json["sourceUrl"].AsString());

        public override string ToString() => $"{Path} ({LengthMetres:G4} m)";
    }
}
