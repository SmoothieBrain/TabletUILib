using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Il2Cpp;
using Il2CppAlta.Meta.UI;
using Il2CppAlta.QuickAccessActions;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace TabletUILib
{
    public static class TabletUI
    {
        static TabletView view;
        static SocialTablet tablet;
        static Func<Page> home;
        static Func<Page> current;
        static Controller lastController;
        static List<Func<Page>> history = new List<Func<Page>>();
        static float waitUntil = -1f;
        static float nextRefresh;
        static bool summoning;
        static Dictionary<IntPtr, SocialTablet> leaving = new Dictionary<IntPtr, SocialTablet>();

        public static bool IsOpen => view != null || waitUntil > 0f;

        public static void AddApp(string name, string icon, Func<Page> homePage)
        {
            TabletWheel.Apps.App(name, string.IsNullOrEmpty(icon) ? "APP" : icon, homePage);
        }

        public static TabletBubble AddBubble(string name, string icon)
        {
            return TabletWheel.Add(name, icon);
        }

        public static TabletBubble Wheel => TabletWheel.Wheel;

        static bool IsLeaving(SocialTablet t)
        {
            return leaving.ContainsKey(t.Pointer);
        }

        static SocialTablet FindTablet()
        {
            foreach (var key in leaving.Where(p => p.Value == null).Select(p => p.Key).ToList()) leaving.Remove(key);
            foreach (var t in UnityEngine.Object.FindObjectsOfType<SocialTablet>())
            {
                if (t != null && t.IsLocalPlayer && t.gameObject.activeInHierarchy && !IsLeaving(t))
                    return t;
            }
            return null;
        }

        static SummonActionItemQuickAccess SocialTabletAction()
        {
            var manager = ActionItemsManager.Instance;
            if (manager == null) return null;
            var items = manager.actionItems;
            if (items == null) return null;
            foreach (var item in items)
            {
                if (item != null && item.Action != null && item.Action.DisplayName == "Social Tablet")
                    return item.Action;
            }
            return null;
        }

        static bool Summon(Controller controller)
        {
            var summon = SocialTabletAction();
            if (summon == null)
            {
                MelonLogger.Warning("couldnt find the social tablet on the wheel");
                return false;
            }
            summoning = true;
            try { summon.Run(controller); }
            finally { summoning = false; }
            return true;
        }

        static Page MakePage()
        {
            Page page;
            try
            {
                page = current() ?? new Page("EMPTY", "this page returned nothing");
            }
            catch (Exception e)
            {
                MelonLogger.Warning("page broke: " + e);
                page = new Page("ERROR", e.Message);
            }
            if (page.Back == null && history.Count > 0) page.Back = Back;
            return page;
        }

        static void Show(bool keepPage)
        {
            if (view != null && current != null) view.Show(MakePage(), keepPage);
        }

        public static void Go(Func<Page> page)
        {
            if (page == null) return;
            if (current != null) history.Add(current);
            current = page;
            Show(false);
        }

        public static void Back()
        {
            if (history.Count == 0)
            {
                GoHome();
                return;
            }
            current = history[history.Count - 1];
            history.RemoveAt(history.Count - 1);
            Show(false);
        }

        public static void GoHome()
        {
            history.Clear();
            current = home;
            Show(false);
        }

        public static void Refresh()
        {
            Show(true);
        }

        public static bool IsShowing(Func<Page> homePage)
        {
            return IsOpen && home == homePage;
        }

        static void Detach()
        {
            waitUntil = -1f;
            var v = view;
            view = null;
            tablet = null;
            current = null;
            history.Clear();
            if (v != null) v.Dispose();
        }

        public static void Close()
        {
            bool wasShowing = view != null;
            Detach();
            if (wasShowing && lastController != null && FindTablet() != null) Summon(lastController);
        }

        internal static void Toggle(Controller controller, Func<Page> homePage)
        {
            lastController = controller;
            if (IsOpen)
            {
                if (home != homePage)
                {
                    home = homePage;
                    GoHome();
                    return;
                }
                Close();
                return;
            }

            home = homePage;
            if (FindTablet() == null && !Summon(controller)) return;
            waitUntil = Time.unscaledTime + 6f;
        }

        static void Open(SocialTablet t)
        {
            waitUntil = -1f;
            tablet = t;
            view = TabletView.Create(t);
            if (view == null)
            {
                tablet = null;
                MelonLogger.Warning("couldnt open the page on the tablet");
                return;
            }

            try
            {
                view.Home = GoHome;
                view.AfterClick = Refresh;
                var screen = t.GetComponentInChildren<TouchScreenMenuBase>(true);
                if (screen != null && !screen.isSetupLocal) screen.Setup(true);
                GoHome();
                view.Window.SwitchToPanel(view.Panel);
            }
            catch (Exception e)
            {
                Detach();
                MelonLogger.Warning("couldnt open the page on the tablet " + e);
            }
        }

        internal static void Tick()
        {
            if (view != null)
            {
                if (tablet == null || !tablet.gameObject.activeInHierarchy || IsLeaving(tablet))
                {
                    Detach();
                    return;
                }
                if (Time.unscaledTime >= nextRefresh)
                {
                    nextRefresh = Time.unscaledTime + 0.25f;
                    Refresh();
                }
                return;
            }

            if (waitUntil < 0f) return;
            if (Time.unscaledTime > waitUntil)
            {
                waitUntil = -1f;
                MelonLogger.Warning("the social tablet didnt come out");
                return;
            }

            var found = FindTablet();
            if (found != null) Open(found);
        }

        static void AfterFade(SocialTablet __instance, bool isFadingIn)
        {
            if (__instance == null) return;
            if (isFadingIn) leaving.Remove(__instance.Pointer);
            else leaving[__instance.Pointer] = __instance;
        }

        static bool KeepOurPanel(Window window, ref Panel panel, bool finishing)
        {
            if (view == null || view.Window != window || view.Panel == null) return true;
            if (panel == view.Panel) return true;
            panel = view.Panel;
            return finishing || window.CurrentPanel != panel;
        }

        static bool BeforeSwitch(Window __instance, ref Panel panel)
        {
            return KeepOurPanel(__instance, ref panel, false);
        }

        static bool BeforeFinish(Window __instance, ref Panel panel)
        {
            return KeepOurPanel(__instance, ref panel, true);
        }

        static void BeforeSummon(SummonActionItemQuickAccess __instance)
        {
            if (!summoning && view != null && __instance != null && __instance.DisplayName == "Social Tablet")
                Detach();
        }

        internal static void Patch(HarmonyLib.Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(Window), "SwitchToPanel", new[] { typeof(Panel) }),
                prefix: new HarmonyMethod(typeof(TabletUI), nameof(BeforeSwitch)));
            harmony.Patch(AccessTools.Method(typeof(Window), "FinishSwitch"),
                prefix: new HarmonyMethod(typeof(TabletUI), nameof(BeforeFinish)));
            harmony.Patch(AccessTools.Method(typeof(SummonActionItemQuickAccess), "Run"),
                prefix: new HarmonyMethod(typeof(TabletUI), nameof(BeforeSummon)));
            harmony.Patch(AccessTools.Method(typeof(SocialTablet), "StartFade"),
                postfix: new HarmonyMethod(typeof(TabletUI), nameof(AfterFade)));
        }
    }
}
