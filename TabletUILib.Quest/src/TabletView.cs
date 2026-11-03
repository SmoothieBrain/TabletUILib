using System;
using System.Collections.Generic;
using System.Reflection;
using Il2Cpp;
using Il2CppAlta.Meta.UI;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;

namespace TabletUILib
{
    internal class TabletView
    {
        const int RowCount = 4;

        static Color headingColor = new Color(0.76f, 0.96f, 1f);
        static Color textColor = new Color(0.87f, 0.94f, 1f);

        static Dictionary<string, Func<Component, Il2CppObjectBase>> keepTypes;

        static Dictionary<string, Func<Component, Il2CppObjectBase>> KeepTypes()
        {
            if (keepTypes != null) return keepTypes;
            keepTypes = new Dictionary<string, Func<Component, Il2CppObjectBase>>
            {
                [Il2CppType.Of<Button>().FullName] = c => c.Cast<Button>(),
                [Il2CppType.Of<TouchScreenButton>().FullName] = c => c.Cast<TouchScreenButton>(),
                [Il2CppType.Of<ButtonWithEvents>().FullName] = c => c.Cast<ButtonWithEvents>(),
                [Il2CppType.Of<TextButton>().FullName] = c => c.Cast<TextButton>(),
                [Il2CppType.Of<TouchScreenButtonVisual>().FullName] = c => c.Cast<TouchScreenButtonVisual>(),
                [Il2CppType.Of<TextRenderer>().FullName] = c => c.Cast<TextRenderer>(),
            };
            return keepTypes;
        }

        class Swap
        {
            public Component Target;
            public object Wrapper;
            public PropertyInfo Field;
            public object Original;
        }

        public Window Window;
        public Panel Panel;
        public Action Home;
        public Action AfterClick;

        TouchScreenMenuBase screen;
        Panel gamePanel;
        GameObject root;
        TextRenderer title;
        TextRenderer status;
        TextRenderer footer;
        Button[] rowButtons = new Button[RowCount];
        TextRenderer[] rowLabels = new TextRenderer[RowCount];
        TextRenderer[] rowDetails = new TextRenderer[RowCount];
        Button back;
        Button prev;
        Button next;
        List<Swap> swaps = new List<Swap>();
        bool closed;

        Page page;
        int pageIndex;

        static Transform Find(Transform parent, string path)
        {
            var t = parent.Find(path);
            if (t == null) throw new Exception("tablet is missing " + path);
            return t;
        }

        static ITouchScreenButton Touch(Component c)
        {
            return c.GetComponent<Button>()?.TryCast<ITouchScreenButton>();
        }

        static bool IsTouch(Transform t)
        {
            return Touch(t) != null;
        }

        static void FitText(TextRenderer text, float worldWidth, int lines)
        {
            text.supportsHTMLTags = false;
            text.isMultiLine = lines > 1;
            float s = Mathf.Abs(text.transform.lossyScale.x);
            if (worldWidth <= 0f || s <= 0f) return;

            float width = worldWidth / s;
            text.maxWidth = width;
            text.bestFitToWidth = lines == 1;
            text.hasLineLimit = true;
            text.maxLines = lines;

            float scale = text.scale;
            if (scale > 0f)
            {
                text.lineWidth = width / scale;
                text.maxScale = scale;
                text.minScale = scale * 0.65f;
            }
        }

        static bool IsField(Type t, PropertyInfo p)
        {
            return p.CanWrite && t.GetField("NativeFieldInfoPtr_" + p.Name, BindingFlags.Static | BindingFlags.NonPublic) != null;
        }

