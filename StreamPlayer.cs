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
            if (station == null || !StationStore.TryNormalizeHttpUrl(station.Url, out string streamUrl))
            {
                MelonLogger.Warning("[RadioStreams] Rejected an invalid stream URL.");
                ModsPanel.ModsUi.ShowToast("Could not play station: invalid stream address", 6f);
                return;
            }
            station.Url = streamUrl;

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
            RESULT result = RuntimeManager.CoreSystem.createSound(station.Url, mode, out FMOD.Sound pendingSound);
            if (result != RESULT.OK)
            {
                FailPending(station, pendingSound, token, "FMOD createSound returned " + result);
                yield break;
            }

            float started = Time.realtimeSinceStartup;
            uint lastPercent = uint.MaxValue;
            OPENSTATE lastState = OPENSTATE.MAX;
            while (token == generation && Time.realtimeSinceStartup - started < 25f)
            {
                result = pendingSound.getOpenState(out OPENSTATE state, out uint percent, out bool starving, out bool diskBusy);
                if (result != RESULT.OK)
                {
                    FailPending(station, pendingSound, token, "FMOD getOpenState returned " + result);
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
                    FailPending(station, pendingSound, token, "FMOD reported a stream/network error");
                    yield break;
                }
                // A buffered percentage does not mean FMOD has completed its
                // non-blocking sound creation. playSound during BUFFERING returns
                // ERR_NOTREADY, so only claim the channel once READY is reported.
                if (state == OPENSTATE.READY)
                {
                    if (token != generation)
                    {
                        ReleaseWhenSafe(pendingSound);
                        yield break;
                    }

                    streamSound = pendingSound;
                    result = RuntimeManager.CoreSystem.playSound(streamSound, default(ChannelGroup), true, out streamChannel);
                    if (result != RESULT.OK)
                    {
                        streamSound.clearHandle();
                        FailPending(station, pendingSound, token, "FMOD playSound returned " + result);
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

            if (token == generation) FailPending(station, pendingSound, token, "connection timed out after 25 seconds");
            else ReleaseWhenSafe(pendingSound);
        }

        /// <summary>Reports failure and lets FMOD finish opening before cleanup.</summary>
        private static void FailPending(Station station, FMOD.Sound pendingSound, int token, string error)
        {
            MelonLogger.Warning("[RadioStreams] " + station.Name + ": " + error);
            ModsPanel.ModsUi.ShowToast("Could not play station: " + error, 6f);
            if (token == generation)
            {
                generation++;
                owner = null;
            }
            ReleaseWhenSafe(pendingSound);
        }

        /// <summary>
        /// FMOD forbids releasing a NONBLOCKING sound while it is still connecting.
        /// Wait for a terminal open state instead of risking a native use-after-free.
        /// </summary>
        private static void ReleaseWhenSafe(FMOD.Sound sound)
        {
            if (sound.hasHandle()) MelonCoroutines.Start(WaitAndRelease(sound));
        }

        private static IEnumerator WaitAndRelease(FMOD.Sound sound)
        {
            while (sound.hasHandle())
            {
                RESULT result = sound.getOpenState(out OPENSTATE state, out _, out _, out _);
                if (result == RESULT.ERR_INVALID_HANDLE)
                {
                    sound.clearHandle();
                    yield break;
                }
                if (state == OPENSTATE.READY || state == OPENSTATE.ERROR)
                {
                    RESULT releaseResult = sound.release();
                    if (releaseResult != RESULT.OK && releaseResult != RESULT.ERR_INVALID_HANDLE)
                        MelonLogger.Warning("[RadioStreams] Deferred FMOD release returned " + releaseResult);
                    sound.clearHandle();
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.1f);
            }
        }

        /// <summary>Lets BOXROOM's original interaction reclaim the radio.</summary>
        internal static void StopForVanillaInteraction() { if (owner != null) Stop(); }
        /// <summary>Stops a channel whose emitting scene object was destroyed.</summary>
        internal static void StopIfOwnedBy(Interactable_Radio radio) { if (owner == radio) Stop(); }

        /// <summary>Stops playback and schedules the FMOD sound for safe release.</summary>
        internal static void Stop()
        {
            generation++;
            if (streamChannel.hasHandle()) { streamChannel.stop(); streamChannel.clearHandle(); }
            if (streamSound.hasHandle())
            {
                FMOD.Sound soundToRelease = streamSound;
                streamSound.clearHandle();
                ReleaseWhenSafe(soundToRelease);
            }
            owner = null;
        }
    }
}
