using System;
using MelonLoader;
using TabletUILib;
using TavernLib.ModdingApi;

[assembly: MelonInfo(typeof(TabletUIMod), "TabletUILib", "1.0.0", "Smoothie")]
[assembly: MelonGame("Alta", "A Township Tale")]

namespace TabletUILib
{
    [TavernMod(ModSide.Client)]
    public class TabletUIMod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            TabletUI.Patch(HarmonyInstance);
            TabletWheel.Patch(HarmonyInstance);
            MelonLogger.Msg("tabletuilib loaded");
        }

        public override void OnUpdate()
        {
            try
            {
                TabletUI.Tick();
            }
            catch (Exception e)
            {
                MelonLogger.Error(e);
            }
        }
    }
}