        static void ClearEvents(Il2CppObjectBase c)
        {
            for (var t = c.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
            {
                foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                {
                    if (!IsField(t, p)) continue;
                    var type = p.PropertyType;
                    try
                    {
                        if (typeof(Il2CppSystem.Delegate).IsAssignableFrom(type))
                            p.SetValue(c, null);
                        else if ((typeof(UnityEventBase).IsAssignableFrom(type) || type == typeof(AltaEvent)) && !type.IsAbstract)
                            p.SetValue(c, Activator.CreateInstance(type));
                        else if (p.Name == "_State_k__BackingField")
                            p.SetValue(c, false);
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Warning("couldnt clear " + t.Name + "." + p.Name + ": " + e.Message);
                    }
                }
            }
        }

        static GameObject CopyObject(Transform source, Transform parent, string name)
        {
            var go = UnityEngine.Object.Instantiate(source.gameObject, parent, false);
            go.name = "TabletUILib " + name;
            go.SetActive(false);
            go.transform.localRotation = Quaternion.Inverse(parent.rotation) * source.rotation;
            Vector3 a = source.lossyScale;
            Vector3 b = parent.lossyScale;
            go.transform.localScale = new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);

            var keep = KeepTypes();
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                if (keep.TryGetValue(mb.GetIl2CppType().FullName, out var cast)) ClearEvents(cast(mb));
                else UnityEngine.Object.DestroyImmediate(mb);
            }
            go.SetActive(true);
            return go;
        }

        static TextRenderer CopyText(Transform source, Transform parent, string name, Color color, float scale)
        {
            var go = CopyObject(source, parent, name);
            var text = go.GetComponent<TextRenderer>();
            go.transform.localScale *= scale;
            FitText(text, 0f, 1);
            text.SetColor(color);
            return text;
        }

        static string Escape(string text)
        {
            return (text ?? "").Replace("\\", "\\\\");
        }

        static void SetText(TextRenderer text, string value)
        {
            text.Text = Escape(value);
        }

        static void SetVisible(Button button, bool on)
        {
            button.IsEnabled = on;
            button.gameObject.SetActive(on);
        }

        Button CopyButton(Transform source, string name, Vector3 pos)
        {
            var go = CopyObject(source, root.transform, name);
            go.transform.localPosition = pos;
            var button = go.GetComponent<Button>();
            if (button == null || Touch(button) == null) throw new Exception(source.name + " isnt a touch button");
            button.IsEnabled = true;
            return button;
        }

        void Run(Action action)
        {
            if (closed || action == null) return;
            try { action(); }
            catch (Exception e) { MelonLogger.Warning("tablet button broke: " + e); }
        }

        void Wire(Button button, Action click)
        {
            Action<Button> released = b =>
            {
                if (!closed && button.IsEnabled) click();
            };
            button.add_Released(released);
        }

        void Remap()
        {
            if (screen == null) return;
            screen.InstantiatedButtons.Clear();
            screen.MapButtons();
        }

        int PageCount()
        {
            if (page == null) return 1;
            return Mathf.Max(1, (page.Rows.Count + RowCount - 1) / RowCount);
        }

        void Draw()
        {
            if (page == null) return;
            int count = PageCount();
            pageIndex = Mathf.Clamp(pageIndex, 0, count - 1);

            SetText(title, page.Title);
            SetText(status, page.Status);
            string foot = page.Footer ?? "";
            if (count > 1) foot = (pageIndex + 1) + " / " + count + (foot != "" ? "   " + foot : "");
            SetText(footer, foot);

            for (int i = 0; i < RowCount; i++)
            {
                int r = pageIndex * RowCount + i;
                Row row = r < page.Rows.Count ? page.Rows[r] : null;
                rowButtons[i].gameObject.SetActive(row != null);
                rowButtons[i].IsEnabled = row != null && row.Click != null;
                rowButtons[i].Label = Escape(row != null ? row.Label : null);
                SetText(rowLabels[i], row != null ? row.Label : null);
                SetText(rowDetails[i], row != null ? row.Detail : null);
                rowDetails[i].gameObject.SetActive(row != null && !string.IsNullOrEmpty(row.Detail));
            }

            SetVisible(back, page.Back != null);
            SetVisible(prev, count > 1);
            SetVisible(next, count > 1);
            Remap();
        }

