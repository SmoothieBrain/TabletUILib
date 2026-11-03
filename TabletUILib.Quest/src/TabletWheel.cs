using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Il2Cpp;
using Il2CppAlta.QuickAccessActions;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace TabletUILib
{
    internal class BubbleEntry
    {
        public string Name;
        public string Icon;
        public Texture2D Picture;
        public Func<Page> Home;
        public Action Run;
        public Func<bool> Active;
        public TabletBubble Owner;
        public TabletBubble Sub;
    }

    public class TabletBubble
    {
        internal string Name;
        internal string Icon;
        internal Texture2D Picture;
        internal List<BubbleEntry> Entries = new List<BubbleEntry>();
        internal TabletBubble Parent;

        internal TabletBubble(string name, string icon)
        {
            Name = name;
            Icon = string.IsNullOrEmpty(icon) ? "MOD" : icon;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public TabletBubble Image(string resource, string entry = null)
        {
            if (string.IsNullOrEmpty(resource)) throw new ArgumentNullException(nameof(resource));
            BubbleEntry target = null;
            if (entry != null)
            {
                target = Entries.FirstOrDefault(e => e.Name == entry);
                if (target == null) throw new ArgumentException("no " + entry + " in " + Name + " yet, add it before giving it an image", nameof(entry));
            }

            var tex = Icons.Load(Assembly.GetCallingAssembly(), resource);
            if (tex == null) return this;
            if (target == null) Picture = tex;
            else if (target.Sub != null) target.Sub.Picture = tex;
            else target.Picture = tex;
            return this;
        }

        public TabletBubble Bubble(string name, string icon, Action<TabletBubble> fill)
        {
            if (fill == null) throw new ArgumentNullException(nameof(fill));
            var sub = new TabletBubble(name, icon) { Parent = this };
            fill(sub);
            Entries.Add(new BubbleEntry { Name = name, Icon = sub.Icon, Sub = sub, Owner = this });
            return this;
        }

        public TabletBubble App(string name, string icon, Func<Page> home)
        {
            if (home == null) throw new ArgumentNullException(nameof(home));
            Entries.Add(new BubbleEntry { Name = name, Icon = icon, Home = home, Owner = this });
            return this;
        }

        public TabletBubble Action(string name, string icon, Action run)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            Entries.Add(new BubbleEntry { Name = name, Icon = icon, Run = run, Owner = this });
            return this;
        }

        public TabletBubble Toggle(string name, string icon, Func<bool> get, Action<bool> set)
        {
            if (get == null || set == null) throw new ArgumentNullException(get == null ? nameof(get) : nameof(set));
            Entries.Add(new BubbleEntry { Name = name, Icon = icon, Run = () => set(!get()), Active = get, Owner = this });
            return this;
        }

        public TabletBubble Toggle(string name, string icon, MelonPreferences_Entry<bool> entry)
        {
            return Toggle(name, icon, () => entry.Value, v =>
            {
                entry.Value = v;
                try { MelonPreferences.Save(); }
                catch (Exception e) { MelonLogger.Warning("couldnt save " + entry.Identifier + ": " + e.Message); }
            });
        }
    }

    internal static class BubbleAction
    {
        static readonly Dictionary<IntPtr, BubbleEntry> entries = new Dictionary<IntPtr, BubbleEntry>();

        static bool InGame => Player.Current != null && GameModeManager.CurrentMode?.TryCast<JoinServerGameModeBase>() != null;

        internal static void Add(QuickAccessMenuAction action, BubbleEntry entry)
        {
            entries[action.Pointer] = entry;
        }

        static bool Find(QuickAccessMenuAction action, out BubbleEntry entry)
        {
            entry = null;
            return action != null && entries.TryGetValue(action.Pointer, out entry);
        }

        static bool Valid(BubbleEntry entry)
        {
            return InGame;
        }

        static bool Active(BubbleEntry entry)
        {
            if (entry.Home != null) return TabletUI.IsShowing(entry.Home);
            try { return entry.Active != null && entry.Active(); }
            catch { return false; }
        }

        static void IsValidPostfix(NestingMenuAction __instance, ref bool __result)
        {
            try
            {
                if (Find(__instance, out var entry)) __result = Valid(entry);
            }
            catch (Exception e)
            {
                MelonLogger.Warning("wheel button check broke: " + e.Message);
            }
        }

        static void IsIgnoredPostfix(NestingMenuAction __instance, ref bool __result)
        {
            try
            {
                if (Find(__instance, out var entry)) __result = !Valid(entry);
            }
            catch (Exception e)
            {
                MelonLogger.Warning("wheel button check broke: " + e.Message);
            }
        }

        static void IsActivePostfix(NestingMenuAction __instance, ref bool __result)
        {
            try
            {
                if (Find(__instance, out var entry)) __result = Active(entry);
            }
            catch (Exception e)
            {
                MelonLogger.Warning("wheel button check broke: " + e.Message);
            }
        }

        static bool BubbleRunPrefix(QuickAccessMenuBubble __instance, Controller controller, ref bool __result)
        {
            BubbleEntry entry;
            try
            {
                if (__instance == null || !Find(__instance.content, out entry)) return true;
            }
            catch
            {
                return true;
            }

            __result = false;
            try
            {
                if (!Valid(entry)) return false;
                if (entry.Home != null) TabletUI.Toggle(controller, entry.Home);
                else entry.Run();
                __result = true;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("wheel button broke: " + e.Message);
            }
            return false;
        }

        internal static void Patch(HarmonyLib.Harmony harmony)
        {
            var t = typeof(NestingMenuAction);
            harmony.Patch(AccessTools.PropertyGetter(t, "IsValid"), postfix: new HarmonyMethod(typeof(BubbleAction), nameof(IsValidPostfix)));
            harmony.Patch(AccessTools.PropertyGetter(t, "IsIgnored"), postfix: new HarmonyMethod(typeof(BubbleAction), nameof(IsIgnoredPostfix)));
            harmony.Patch(AccessTools.PropertyGetter(t, "IsActive"), postfix: new HarmonyMethod(typeof(BubbleAction), nameof(IsActivePostfix)));
            harmony.Patch(AccessTools.Method(typeof(QuickAccessMenuBubble), "Run"), prefix: new HarmonyMethod(typeof(BubbleAction), nameof(BubbleRunPrefix)));
        }
    }

    internal static class TabletWheel
    {
        static List<TabletBubble> bubbles = new List<TabletBubble>();
        static TabletBubble apps;
        internal static TabletBubble Wheel = new TabletBubble("wheel", null);
        static Transform holder;
        static HashSet<int> done = new HashSet<int>();
        static HashSet<IntPtr> ours = new HashSet<IntPtr>();

        internal static TabletBubble Apps
        {
            get
            {
                if (apps == null) apps = Add("TabletUILib", "APPS");
                return apps;
            }
        }

        internal static TabletBubble Add(string name, string icon)
        {
            var bubble = new TabletBubble(name, icon);
            bubbles.Add(bubble);
            return bubble;
        }

        static Transform Holder()
        {
            if (holder == null)
            {
                var go = new GameObject("TabletUILib wheel icons");
                go.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(go);
                holder = go.transform;
            }
            return holder;
        }

        static Font font;
        static TextRenderer labelSource;

        static bool GameLabel(Transform parent, string label)
        {
            if (labelSource == null) return false;
            try
            {
                var copy = UnityEngine.Object.Instantiate(labelSource.gameObject, parent, false);
                copy.name = "TabletUILib label";
                copy.SetActive(true);
                copy.transform.localPosition = Vector3.zero;
                copy.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                var text = copy.GetComponent<TextRenderer>();
                text.Text = label;
                text.SetColor(Color.white);
                foreach (var r in copy.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
                return true;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("couldnt copy the wheel title for a label: " + e.Message);
                return false;
            }
        }

        static GameObject MakeIcon(GameObject template, string label, Texture2D image)
        {
            if (template == null) return null;
            var go = UnityEngine.Object.Instantiate(template);
            go.transform.SetParent(Holder(), false);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = false;

            if (image != null && Icons.Add(go.transform, image)) return go;

            var textObj = new GameObject("TabletUILib icon");
            textObj.transform.SetParent(go.transform, false);
            textObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            TextMesh text = null;
            try
            {
                if (font == null) font = Resources.GetBuiltinResource(Il2CppType.Of<Font>(), "Arial.ttf")?.TryCast<Font>();
                text = textObj.AddComponent<TextMesh>();
            }
            catch (Exception e)
            {
                MelonLogger.Warning("no TextMesh on this build: " + e.Message);
            }
            if (text == null)
            {
                UnityEngine.Object.Destroy(textObj);
                if (!GameLabel(go.transform, label))
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
                return go;
            }
            if (font != null)
            {
                text.font = font;
                text.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            text.text = label;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontStyle = FontStyle.Bold;
            text.fontSize = 64;
            text.characterSize = label.Length > 3 ? 0.01f : 0.0125f;
            text.color = Color.white;
            return go;
        }

        static void Setup(QuickAccessMenuAction action, QuickAccessMenuAction template, string name, string label, Texture2D image)
        {
            var prefab = template.prefab;
            action.prefab = MakeIcon(prefab, string.IsNullOrEmpty(label) ? "?" : label, image) ?? prefab;
            action.displayName = name;
        }

        static QuickAccessMenuAction MakeButton(NestingMenuAction nest, QuickAccessMenuAction template, BubbleEntry entry)
        {
            var action = UnityEngine.Object.Instantiate(nest);
            action.nestedActions = new Il2CppReferenceArray<QuickAccessMenuAction>(Array.Empty<QuickAccessMenuAction>());
            action.spawnedBubbles = new Il2CppSystem.Collections.Generic.List<QuickAccessMenuBubble>();
            Setup(action, template, entry.Name, entry.Icon, entry.Picture);
            BubbleAction.Add(action, entry);
            return action;
        }

        static NestingMenuAction MakeBubble(NestingMenuAction nest, QuickAccessMenuAction template, TabletBubble bubble)
        {
            var root = UnityEngine.Object.Instantiate(nest);
            Setup(root, template, bubble.Name, bubble.Icon, bubble.Picture);
            root.nestedActions = new Il2CppReferenceArray<QuickAccessMenuAction>(bubble.Entries.Select(e => MakeEntry(nest, template, e)).ToArray());
            root.spawnedBubbles = new Il2CppSystem.Collections.Generic.List<QuickAccessMenuBubble>();
            ours.Add(root.Pointer);
            return root;
        }

        static QuickAccessMenuAction MakeEntry(NestingMenuAction nest, QuickAccessMenuAction template, BubbleEntry entry)
        {
            if (entry.Sub != null) return MakeBubble(nest, template, entry.Sub);
            return MakeButton(nest, template, entry);
        }

        static void AddBubbles(QuickAccessMenuController controller)
        {
            if (controller == null || (bubbles.Count == 0 && Wheel.Entries.Count == 0)) return;
            int id = controller.GetInstanceID();
            if (done.Contains(id)) return;

            var menus = controller.quickAccessMenus;
            if (menus == null || menus.Length == 0) return;
            done.Add(id);
            if (labelSource == null) labelSource = controller.CenteredTitleDisplay;

            var templates = menus.Where(m => m != null).ToArray();
            var nest = templates.Select(m => m.TryCast<NestingMenuAction>()).FirstOrDefault(n => n != null);
            if (nest == null)
            {
                MelonLogger.Warning("no submenu to copy so there are no mod bubbles");
                return;
            }

            var list = menus.ToList();
            foreach (var bubble in bubbles)
            {
                if (bubble.Entries.Count == 0) continue;
                if (list.Any(m => m != null && m.DisplayName == bubble.Name)) continue;
                list.Add(MakeBubble(nest, templates[0], bubble));
                MelonLogger.Msg("added " + bubble.Name + " bubble to the wheel");
            }
            foreach (var entry in Wheel.Entries)
            {
                if (list.Any(m => m != null && m.DisplayName == entry.Name)) continue;
                list.Add(MakeEntry(nest, templates[0], entry));
                MelonLogger.Msg("added " + entry.Name + " to the wheel");
            }
            controller.quickAccessMenus = new Il2CppReferenceArray<QuickAccessMenuAction>(list.ToArray());
        }

        static void AfterInit(QuickAccessMenuController __instance)
        {
            try
            {
                AddBubbles(__instance);
            }
            catch (Exception e)
            {
                MelonLogger.Warning("couldnt add the mod bubbles: " + e);
            }
        }

        static void AfterSpawn(NestingMenuAction __instance, Vector3 __0)
        {
            if (__instance == null || !ours.Contains(__instance.Pointer)) return;
            var spawned = __instance.spawnedBubbles;
            if (spawned == null) return;
            for (int i = 0; i < spawned.Count; i++)
            {
                var b = spawned[i];
                if (b == null) continue;
                b.transform.position = __0 + (b.transform.position - __0) * 1.65f;
            }
        }

        internal static void Patch(HarmonyLib.Harmony harmony)
        {
            BubbleAction.Patch(harmony);
            harmony.Patch(AccessTools.Method(typeof(QuickAccessMenuController), "InitializeControllerExtension"),
                postfix: new HarmonyMethod(typeof(TabletWheel), nameof(AfterInit)));
            harmony.Patch(AccessTools.Method(typeof(NestingMenuAction), "SpawnNestedBubbles"),
                postfix: new HarmonyMethod(typeof(TabletWheel), nameof(AfterSpawn)));
        }
    }
}
