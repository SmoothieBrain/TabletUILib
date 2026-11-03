using System;
using MelonLoader;
using TabletUILib;

[assembly: MelonInfo(typeof(TabletUIMod), "TabletUILib", "1.0.0-quest", "Smoothie")]
[assembly: MelonGame("Alta", "Att_Quest")]

namespace TabletUILib
{
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
