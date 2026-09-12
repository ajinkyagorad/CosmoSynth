namespace CosmoSynth.Core
{
    /// <summary>A place on Earth the observed sky can be computed from.</summary>
    public readonly struct ObservingSite
    {
        public readonly string Id;
        public readonly string Name;

        /// <summary>Geodetic latitude, degrees north.</summary>
        public readonly double LatitudeDeg;

        /// <summary>Longitude, degrees east of Greenwich.</summary>
        public readonly double LongitudeDeg;

        public ObservingSite(string id, string name, double latitudeDeg, double longitudeDeg)
        {
            Id = id;
            Name = name;
            LatitudeDeg = latitudeDeg;
            LongitudeDeg = longitudeDeg;
        }

        public override string ToString() => $"{Name} ({LatitudeDeg:F2}, {LongitudeDeg:F2})";
    }

    /// <summary>
    /// The twelve places TOOLS -&gt; OBSERVED SKY offers, plus the LAT/LON steppers' bounds.
    ///
    /// Chosen to span both hemispheres and the whole range of latitude, because the one thing a
    /// sky-from-a-place control is for is showing that the sky is different somewhere else: a list
    /// of twelve northern cities would demonstrate nothing. Measured constants, city-hall
    /// coordinates rounded to 0.01 degrees (about 1 km), which is far inside the angular
    /// resolution of anything drawn.
    /// </summary>
    public static class ObservingSites
    {
        /// <summary>Source for every row: the WGS84 coordinates published for each city centre.</summary>
        public const string Source = "City-centre coordinates, WGS84, rounded to 0.01 deg";

        public static readonly ObservingSite[] All =
        {
            new ObservingSite("helsinki", "Helsinki", 60.17, 24.94),
            new ObservingSite("tokyo", "Tokyo", 35.69, 139.69),
            new ObservingSite("mumbai", "Mumbai", 19.08, 72.88),
            new ObservingSite("new-york", "New York", 40.71, -74.01),
            new ObservingSite("los-angeles", "Los Angeles", 34.05, -118.24),
            new ObservingSite("honolulu", "Honolulu", 21.31, -157.86),
            new ObservingSite("nairobi", "Nairobi", -1.29, 36.82),
            new ObservingSite("sao-paulo", "Sao Paulo", -23.55, -46.63),
            new ObservingSite("santiago", "Santiago", -33.45, -70.67),
            new ObservingSite("cape-town", "Cape Town", -33.92, 18.42),
            new ObservingSite("sydney", "Sydney", -33.87, 151.21),
            new ObservingSite("south-pole", "South Pole", -90.00, 0.00)
        };

        /// <summary>Helsinki, because that is where the app is being built and tested.</summary>
        public static ObservingSite Default => All[0];

        public static bool TryFind(string id, out ObservingSite site)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (!string.Equals(All[i].Id, id, System.StringComparison.OrdinalIgnoreCase)) continue;
                site = All[i];
                return true;
            }
            site = Default;
            return false;
        }

        /// <summary>Clamp a hand-entered latitude and wrap a longitude into (-180, 180].</summary>
        public static ObservingSite Custom(double latitudeDeg, double longitudeDeg)
        {
            double lat = MathD.Clamp(latitudeDeg, -90.0, 90.0);
            double lon = MathD.WrapPi(longitudeDeg * MathD.Deg2Rad) * MathD.Rad2Deg;
            return new ObservingSite("custom", "Custom site", lat, lon);
        }
    }
}
