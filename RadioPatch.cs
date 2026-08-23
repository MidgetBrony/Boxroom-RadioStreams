using HarmonyLib;
using UnityEngine;

namespace BoxroomRadioStreams
{
    /// <summary>
    /// Adapts BOXROOM's existing radio interaction without replacing its normal
    /// click behavior. This class is unnecessary when merged into game source.
    /// </summary>
    [HarmonyPatch(typeof(Interactable_Radio), nameof(Interactable_Radio.OnInteract))]
    internal static class RadioPatch
    {
        private static bool Prefix(Interactable_Radio __instance)
        {
            // Shift-click is intentionally the only overridden interaction.
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (shift)
            {
                StationMenu.Open(__instance);
                return false;
            }

            // Internet audio must release its FMOD channel before vanilla radio
            // playback claims the same physical radio.
            StreamPlayer.StopForVanillaInteraction();
            return true;
        }

        [HarmonyPatch(typeof(Interactable_Radio), "OnDestroy")]
        [HarmonyPostfix]
        // A stream must never outlive the scene object that emits its 3D audio.
        private static void RadioDestroyed(Interactable_Radio __instance) => StreamPlayer.StopIfOwnedBy(__instance);
    }
}
