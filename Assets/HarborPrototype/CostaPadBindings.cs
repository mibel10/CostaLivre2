using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    internal sealed class CostaPadBindings
    {
        private readonly InputAction[] actions;
        private readonly int[] indices;
        private int pending = -1;
        private int captureFrame;
        internal int Count => actions.Length;
        internal bool Waiting => pending >= 0;
        internal string Error { get; private set; } = "";

        internal CostaPadBindings(params InputAction[] actions)
        {
            this.actions = actions;
            indices = Array.ConvertAll(actions, action =>
            {
                for (int i = 0; i < action.bindings.Count; i++)
                    if (action.bindings[i].path.StartsWith("<Gamepad>/", StringComparison.Ordinal))
                        return i;
                throw new ArgumentException($"{action.name} has no gamepad binding.");
            });
            for (int i = 0; i < Count; i++)
            {
                string path = PlayerPrefs.GetString(Key(i));
                if (path.StartsWith("<Gamepad>/", StringComparison.Ordinal) && path != "<Gamepad>/start")
                    actions[i].ApplyBindingOverride(indices[i], path);
            }
        }

        private string Key(int i) => "CostaPad." + actions[i].name;

        internal string Name(int i) => actions[i].GetBindingDisplayString(indices[i],
            InputBinding.DisplayStringOptions.DontIncludeInteractions);

        internal void Begin(int i)
        {
            pending = i;
            captureFrame = Time.frameCount;
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
            for (int i = 0; i < Count; i++)
            {
                actions[i].RemoveBindingOverride(indices[i]);
                PlayerPrefs.DeleteKey(Key(i));
            }
            PlayerPrefs.Save();
        }

        internal void Tick()
        {
            if (!Waiting || Time.frameCount <= captureFrame)
                return;
            Gamepad pad = Gamepad.current;
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true || pad?.startButton.wasPressedThisFrame == true)
            {
                pending = -1;
                return;
            }
            if (pad == null)
            {
                Error = L("Connect a gamepad.", "Conecte um controle.");
                return;
            }

            foreach (InputControl control in pad.allControls)
            {
                if (control is not ButtonControl button || !button.wasPressedThisFrame)
                    continue;
                string suffix;
                if (button.parent == pad)
                    suffix = button.name;
                else if (button.parent == pad.dpad)
                    suffix = "dpad/" + button.name;
                else
                    continue;
                if (button == pad.startButton)
                {
                    Error = L("Start is reserved for restarting the run.", "Start está reservado para reiniciar a partida.");
                    return;
                }
                string path = "<Gamepad>/" + suffix;
                string previous = actions[pending].bindings[indices[pending]].effectivePath;
                for (int i = 0; i < Count; i++)
                    if (i != pending && actions[i].bindings[indices[i]].effectivePath == path)
                        Apply(i, previous);
                Apply(pending, path);
                PlayerPrefs.Save();
                pending = -1;
                return;
            }
        }

        private void Apply(int i, string path)
        {
            actions[i].ApplyBindingOverride(indices[i], path);
            PlayerPrefs.SetString(Key(i), path);
        }
    }
}
