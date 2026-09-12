using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// A shim. The dashboard itself is gone — <see cref="WristPanel"/> replaces it — and this holds
    /// only the three members the play-mode harness calls.
    ///
    /// <b>To be deleted by the integrator</b> once <c>PlayModeShot</c> moves from <c>rung&lt;N&gt;</c>
    /// to the contract's <c>rung:&lt;id&gt;</c> view (§5.7.6). It is here rather than the file simply
    /// being removed because <c>PlayModeShot.cs</c> is integrator-owned and deleting a type it
    /// references would break the Editor compile in every lane at once.
    ///
    /// The forwarding is not a no-op: <c>rung&lt;N&gt;</c> now means the contract's nineteen rungs
    /// (<c>LayerTable.Rungs</c>) rather than the old hand-kept ladder that stopped at 130 light
    /// years, so an existing harness view exercises the new ladder without being rewritten.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class WristDashboard : MonoBehaviour
    {
        /// <summary>Nineteen, from <c>LayerTable.Rungs</c>.</summary>
        public static int ScaleRungCount => LayerTable.Rungs.Length;

        public static string ScaleRungName(int index) =>
            index >= 0 && index < LayerTable.Rungs.Length ? LayerTable.Rungs[index].Caption : "(no such rung)";

        /// <summary>Travel to a rung by index, which is what the harness's <c>rung&lt;N&gt;</c> asks for.</summary>
        public void ApplyScaleRung(int index)
        {
            if (index < 0 || index >= LayerTable.Rungs.Length) return;

            var travel = FindAnyObjectByType<TravelController>();
            if (travel == null)
            {
                Debug.LogWarning("[CosmoSynth] no TravelController: cannot apply a scale rung");
                return;
            }

            travel.TravelToRung(LayerTable.Rungs[index].Id);
        }
    }
}
