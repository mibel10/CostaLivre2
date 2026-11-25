using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CostaLivre
{
    internal sealed class CostaBindings
    {
        private readonly InputAction[] actions;
        private readonly int[] keyboard;
        private int pending = -1;
        internal bool Waiting => pending >= 0;
        internal string Error { get; private set; } = "";

        internal CostaBindings(params InputAction[] actions)
        {
            this.actions = actions;
            keyboard = new int[actions.Length];
            for (int i = 0; i < actions.Length; i++)
            {
                var entries = actions[i].bindings;
                for (int bind = 0; bind < entries.Count; bind++)
                {
                    if (entries[bind].path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase))
                    {
                        keyboard[i] = bind;
                        break;
                    }
                }
            }

            for (int i = 0; i < actions.Length; i++)
            {
                string saved = PlayerPrefs.GetString($"CostaKey{i}", "");
                if (saved.Length > 0)
                    actions[i].ApplyBindingOverride(keyboard[i], saved);
            }
        }

        internal string Name(int index)
        {
            string name = actions[index].GetBindingDisplayString(keyboard[index],
                InputBinding.DisplayStringOptions.DontIncludeInteractions);
            if (!CostaText.Portuguese)
                return name;

            switch (name)
            {
                case "Space": return "Espaço";
                case "Left Shift": return "Shift Esq.";
                case "Right Shift": return "Shift Dir.";
                case "Left Ctrl": return "Ctrl Esq.";
                case "Right Ctrl": return "Ctrl Dir.";
                default: return name;
            }
        }

        internal void Begin(int index)
        {
            pending = index;
            Error = "";
        }

        internal void Cancel()
        {
            pending = -1;
            Error = "";
        }

        internal void Reset()
        {
            pending = -1;
            Error = "";
            for (int i = 0; i < actions.Length; i++)
                actions[i].RemoveBindingOverride(keyboard[i]);
            for (int i = 0; i < actions.Length; i++)
                PlayerPrefs.DeleteKey($"CostaKey{i}");
            PlayerPrefs.Save();
        }

        internal void Tick()
        {
            if (!Waiting || Keyboard.current == null)
                return;

            foreach (var key in Keyboard.current.allKeys)
            {
                if (!key.wasPressedThisFrame)
                    continue;

                if (key.keyCode == Key.Escape)
                {
                    pending = -1;
                    return;
                }

                string path = "<Keyboard>/" + key.name;
                foreach (InputAction action in actions[pending].actionMap.actions)
                {
                    if (action == actions[pending] || action.name == "Previous" || action.name == "Next"
                        || action.name == "Attack")
                    {
                        continue;
                    }

                    foreach (var binding in action.bindings)
                    {
                        if (string.IsNullOrEmpty(binding.effectivePath) || binding.isComposite)
                            continue;
                        if (InputSystem.FindControl(binding.effectivePath) == key
                            || string.Equals(binding.effectivePath, path, StringComparison.OrdinalIgnoreCase))
                        {
                            Error = CostaText.L("That key is already in use.", "Essa tecla já está em uso.");
                            return;
                        }
                    }
                }

                if (key.keyCode == Key.Enter)
                {
                    Error = CostaText.L("That key is reserved.", "Essa tecla é reservada.");
                    return;
                }

                actions[pending].ApplyBindingOverride(keyboard[pending], path);
                PlayerPrefs.SetString($"CostaKey{pending}", path);
                PlayerPrefs.Save();
                pending = -1;
                Error = "";
                return;
            }
        }

    }
}