        void ClickRow(int index)
        {
            int r = pageIndex * RowCount + index;
            if (page == null || r >= page.Rows.Count) return;
            Run(page.Rows[r].Click);
            Run(AfterClick);
        }

        void TurnPage(int dir)
        {
            int count = PageCount();
            pageIndex = (pageIndex + dir + count) % count;
            Draw();
        }

        public void Show(Page p, bool keepPage)
        {
            if (closed) return;
            if (!keepPage || p == null || page == null || p.Title != page.Title) pageIndex = 0;
            page = p;
            Draw();
        }

        void Replace(Component target, object wrapper, string field, object value)
        {
            var prop = wrapper.GetType().GetProperty(field, BindingFlags.Instance | BindingFlags.Public);
            if (prop == null) return;
            swaps.Add(new Swap { Target = target, Wrapper = wrapper, Field = prop, Original = prop.GetValue(wrapper) });
            prop.SetValue(wrapper, value);
        }

        void GoHome()
        {
            Run(Home);
        }

        void TakeOverHome(Transform buttons)
        {
            int found = 0;

            foreach (string name in new[] { "Home Button", "Extended Home Button" })
            {
                var home = buttons.Find(name);
                if (home == null) continue;

                foreach (var b in home.GetComponentsInChildren<BasicTouchScreenButton>(true))
                {
                    var ev = new AltaEvent();
                    ev.add_Invoked(new Action(GoHome));
                    Replace(b, b, "onPressed", new AltaEvent());
                    Replace(b, b, "onReleased", ev);
                    found++;
                }
                foreach (var b in home.GetComponentsInChildren<Button>(true))
                {
                    Replace(b, b, "Pressed", null);
                    Replace(b, b, "Released", (Il2CppSystem.Action<Button>)new Action<Button>(x => GoHome()));
                    var withEvents = b.TryCast<ButtonWithEvents>();
                    if (withEvents != null)
                    {
                        Replace(b, withEvents, "onPress", new UnityEvent());
                        Replace(b, withEvents, "onRelease", new UnityEvent());
                    }
                    found++;
                }
            }
            if (found == 0) MelonLogger.Warning("no home button on the tablet");
        }

