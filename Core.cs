using MelonLoader;

[assembly: MelonInfo(typeof(BoxroomRadioStreams.Core), "BOXROOM Radio Streams", "1.2.0", "MidgetBrony")]
[assembly: MelonGame(null, "BOXROOM")]

namespace BoxroomRadioStreams
{
    /// <summary>
    /// MelonLoader entry point for the standalone mod build. Native BOXROOM
    /// integration can initialize the station store from its own startup flow.
    /// </summary>
    public sealed class Core : MelonMod
    {
        /// <summary>Creates the user-data folder and loads saved favorites.</summary>
        public override void OnInitializeMelon()
        {
            StationStore.Initialize();
            LoggerInstance.Msg("Radio Streams initialized. Shift-click a BOXROOM radio to choose a station.");
        }
    }
}
