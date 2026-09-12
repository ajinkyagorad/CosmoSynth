using System;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// The simulation's clock: an absolute instant that advances at a controllable multiple of
    /// real time, from paused to millions of years per second.
    ///
    /// Simulation time is held as a double in <see cref="CosmicTime"/> and advanced by
    /// accumulating <c>Time.deltaTime * scale</c>. That accumulation is the one place a
    /// long-running session can silently lose precision, so it is done in double and never
    /// round-tripped through a float.
    ///
    /// Time acceleration is quantised to a ladder of preset rates rather than being freely
    /// continuous. Two reasons: a rate the user can name ("one day per second") is far more
    /// useful than an arbitrary one, and a continuously-varying rate makes it impossible to
    /// reproduce a moment you just flew past.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("CosmoSynth/Simulation Clock")]
    public sealed class SimulationClock : MonoBehaviour
    {
        /// <summary>
        /// The time-warp ladder, in simulated seconds per real second. Each step is a round
        /// number a person can hold in their head.
        /// </summary>
        /// <summary>
        /// The ladder itself moved to <c>Core/Time/WarpLadder.cs</c> (CONTRACT 5.8.1): 28 signed
        /// steps, mirrored about real time, with <b>no zero</b> — pause is a separate state that
        /// keeps the warp, so stepping out of pause returns to the rate you were running at rather
        /// than to whichever side the last press came from.
        ///
        /// This array survives as a forward for anything still reading the old forward-only one.
        /// </summary>
        public static double[] WarpRates
        {
            get
            {
                var rates = new double[WarpLadder.MaxIndex + 1];
                for (int i = 1; i <= WarpLadder.MaxIndex; i++) rates[i] = WarpLadder.Rate(i);
                return rates;
            }
        }

        [Header("Epoch")]
        [Tooltip("ISO-8601 UTC instant the simulation starts from. Empty means the real current time.")]
        [SerializeField] private string _startEpochUtc = "";

        [Header("Time warp")]
        [Tooltip("Index into the warp ladder. 1 is real time.")]
        [SerializeField] private int _warpIndex = 1;

        [Tooltip("Advance automatically. Turn off to scrub time by hand.")]
        [SerializeField] private bool _running = true;

        private CosmicTime _now;
        private bool _initialised;

        /// <summary>Raised whenever the epoch is set explicitly, as opposed to advancing normally.</summary>
        public event Action<CosmicTime> EpochChanged;

        /// <summary>The current simulation instant.</summary>
        public CosmicTime Now
        {
            get
            {
                EnsureInitialised();
                return _now;
            }
            set
            {
                _now = value;
                _initialised = true;
                EpochChanged?.Invoke(_now);
            }
        }

        /// <summary>Simulated seconds elapsed per real second. Negative runs time backwards.</summary>
        public double WarpRate => _running ? WarpLadder.Rate(_warpIndex) : 0.0;

        /// <summary>Signed index into the warp ladder: -14 to +14, never 0.</summary>
        public int WarpIndex
        {
            get => _warpIndex;
            set => _warpIndex = WarpLadder.Clamp(value);
        }

        public bool IsRunning
        {
            get => _running;
            set => _running = value;
        }

        /// <summary>Pause is its own state, and it keeps the warp it was running at.</summary>
        public bool IsPaused => !_running;

        /// <summary>Simulated seconds that elapsed during the last frame.</summary>
        public double LastStepSeconds { get; private set; }

        private void Awake() => EnsureInitialised();

        private void EnsureInitialised()
        {
            if (_initialised) return;
            _initialised = true;

            if (!string.IsNullOrWhiteSpace(_startEpochUtc) && TryParseIso8601(_startEpochUtc, out CosmicTime parsed))
            {
                _now = parsed;
                return;
            }

            // Default to the machine's current time, so the app opens on the sky as it is now.
            DateTime utc = DateTime.UtcNow;
            _now = CosmicTime.FromUtc(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute,
                utc.Second + utc.Millisecond / 1000.0);
        }

        private void Update()
        {
            EnsureInitialised();

            double rate = WarpRate;
            if (rate == 0.0)
            {
                LastStepSeconds = 0.0;
                return;
            }

            // unscaledDeltaTime, not deltaTime: Time.timeScale belongs to the engine's own
            // slow-motion and pause machinery, and letting it multiply the warp rate would
            // make the astronomical clock depend on an unrelated gameplay setting.
            LastStepSeconds = (double)Time.unscaledDeltaTime * rate;
            _now += LastStepSeconds;
        }

        public void FasterWarp() => WarpIndex = WarpLadder.Step(_warpIndex, 1);

        public void SlowerWarp() => WarpIndex = WarpLadder.Step(_warpIndex, -1);

        public void TogglePause() => _running = !_running;

        /// <summary>Jump to a UTC calendar instant.</summary>
        public void SetEpochUtc(int year, int month, int day, int hour = 0, int minute = 0, double second = 0.0) =>
            Now = CosmicTime.FromUtc(year, month, day, hour, minute, second);

        /// <summary>Jump to the machine's current time.</summary>
        public void SetEpochToNow()
        {
            DateTime utc = DateTime.UtcNow;
            SetEpochUtc(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, utc.Second + utc.Millisecond / 1000.0);
        }

        /// <summary>A short label for the current warp rate, for the HUD.</summary>
        public string WarpLabel => _running ? WarpLadder.Label(_warpIndex) : "paused";

        /// <summary>
        /// Parse a subset of ISO-8601: <c>YYYY-MM-DD</c> optionally followed by
        /// <c>THH:MM:SS(.fff)</c> and a <c>Z</c>. Deliberately strict — a date that silently
        /// fails to parse would start the simulation at the wrong epoch, and every position in
        /// the scene would be wrong in a way that looks entirely plausible.
        /// </summary>
        public static bool TryParseIso8601(string text, out CosmicTime result)
        {
            result = CosmicTime.J2000;
            if (string.IsNullOrWhiteSpace(text)) return false;

            text = text.Trim();
            if (text.EndsWith("Z", StringComparison.OrdinalIgnoreCase)) text = text.Substring(0, text.Length - 1);

            string[] halves = text.Split('T', ' ');
            string[] date = halves[0].Split('-');
            if (date.Length != 3) return false;

            // A leading '-' would mean a negative (BCE) year, which this format does not cover.
            if (!int.TryParse(date[0], out int year) ||
                !int.TryParse(date[1], out int month) ||
                !int.TryParse(date[2], out int day)) return false;

            if (month < 1 || month > 12 || day < 1 || day > 31) return false;

            int hour = 0, minute = 0;
            double second = 0.0;

            if (halves.Length > 1)
            {
                string[] time = halves[1].Split(':');
                if (time.Length > 0 && !int.TryParse(time[0], out hour)) return false;
                if (time.Length > 1 && !int.TryParse(time[1], out minute)) return false;
                if (time.Length > 2 && !double.TryParse(time[2],
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out second)) return false;

                if (hour < 0 || hour > 23 || minute < 0 || minute > 59 || second < 0.0 || second >= 61.0) return false;
            }

            result = CosmicTime.FromUtc(year, month, day, hour, minute, second);
            return true;
        }
    }
}