        void Build(SocialTablet tablet)
        {
            Window = tablet.GetComponent<Window>();
            screen = tablet.GetComponentInChildren<TouchScreenMenuBase>(true);
            if (Window == null || screen == null) throw new Exception("no window or touch screen on the tablet");
            gamePanel = Window.CurrentPanel;

            var pages = Find(screen.transform, "Components/Pages");
            var buttons = Find(screen.transform, "Components/Buttons");
            var actions = Find(pages, "Player Actions Page");
            var recent = Find(pages, "Recent Players Page");
            var rowSource = Find(actions, "Add Friend Button");
            var backSource = Find(actions, "Back Page Button");
            var titleSource = Find(recent, "Title");
            var prevSource = Find(recent, "Back Button");
            var nextSource = Find(recent, "Forward Button");
            var textSource = rowSource.GetComponentInChildren<TextRenderer>(true);
            var rowTouch = Touch(rowSource);
            if (textSource == null || rowTouch == null) throw new Exception("tablet row is missing its text or touch");

            float rowHeight = rowTouch.ButtonSize.y * Mathf.Abs(screen.transform.lossyScale.y) / Mathf.Abs(actions.lossyScale.y);

            root = new GameObject("TabletUILib page");
            root.layer = actions.gameObject.layer;
            root.SetActive(false);
            root.transform.SetParent(pages, false);
            root.transform.localPosition = actions.localPosition;
            root.transform.localRotation = actions.localRotation;
            root.transform.localScale = actions.localScale;
            Panel = root.AddComponent<Panel>();
            Panel.window = Window;

            float titleY = actions.InverseTransformPoint(titleSource.position).y;
            var prevPos = actions.InverseTransformPoint(prevSource.position);
            var nextPos = actions.InverseTransformPoint(nextSource.position);
            float pagerY = Mathf.Max(prevPos.y, nextPos.y);
            float top = titleY - rowHeight * 1.5f;
            float bottom = pagerY + rowHeight * 1.5f;
            float gap = (top - bottom) / (RowCount - 1);
            if (gap < rowHeight * 1.05f) throw new Exception("not enough room on the tablet for the rows");

            title = CopyText(titleSource, root.transform, "title", headingColor, 1f);
            title.transform.localPosition = actions.InverseTransformPoint(titleSource.position);
            var textPos = actions.InverseTransformPoint(textSource.transform.position);
            status = CopyText(textSource.transform, root.transform, "status", textColor, 0.65f);
            status.transform.localPosition = new Vector3(textPos.x, titleY - (titleY - top) * 0.6f, textPos.z);

            for (int i = 0; i < RowCount; i++)
            {
                int index = i;
                var button = CopyButton(rowSource, "row " + (i + 1), new Vector3(rowSource.localPosition.x, top - gap * i, rowSource.localPosition.z));
                var label = button.GetComponentInChildren<TextRenderer>(true);
                label.SetColor(textColor);

                var labelPos = button.transform.InverseTransformPoint(label.transform.position);
                var up = button.transform.InverseTransformVector(root.transform.TransformVector(new Vector3(0f, gap, 0f)));
                label.transform.position = button.transform.TransformPoint(labelPos + up * 0.13f);

                var detail = CopyText(textSource.transform, button.transform, "detail", textColor, 0.65f);
                detail.transform.localPosition = labelPos - up * 0.18f;

                rowButtons[i] = button;
                rowLabels[i] = label;
                rowDetails[i] = detail;
                Wire(button, () => ClickRow(index));
            }

            prev = CopyButton(IsTouch(prevSource) ? prevSource : backSource, "previous", prevPos);
            next = CopyButton(IsTouch(nextSource) ? nextSource : backSource, "next", nextPos);
            back = CopyButton(backSource, "back", new Vector3(prevPos.x, titleY, prevPos.z));
            Wire(prev, () => TurnPage(-1));
            Wire(next, () => TurnPage(1));
            Wire(back, () => Run(page != null ? page.Back : null));

            footer = CopyText(textSource.transform, root.transform, "footer", textColor, 0.65f);
            footer.transform.localPosition = new Vector3(textPos.x, (bottom + prevPos.y) / 2f, textPos.z);

            float width = rowTouch.ButtonSize.x * Mathf.Abs(screen.transform.lossyScale.x) * 0.95f;
            FitText(title, width * 0.8f, 1);
            FitText(status, width, 2);
            FitText(footer, width, 2);
            for (int i = 0; i < RowCount; i++)
            {
                FitText(rowLabels[i], width, 1);
                FitText(rowDetails[i], width, 1);
            }

            TakeOverHome(buttons);
            Remap();
        }

        public void Dispose()
        {
            if (closed) return;
            closed = true;

            for (int i = swaps.Count - 1; i >= 0; i--)
            {
                var s = swaps[i];
                if (s.Target == null) continue;
                s.Field.SetValue(s.Wrapper, s.Original);
            }
            swaps.Clear();

            var window = Window;
            var panel = Panel;
            Window = null;
            Panel = null;
            try
            {
                if (window != null && panel != null && window.CurrentPanel == panel)
                {
                    if (gamePanel != null) window.SwitchToPanel(gamePanel);
                    else window._CurrentPanel_k__BackingField = null;
                }
            }
            catch (Exception e)
            {
                MelonLogger.Warning("couldnt put the tablet back: " + e.Message);
            }

            if (root != null)
            {
                root.SetActive(false);
                root.transform.SetParent(null, false);
                UnityEngine.Object.Destroy(root);
                root = null;
            }
            Remap();
            screen = null;
        }

        public static TabletView Create(SocialTablet tablet)
        {
            var view = new TabletView();
            try
            {
                view.Build(tablet);
                return view;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("tablet page failed: " + e.Message);
                view.Dispose();
                return null;
            }
        }
    }
}
