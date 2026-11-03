using System;
using System.Collections.Generic;
using System.Globalization;
using MelonLoader;
using UnityEngine;

namespace TabletUILib
{
    public class Row
    {
        public string Label;
        public string Detail;
        public Action Click;
    }

    public class Page
    {
        public string Title;
        public string Status;
        public string Footer;
        public Action Back;
        public List<Row> Rows = new List<Row>();

        public Page()
        {
        }

        public Page(string title, string status = null)
        {
            Title = title;
            Status = status;
        }

        public Page Add(string label, string detail, Action click)
        {
            Rows.Add(new Row { Label = label, Detail = detail, Click = click });
            return this;
        }

        public Page Add(string label, Action click)
        {
            return Add(label, null, click);
        }

        public Page Button(string label, Action click)
        {
            return Add(label, null, click);
        }

        public Page Button(string label, string detail, Action click)
        {
            return Add(label, detail, click);
        }

        public Page Text(string label, string detail = null)
        {
            return Add(label, detail, null);
        }

        public Page Link(string label, Func<Page> page)
        {
            return Add(label, null, () => TabletUI.Go(page));
        }

        public Page Link(string label, string detail, Func<Page> page)
        {
            return Add(label, detail, () => TabletUI.Go(page));
        }

        public Page Toggle(string label, Func<bool> get, Action<bool> set)
        {
            return Add(label, get() ? "on" : "off", () => set(!get()));
        }

        public Page Toggle(string label, MelonPreferences_Entry<bool> entry)
        {
            return Toggle(label, () => entry.Value, v => Save(entry, v));
        }

        public Page Choice(string label, string[] options, Func<int> get, Action<int> set)
        {
            if (options == null || options.Length == 0) return Text(label);
            int index = Mathf.Clamp(get(), 0, options.Length - 1);
            return Add(label, options[index], () => set((Mathf.Clamp(get(), 0, options.Length - 1) + 1) % options.Length));
        }

        public Page Choice<T>(string label, Func<T> get, Action<T> set) where T : struct, Enum
        {
            var values = (T[])Enum.GetValues(typeof(T));
            var names = Array.ConvertAll(values, v => v.ToString().ToLowerInvariant());
            return Choice(label, names, () => Array.IndexOf(values, get()), i => set(values[i]));
        }

        public Page Choice<T>(string label, MelonPreferences_Entry<T> entry) where T : struct, Enum
        {
            return Choice<T>(label, () => entry.Value, v => Save(entry, v));
        }

        public Page Slider(string label, Func<float> get, Action<float> set, float min, float max, float step)
        {
            return Add(label, Number(get()), () => TabletUI.Go(() => SliderPage(label, get, set, min, max, step)));
        }

        public Page Slider(string label, MelonPreferences_Entry<float> entry, float min, float max, float step)
        {
            return Slider(label, () => entry.Value, v => Save(entry, v), min, max, step);
        }

        public Page Slider(string label, Func<int> get, Action<int> set, int min, int max, int step = 1)
        {
            Func<float> getFloat = () => get();
            Action<float> setFloat = v => set(Mathf.RoundToInt(v));
            return Slider(label, getFloat, setFloat, (float)min, (float)max, (float)step);
        }

        public Page Slider(string label, MelonPreferences_Entry<int> entry, int min, int max, int step = 1)
        {
            return Slider(label, () => entry.Value, v => Save(entry, v), min, max, step);
        }

        static Page SliderPage(string label, Func<float> get, Action<float> set, float min, float max, float step)
        {
            Action<float> change = v => set(Mathf.Clamp(v, min, max));
            return new Page(label, Number(get()) + "   (" + Number(min) + " to " + Number(max) + ")")
                .Button("up", "+" + Number(step), () => change(get() + step))
                .Button("down", "-" + Number(step), () => change(get() - step))
                .Button("max", Number(max), () => change(max))
                .Button("min", Number(min), () => change(min));
        }

        static string Number(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        static void Save<T>(MelonPreferences_Entry<T> entry, T value)
        {
            entry.Value = value;
            try { MelonPreferences.Save(); }
            catch (Exception e) { MelonLogger.Warning("couldnt save " + entry.Identifier + ": " + e.Message); }
        }
    }
}
