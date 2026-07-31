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
        public static readonly double[] WarpRates =
        {
            0.0,           // paused
            1.0,           // real time
            10.0,
            60.0,          // a minute per second
            600.0,
            3600.0,        // an hour per second
            21600.0,
            86400.0,       // a day per second
            604800.0,      // a week per second
            2592000.0,     // a month per second
            31557600.0,    // a year per second
            315576000.0,   // a decade per second
            3155760000.0,  // a century per second
            3.155760e11,   // ten millennia per second
            3.155760e13    // a million years per second
        };

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

        /// <summary>Simulated seconds elapsed per real second.</summary>
        public double WarpRate => _running ? WarpRates[Mathf.Clamp(_warpIndex, 0, WarpRates.Length - 1)] : 0.0;

        public int WarpIndex
        {
            get => _warpIndex;
            set => _warpIndex = Mathf.Clamp(value, 0, WarpRates.Length - 1);
        }

        public bool IsRunning
        {
            get => _running;
            set => _running = value;
        }

        public bool IsPaused => !_running || _warpIndex == 0;

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

        public void FasterWarp() => WarpIndex = _warpIndex + 1;

        public void SlowerWarp() => WarpIndex = _warpIndex - 1;

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
        public string WarpLabel
        {
            get
            {
                double rate = WarpRates[Mathf.Clamp(_warpIndex, 0, WarpRates.Length - 1)];
                if (!_running || rate == 0.0) return "paused";
                if (rate < 60.0) return $"{rate:0.#}x";
                if (rate < 3600.0) return $"{rate / 60.0:0.#} min/s";
                if (rate < 86400.0) return $"{rate / 3600.0:0.#} hr/s";
                if (rate < 31557600.0) return $"{rate / 86400.0:0.#} day/s";
                if (rate < 3.15576e11) return $"{rate / 31557600.0:0.#} yr/s";
                return $"{rate / 31557600.0:0.###e0} yr/s";
            }
        }

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
