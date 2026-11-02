using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Alta.QuickAccessActions;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace TabletUILib
{
    internal class BubbleEntry
    {
        public string Name;
        public string Icon;
        public Func<Page> Home;
        public Action Run;
        public Func<bool> Active;
        public TabletBubble Owner;
        public Action<Player> Server;
        public string Role;

        public string Id => Owner.Name + "/" + Name;
    }

    public class TabletBubble
    {
        internal string Name;
        internal string Icon;
        internal List<BubbleEntry> Entries = new List<BubbleEntry>();
        internal string Needs;

        internal TabletBubble(string name, string icon)
        {
            Name = name;
            Icon = string.IsNullOrEmpty(icon) ? "MOD" : icon;
        }

        public TabletBubble NeedsServer(string mod)
        {
            if (string.IsNullOrEmpty(mod)) throw new ArgumentNullException(nameof(mod));
            Needs = mod;
            return this;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public TabletBubble NeedsServer()
        {
            var asm = Assembly.GetCallingAssembly();
            var melon = MelonAssembly.LoadedAssemblies.FirstOrDefault(a => a.Assembly == asm)?.LoadedMelons.FirstOrDefault();
            if (melon == null) throw new InvalidOperationException("couldnt tell which mod this is, pass the mod name to NeedsServer");
            return NeedsServer(melon.Info.Name);
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

        public TabletBubble ServerAction(string name, string icon, Action<Player> run, string role = null)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            Entries.Add(new BubbleEntry { Name = name, Icon = icon, Server = run, Role = role, Owner = this });
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

    internal class BubbleAction : QuickAccessMenuAction
    {
        public BubbleEntry Entry;

        static bool InGame => Player.Current != null && GameModeManager.CurrentMode is JoinServerGameModeBase;

        public override bool IsValid
        {
            get
            {
                if (!InGame || Entry == null) return false;
                if (Entry.Owner.Needs != null && !ServerMods.Has(Entry.Owner.Needs)) return false;
                return Entry.Server == null || ServerMods.HasAction(Entry.Id);
            }
        }

        public override bool IsIgnored => !IsValid || base.IsIgnored;

        public override bool IsActive
        {
            get
            {
                if (Entry == null) return false;
                if (Entry.Home != null) return TabletUI.IsShowing(Entry.Home);
                try { return Entry.Active != null && Entry.Active(); }
                catch { return false; }
            }
        }

        public override void Run(Controller controller)
        {
            try
            {
                if (Entry.Home != null) TabletUI.Toggle(controller, Entry.Home);
                else if (Entry.Server != null) ServerMods.Run(Entry.Id);
                else Entry.Run();
            }
            catch (Exception e)
            {
                MelonLogger.Warning("wheel button broke: " + e.Message);
            }
        }
    }

    internal static class TabletWheel
    {
        static FieldInfo menusField = AccessTools.Field(typeof(QuickAccessMenuController), "quickAccessMenus");
        static FieldInfo nestedField = AccessTools.Field(typeof(NestingMenuAction), "nestedActions");
        static FieldInfo bubblesField = AccessTools.Field(typeof(NestingMenuAction), "spawnedBubbles");
        static FieldInfo prefabField = AccessTools.Field(typeof(QuickAccessMenuAction), "prefab");
        static FieldInfo nameField = AccessTools.Field(typeof(QuickAccessMenuAction), "displayName");

        static List<TabletBubble> bubbles = new List<TabletBubble>();
        static TabletBubble apps;
        static Transform holder;
        static HashSet<int> done = new HashSet<int>();
        static HashSet<NestingMenuAction> ours = new HashSet<NestingMenuAction>();

        internal static TabletBubble Apps
        {
            get
            {
                if (apps == null) apps = Add("TabletUILib", "APPS");
                return apps;
            }
        }

        internal static IEnumerable<BubbleEntry> ServerEntries => bubbles.SelectMany(b => b.Entries).Where(e => e.Server != null);

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

        static UnityEngine.Object TextIcon(UnityEngine.Object template, string label)
        {
            if (template == null) return null;
            var copy = UnityEngine.Object.Instantiate(template);
            GameObject go = copy as GameObject;
            if (go == null && copy is Component comp) go = comp.gameObject;
            if (go == null) return null;

            go.transform.SetParent(Holder(), false);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = false;

            var textObj = new GameObject("TabletUILib icon");
            textObj.transform.SetParent(go.transform, false);
            textObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var text = textObj.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            text.text = label;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontStyle = FontStyle.Bold;
            text.fontSize = 64;
            text.characterSize = label.Length > 3 ? 0.01f : 0.0125f;
            text.color = Color.white;
            return copy;
        }

        static void Setup(QuickAccessMenuAction action, QuickAccessMenuAction template, string name, string label)
        {
            var prefab = (UnityEngine.Object)prefabField.GetValue(template);
            prefabField.SetValue(action, TextIcon(prefab, string.IsNullOrEmpty(label) ? "?" : label) ?? prefab);
            nameField.SetValue(action, name);
        }

        static BubbleAction MakeButton(QuickAccessMenuAction template, BubbleEntry entry)
        {
            var action = ScriptableObject.CreateInstance<BubbleAction>();
            action.Entry = entry;
            Setup(action, template, entry.Name, entry.Icon);
            return action;
        }

        static NestingMenuAction MakeBubble(NestingMenuAction nest, QuickAccessMenuAction template, TabletBubble bubble)
        {
            var root = UnityEngine.Object.Instantiate(nest);
            Setup(root, template, bubble.Name, bubble.Icon);
            nestedField.SetValue(root, bubble.Entries.Select(e => (QuickAccessMenuAction)MakeButton(template, e)).ToArray());
            bubblesField.SetValue(root, new List<QuickAccessMenuBubble>());
            ours.Add(root);
            return root;
        }

        static void AddBubbles(QuickAccessMenuController controller)
        {
            if (controller == null || bubbles.Count == 0) return;
            int id = controller.GetInstanceID();
            if (done.Contains(id)) return;

            var menus = (QuickAccessMenuAction[])menusField.GetValue(controller);
            if (menus == null || menus.Length == 0) return;
            done.Add(id);

            var templates = menus.Where(m => m != null).ToArray();
            var nest = templates.OfType<NestingMenuAction>().FirstOrDefault();
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
            menusField.SetValue(controller, list.ToArray());
        }

        static void AfterInit(QuickAccessMenuController __instance)
        {
            try
            {
                AddBubbles(__instance);
            }
            catch (Exception e)
            {
                MelonLogger.Warning("couldnt add the mod bubbles: " + e.Message);
            }
        }

        static void AfterSpawn(NestingMenuAction __instance, Vector3 __0)
        {
            if (!ours.Contains(__instance)) return;
            var spawned = bubblesField.GetValue(__instance) as List<QuickAccessMenuBubble>;
            if (spawned == null) return;
            foreach (var b in spawned)
            {
                if (b == null) continue;
                b.transform.position = __0 + (b.transform.position - __0) * 1.65f;
            }
        }

        internal static void Patch(HarmonyLib.Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(QuickAccessMenuController), "InitializeControllerExtension"),
                postfix: new HarmonyMethod(typeof(TabletWheel), nameof(AfterInit)));
            harmony.Patch(AccessTools.Method(typeof(NestingMenuAction), "SpawnNestedBubbles"),
                postfix: new HarmonyMethod(typeof(TabletWheel), nameof(AfterSpawn)));
        }
    }
}
