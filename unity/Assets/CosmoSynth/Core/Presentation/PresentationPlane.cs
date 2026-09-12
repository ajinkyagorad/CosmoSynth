namespace CosmoSynth.Core
{
    /// <summary>Which physical plane is laid flat on the room (§5.2).</summary>
    public enum PresentationPlane
    {
        Horizon,
        LocalVertical,
        FocusEquator,
        Equatorial,
        Ecliptic,
        Galactic,
        Supergalactic
    }

    /// <summary>Everything <see cref="Planes.Up"/> needs, built by one caller and nobody else.</summary>
    public readonly struct PlaneContext
    {
        public readonly double SiteLatDeg;
        public readonly double SiteLonDeg;

        /// <summary>Viewer position relative to Earth's centre, ICRF km, for LocalVertical.</summary>
        public readonly Double3 ViewerKm;

        /// <summary>Catalogue id for FocusEquator; null otherwise.</summary>
        public readonly string FocusBodyId;

        public readonly CosmicTime Now;

        public PlaneContext(double siteLatDeg, double siteLonDeg, Double3 viewerKm,
            string focusBodyId, CosmicTime now)
        {
            SiteLatDeg = siteLatDeg;
            SiteLonDeg = siteLonDeg;
            ViewerKm = viewerKm;
            FocusBodyId = focusBodyId;
            Now = now;
        }
    }

    /// <summary>
    /// The one declaration of the up-vector function (R31).
    ///
    /// STUB, lane `nav-ui`: §5.2 assigns <c>Core/Presentation/PresentationPlane.cs</c> to the
    /// integrator at P0.3 and it was not in the foundation branch, but <c>Rung</c>, <c>ViewState</c>
    /// and <c>Destination</c> all name the type. Written here to the contract's exact signatures so
    /// the integrator can replace the file wholesale. Two known gaps, both marked below:
    /// <c>LocalVertical</c> reads <see cref="PlaneContext.ViewerKm"/> as already Earth-relative
    /// (the struct carries no Earth position to subtract), and <c>FocusEquator</c> resolves only
    /// the eleven bodies with an IAU rotation model in <see cref="BodyOrientation"/>.
    /// </summary>
    public static class Planes
    {
        /// <summary>The ICRF unit vector this plane places on room up.</summary>
        public static Double3 Up(PresentationPlane plane, in PlaneContext ctx)
        {
            switch (plane)
            {
                case PresentationPlane.Horizon:
                    return Zenith(ctx.SiteLatDeg, ctx.SiteLonDeg, ctx.Now);

                case PresentationPlane.LocalVertical:
                    // STUB: Earth-relative by assumption; the real driver subtracts Earth's
                    // absolute position before calling.
                    return ctx.ViewerKm.SqrMagnitude > 0.0 ? ctx.ViewerKm.Normalized : Double3.UnitZ;

                case PresentationPlane.FocusEquator:
                    return TryPole(ctx.FocusBodyId, ctx.Now, out Double3 pole) ? pole : Double3.UnitZ;

                case PresentationPlane.Ecliptic:
                    return StandardFrames.EclipticToEquatorial() * Double3.UnitZ;

                case PresentationPlane.Galactic:
                    return StandardFrames.GalacticToEquatorial * Double3.UnitZ;

                case PresentationPlane.Supergalactic:
                    return StandardFrames.SupergalacticToEquatorial * Double3.UnitZ;

                default:
                    return Double3.UnitZ;
            }
        }

        /// <summary>The local zenith at a place on the rotating Earth, as an ICRF direction.</summary>
        public static Double3 Zenith(double latitudeDeg, double longitudeDeg, CosmicTime now)
        {
            Double3 bodyFixed = BodyOrientation.SurfacePointCentric(latitudeDeg, longitudeDeg, 1.0, 1.0);
            return (BodyOrientation.Earth.BodyFixedToIcrf(now) * bodyFixed).Normalized;
        }

        private static bool TryPole(string bodyId, CosmicTime now, out Double3 pole)
        {
            pole = Double3.UnitZ;
            if (string.IsNullOrEmpty(bodyId)) return false;

            switch (bodyId.ToLowerInvariant())
            {
                case "sun": pole = BodyOrientation.Sun.PoleDirectionAt(now); return true;
                case "mercury": pole = BodyOrientation.Mercury.PoleDirectionAt(now); return true;
                case "venus": pole = BodyOrientation.Venus.PoleDirectionAt(now); return true;
                case "earth": pole = BodyOrientation.Earth.PoleDirectionAt(now); return true;
                case "moon": pole = BodyOrientation.Moon.PoleDirectionAt(now); return true;
                case "mars": pole = BodyOrientation.Mars.PoleDirectionAt(now); return true;
                case "jupiter": pole = BodyOrientation.Jupiter.PoleDirectionAt(now); return true;
                case "saturn": pole = BodyOrientation.Saturn.PoleDirectionAt(now); return true;
                case "uranus": pole = BodyOrientation.Uranus.PoleDirectionAt(now); return true;
                case "neptune": pole = BodyOrientation.Neptune.PoleDirectionAt(now); return true;
                case "pluto": pole = BodyOrientation.Pluto.PoleDirectionAt(now); return true;
                default: return false;
            }
        }

        /// <summary>The panel's name for a plane.</summary>
        public static string Name(PresentationPlane plane) => plane switch
        {
            PresentationPlane.Horizon => "Horizon",
            PresentationPlane.LocalVertical => "Local vertical",
            PresentationPlane.FocusEquator => "Focus equator",
            PresentationPlane.Equatorial => "Equatorial",
            PresentationPlane.Ecliptic => "Ecliptic",
            PresentationPlane.Galactic => "Galactic",
            PresentationPlane.Supergalactic => "Supergalactic",
            _ => "?"
        };
    }
}
