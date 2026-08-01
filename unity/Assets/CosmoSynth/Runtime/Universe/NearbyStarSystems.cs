using System.Collections.Generic;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Gives the star you are near its worlds, and takes them away when you leave.
    ///
    /// This is the component that turns a sky into a universe. The catalogue holds 119 613 real
    /// stars but only their light; approach one and this generates its planets, moons and
    /// surfaces from a seed derived from the star's own measured position, so the system is the
    /// same one every time anybody arrives, on any machine, forever.
    ///
    /// **Only one system exists at a time.** Generating a system costs a dozen bodies and a
    /// dozen GameObjects, and 119 613 of those is not a design, it is an out-of-memory error. The
    /// nearest star within reach has worlds; every other star is a point of light until you go
    /// there. That is exactly how it feels from inside, because a star system is invisible from
    /// the next star along anyway.
    ///
    /// **Nothing is persisted.** A generated system is a pure function of the star, so it is
    /// rebuilt on arrival rather than stored. That also means it can never drift out of step with
    /// the catalogue it came from.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("CosmoSynth/Nearby Star Systems")]
    public sealed class NearbyStarSystems : MonoBehaviour
    {
        [Tooltip("Generate the nearest star's system once the viewer is this close, in parsecs. " +
                 "One parsec keeps it off until you have actually left the solar system.")]
        [SerializeField] private float _generationRadiusParsecs = 1.0f;

        [Tooltip("Hysteresis: the system is discarded only past this multiple of the radius, so " +
                 "hovering at the boundary does not rebuild the scene every frame.")]
        [SerializeField] private float _releaseMultiplier = 1.35f;

        [Tooltip("Seconds between checks. The viewer cannot cross a parsec in a frame.")]
        [SerializeField] private float _checkInterval = 0.5f;

        [Tooltip("How many of the brightest catalogue stars are candidates. Scanning all 119613 " +
                 "every check is wasted work: the faint ones are mostly distant.")]
        [SerializeField] private int _candidateStars = 20000;

        private UniverseSimulation _simulation;
        private SkyAndRings _sky;

        private float _nextCheck;

        /// <summary>Ids the last generated system added, so exactly those can be removed again.</summary>
        private readonly List<string> _generated = new List<string>();

        /// <summary>Catalogue index of the star whose system is loaded, or -1.</summary>
        private int _activeStar = -1;

        /// <summary>The star whose system is currently generated, for the HUD.</summary>
        public string ActiveStarId { get; private set; }

        /// <summary>Bodies the current generated system contributes.</summary>
        public int GeneratedBodyCount => _generated.Count;

        private UniverseSimulation Simulation =>
            _simulation != null ? _simulation : (_simulation = GetComponent<UniverseSimulation>());

        private SkyAndRings Sky =>
            _sky != null ? _sky : (_sky = FindAnyObjectByType<SkyAndRings>());

        private void Update()
        {
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + Mathf.Max(0.05f, _checkInterval);

            if (Simulation?.Catalog == null) return;

            StarCatalog stars = Sky != null ? Sky.Stars : null;
            if (stars == null || stars.Count == 0) return;      // sky still streaming in

            Camera camera = Camera.main;
            if (camera == null) return;

            // Where the viewer actually is, in absolute kilometres. Going through the origin
            // rather than reading a transform is the only way to get this right: bodies are
            // placed with localPosition under the simulation transform, which is the universe's
            // anchor in the room, and at tabletop scale that transform is worth light years.
            Vector3 inUniverse = Simulation.transform.InverseTransformPoint(camera.transform.position);
            Double3 viewerKm = Simulation.Origin.ToAbsolute(inUniverse);

            int nearest = NearestStar(stars, viewerKm, out double nearestParsecs);

            double release = _generationRadiusParsecs * Mathf.Max(1.01f, _releaseMultiplier);

            if (_activeStar >= 0 && (nearest != _activeStar || nearestParsecs > release))
            {
                ReleaseSystem();
            }

            if (_activeStar < 0 && nearest >= 0 && nearestParsecs <= _generationRadiusParsecs)
            {
                GenerateSystem(stars, nearest);
            }
        }

        /// <summary>Index of the nearest candidate star to a point, and its distance in parsecs.</summary>
        private int NearestStar(StarCatalog stars, Double3 viewerKm, out double nearestParsecs)
        {
            int best = -1;
            double bestSquared = double.PositiveInfinity;

            Double3 viewerParsecs = viewerKm / Constants.Parsec;
            int limit = Mathf.Min(stars.Count, Mathf.Max(1, _candidateStars));

            for (int i = 0; i < limit; i++)
            {
                CatalogStar star = stars.Stars[i];

                // A star with no parallax has no position to fly to — only a direction. Its
                // distance is not known, so "am I near it" has no answer and it cannot host a
                // system. Placing it at HYG's 100 000 pc sentinel would invent one.
                if ((star.Flags & StarFlags.DistanceUnknown) != 0) continue;

                double squared = (star.PositionParsecs - viewerParsecs).SqrMagnitude;
                if (squared < bestSquared)
                {
                    bestSquared = squared;
                    best = i;
                }
            }

            nearestParsecs = best >= 0 ? System.Math.Sqrt(bestSquared) : double.PositiveInfinity;
            return best;
        }

        private void GenerateSystem(StarCatalog stars, int index)
        {
            CatalogStar star = stars.Stars[index];
            string id = $"hyg-{index}";

            CelestialCatalog live = Simulation.Catalog;

            // Do not invent worlds for a star that already has real ones. The exoplanet
            // catalogue puts 60 host stars at their measured positions with measured planets,
            // and generating over one of those would replace knowledge with invention — the
            // exact failure the provenance system exists to prevent. Matched by position rather
            // than by name, because the two catalogues do not share an identifier scheme.
            Double3 positionKm = star.PositionParsecs * Constants.Parsec;
            const double sameStarKm = 0.05 * Constants.Parsec;

            foreach (CelestialBody body in live.Bodies)
            {
                if (!body.IsStar || !body.PositionKm.HasValue) continue;
                if (Double3.Distance(body.PositionKm.Value, positionKm) < sameStarKm)
                {
                    // Already represented, with better data than we could make up.
                    _activeStar = index;
                    ActiveStarId = body.Id;
                    return;
                }
            }

            // Named after its catalogue index for now. The 548 stars with proper names ship in
            // sky/stars_named.json and arriving at "Vega" would read far better than "hyg-42",
            // but that is a second asset to stream and it is not what makes the worlds appear.
            CelestialCatalog generated = StarSystemFactory.Create(
                star, id, id, Simulation.Clock.Now);

            _generated.Clear();
            foreach (CelestialBody body in generated.Bodies) _generated.Add(body.Id);

            int added = Simulation.AddBodies(generated);
            _activeStar = index;
            ActiveStarId = id;

            Debug.Log($"[CosmoSynth] generated {added} bodies around {ActiveStarId} " +
                      $"(mag {star.ApparentMagnitude:F2}, {star.TemperatureK:F0} K, " +
                      $"seed {StarSystemFactory.SeedFor(star)}) — planets are PROCEDURAL");
        }

        private void ReleaseSystem()
        {
            CelestialCatalog live = Simulation?.Catalog;
            if (live == null) { _generated.Clear(); _activeStar = -1; ActiveStarId = null; return; }

            int removed = Simulation.RemoveBodies(_generated);
            _generated.Clear();

            string was = ActiveStarId;
            _activeStar = -1;
            ActiveStarId = null;

            if (removed > 0) Debug.Log($"[CosmoSynth] released {removed} generated bodies around {was}");
        }

        /// <summary>One line for the diagnostics readout.</summary>
        public string StatusLine() =>
            _activeStar < 0
                ? "nearby system: none (no star within range)"
                : $"nearby system: {ActiveStarId}, {_generated.Count} generated bodies";
    }
}
