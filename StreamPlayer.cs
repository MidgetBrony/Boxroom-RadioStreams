using HarmonyLib;
using MelonLoader;
using System.Collections;
using FMOD;
using FMODUnity;
using UnityEngine;

namespace BoxroomRadioStreams
{
    /// <summary>
    /// Owns the single active internet stream. It uses BOXROOM's existing FMOD
    /// core system so volume, output device, and listener state stay game-owned.
    /// </summary>
    internal static class StreamPlayer
    {
        private static Interactable_Radio owner;
        private static FMOD.Sound streamSound;
        private static FMOD.Channel streamChannel;
        private static int generation;

        /// <summary>Stops previous playback and begins tuning from a placed radio.</summary>
        internal static void Play(Interactable_Radio radio, Station station)
        {
            Stop();
            if (radio == null) return;

            // Turn BOXROOM's FMOD/album playback off before claiming this radio.
            // Native integration should replace this reflection with a shared
            // radio playback-mode transition.
            AccessTools.Method(typeof(Interactable_Radio), "TurnOff")?.Invoke(radio, null);
            owner = radio;
            int token = ++generation;
            ModsPanel.ModsUi.ShowToast("Tuning: " + station.Name, 4f);
            MelonLogger.Msg("[RadioStreams] Opening FMOD stream: " + station.Name + " (" + station.Url + ")");
            MelonCoroutines.Start(LoadStream(station, token));
        }

        /// <summary>Waits for non-blocking FMOD creation before starting the channel.</summary>
        private static IEnumerator LoadStream(Station station, int token)
        {
            // CREATESTREAM avoids downloading an endless broadcast. NONBLOCKING
            // keeps network/TLS work off the frame that handled the UI click.
            MODE mode = MODE.CREATESTREAM | MODE.NONBLOCKING | MODE._3D | MODE._3D_WORLDRELATIVE | MODE.MPEGSEARCH;
            RESULT result = RuntimeManager.CoreSystem.createSound(station.Url, mode, out streamSound);
            if (result != RESULT.OK)
            {
                Fail(station, "FMOD createSound returned " + result);
                yield break;
            }

            float started = Time.realtimeSinceStartup;
            uint lastPercent = uint.MaxValue;
            OPENSTATE lastState = OPENSTATE.MAX;
            while (token == generation && Time.realtimeSinceStartup - started < 25f)
            {
                result = streamSound.getOpenState(out OPENSTATE state, out uint percent, out bool starving, out bool diskBusy);
                if (result != RESULT.OK)
                {
                    Fail(station, "FMOD getOpenState returned " + result);
                    yield break;
                }
                if (percent != lastPercent || state != lastState)
                {
                    lastPercent = percent;
                    lastState = state;
                    MelonLogger.Msg("[RadioStreams] " + station.Name + " state=" + state + " buffer=" + percent + "%");
                }
                if (state == OPENSTATE.ERROR)
                {
                    Fail(station, "FMOD reported a stream/network error");
                    yield break;
                }
                // A buffered percentage does not mean FMOD has completed its
                // non-blocking sound creation. playSound during BUFFERING returns
                // ERR_NOTREADY, so only claim the channel once READY is reported.
                if (state == OPENSTATE.READY)
                {
                    result = RuntimeManager.CoreSystem.playSound(streamSound, default(ChannelGroup), true, out streamChannel);
                    if (result != RESULT.OK)
                    {
                        Fail(station, "FMOD playSound returned " + result);
                        yield break;
                    }
                    // Pin the FMOD channel to the radio rather than the player.
                    Vector3 position = owner.transform.position;
                    FMOD.VECTOR fmodPosition = new FMOD.VECTOR { x = position.x, y = position.y, z = position.z };
                    FMOD.VECTOR velocity = default(FMOD.VECTOR);
                    streamChannel.set3DAttributes(ref fmodPosition, ref velocity);
                    streamChannel.set3DMinMaxDistance(1.5f, 18f);
                    streamChannel.setPaused(false);
                    MelonLogger.Msg("[RadioStreams] Playback started: " + station.Name);
                    ModsPanel.ModsUi.ShowToast("Playing: " + station.Name, 4f);
                    yield break;
                }
                yield return null;
            }

            if (token == generation) Fail(station, "connection timed out after 25 seconds");
        }

        /// <summary>Reports a station-level failure without destabilizing the menu.</summary>
        private static void Fail(Station station, string error)
        {
            MelonLogger.Warning("[RadioStreams] " + station.Name + ": " + error);
            ModsPanel.ModsUi.ShowToast("Could not play station: " + error, 6f);
            Stop();
        }

        /// <summary>Lets BOXROOM's original interaction reclaim the radio.</summary>
        internal static void StopForVanillaInteraction() { if (owner != null) Stop(); }
        /// <summary>Stops a channel whose emitting scene object was destroyed.</summary>
        internal static void StopIfOwnedBy(Interactable_Radio radio) { if (owner == radio) Stop(); }

        /// <summary>Releases the FMOD channel and sound exactly once.</summary>
        internal static void Stop()
        {
            generation++;
            if (streamChannel.hasHandle()) { streamChannel.stop(); streamChannel.clearHandle(); }
            if (streamSound.hasHandle()) { streamSound.release(); streamSound.clearHandle(); }
            owner = null;
        }
    }
}
