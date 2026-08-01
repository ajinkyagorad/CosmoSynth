using System;

namespace CosmoSynth.Core
{
    /// <summary>
    /// A fixed place on a body's surface: a landing site, a rover, a base, an observatory.
    ///
    /// Coordinates are <b>planetocentric</b> — the angle subtended at the body's centre —
    /// because that is the convention almost every planetary dataset outside Earth is gridded
    /// on, and it is what JPL quotes for the Mars landing sites. Planetodetic latitude, the
    /// angle of the local surface normal, differs by up to 0.19 degrees on Mars, which is about
    /// 11 km on the ground: enough to put a rover in the wrong crater.
    /// <see cref="BodyOrientation.GeodeticToCentricLatitude"/> converts, for sources that
    /// publish the other one.
    /// </summary>
    [Serializable]
    public struct SurfaceSite
    {
        /// <summary>Planetocentric latitude, degrees north.</summary>
        public double LatitudeDeg;

        /// <summary>East longitude, degrees.</summary>
        public double EastLongitudeDeg;

        /// <summary>Height above the reference ellipsoid, km.</summary>
        public double AltitudeKm;

        /// <summary>Human-readable place name, e.g. "Jezero Crater".</summary>
        public string Name;

        public SurfaceSite(double latitudeDeg, double eastLongitudeDeg, double altitudeKm = 0.0,
            string name = null)
        {
            LatitudeDeg = latitudeDeg;
            EastLongitudeDeg = eastLongitudeDeg;
            AltitudeKm = altitudeKm;
            Name = name;
        }

        /// <summary>Position in the host body's rotating frame, km from its centre.</summary>
        public Double3 BodyFixedPositionKm(double equatorialRadiusKm, double polarRadiusKm) =>
            BodyOrientation.SurfacePointCentric(LatitudeDeg, EastLongitudeDeg,
                equatorialRadiusKm, polarRadiusKm, AltitudeKm);

        public JsonValue ToJson()
        {
            var json = JsonValue.Object();
            json["latitudeDeg"] = JsonValue.Number(LatitudeDeg);
            json["eastLongitudeDeg"] = JsonValue.Number(EastLongitudeDeg);
            if (AltitudeKm != 0.0) json["altitudeKm"] = JsonValue.Number(AltitudeKm);
            if (!string.IsNullOrEmpty(Name)) json["name"] = JsonValue.String(Name);
            return json;
        }

        public static SurfaceSite FromJson(JsonValue json) =>
            new SurfaceSite(
                json.RequireDouble("latitudeDeg"),
                json.RequireDouble("eastLongitudeDeg"),
                json["altitudeKm"].AsDouble(),
                json["name"].AsString());

        public override string ToString()
        {
            string place = string.IsNullOrEmpty(Name) ? string.Empty : $" ({Name})";
            char northSouth = LatitudeDeg >= 0.0 ? 'N' : 'S';
            return $"{System.Math.Abs(LatitudeDeg):F4}{northSouth} {EastLongitudeDeg:F4}E{place}";
        }
    }
}
