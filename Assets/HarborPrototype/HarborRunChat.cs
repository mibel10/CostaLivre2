using UnityEngine;
using UnityEngine.InputSystem;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    public sealed partial class HarborRun
    {
        private bool chatOpen;
        private string chatDraft = "";
        private TouchScreenKeyboard chatKeyboard;
        private GUIStyle chatStyle;
        private GUIStyle chatInputStyle;

        private bool HandleChatInput()
        {
            if (!session.Online)
            {
                if (chatOpen)
                    CloseChat();
                return false;
            }

            if (chatKeyboard != null && chatKeyboard.status != TouchScreenKeyboard.Status.Visible)
            {
                if (chatKeyboard.status == TouchScreenKeyboard.Status.Done && !string.IsNullOrWhiteSpace(chatKeyboard.text))
                    session.SendChat(chatKeyboard.text);
                chatKeyboard = null;
                CloseChat();
                return true;
            }

            if (!chatOpen)
            {
                if (Keyboard.current?.tKey.wasPressedThisFrame == true)
                {
                    OpenChat();
                    return true;
                }
                return false;
            }

            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            {
                CloseChat();
                return true;
            }
            if (Keyboard.current?.enterKey.wasPressedThisFrame == true
                || Keyboard.current?.numpadEnterKey.wasPressedThisFrame == true)
            {
                if (!string.IsNullOrWhiteSpace(chatDraft))
                    session.SendChat(chatDraft);
                CloseChat();
                return true;
            }
            return true;
        }

        private void OpenChat()
        {
            chatOpen = true;
            chatDraft = "";
            SetCursor(false);
            if (TouchScreenKeyboard.isSupported)
                chatKeyboard = TouchScreenKeyboard.Open("", TouchScreenKeyboardType.Default, false, false, false, false, L("Message...", "Mensagem..."), 110);
        }

        private void CloseChat()
        {
            chatOpen = false;
            chatDraft = "";
            if (!inMenu && overlay == FieldOverlay.None && !deathScreen.BlocksGameplay && Application.isFocused)
                SetCursor(true);
        }

        private void DrawOnlineChat()
        {
            if (session == null || (!session.Online && !session.Migrating))
                return;

            var messages = session.ChatMessages;
            bool showFeed = chatOpen;
            float now = Time.unscaledTime;
            for (int i = 0; !showFeed && i < messages.Count; i++)
                if (now - messages[i].Time < 10f)
                    showFeed = true;

            if (!showFeed && !CostaTouch.Available)
                return;

            if (chatStyle == null)
            {
                chatStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    wordWrap = true,
                    richText = false
                };
                chatInputStyle = new GUIStyle(GUI.skin.textField) { fontSize = 14 };
            }

            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1000f, Screen.height / 580f), 0.9f, 1.5f);
            Matrix4x4 prev = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale;
            float h = Screen.height / scale;

            float x = 18f;
            float boxW = Mathf.Min(360f, w * 0.44f);
            float bottomY = h - (CostaTouch.Available ? 148f : 74f);

            if (showFeed && Event.current.type == EventType.Repaint)
            {
                int drawn = 0;
                for (int i = messages.Count - 1; i >= 0 && drawn < 6; i--)
                {
                    var msg = messages[i];
                    float age = now - msg.Time;
                    if (!chatOpen && age >= 10f)
                        continue;
                    float alpha = chatOpen ? 1f : Mathf.Clamp01(1f - Mathf.InverseLerp(8f, 10f, age));
                    float lineY = bottomY - 34f - drawn * 24f;
                    Color old = GUI.color;
                    GUI.color = new Color(0.03f, 0.05f, 0.08f, 0.68f * alpha);
                    GUI.DrawTexture(new Rect(x, lineY, boxW, 22f), Texture2D.whiteTexture);
                    string line = msg.System ? msg.Text : $"{msg.Sender}: {msg.Text}";
                    chatStyle.normal.textColor = msg.System
                        ? new Color(0.98f, 0.74f, 0.34f, alpha)
                        : new Color(0.96f, 0.96f, 0.94f, alpha);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(x + 8f, lineY + 1f, boxW - 14f, 21f), line, chatStyle);
                    GUI.color = old;
                    drawn++;
                }
            }

            if (chatOpen)
            {
                Rect inputRect = new(x, bottomY - 4f, boxW - 76f, 28f);
                GUI.SetNextControlName("CostaChatInput");
                chatDraft = GUI.TextField(inputRect, chatDraft, 110, chatInputStyle);
                GUI.FocusControl("CostaChatInput");
                if (CostaUi.Button(new Rect(inputRect.xMax + 4f, bottomY - 4f, 72f, 28f), L("Send", "Enviar"), chatInputStyle))
                {
                    if (!string.IsNullOrWhiteSpace(chatDraft))
                        session.SendChat(chatDraft);
                    CloseChat();
                }
            }
            else if (CostaTouch.Available && session.Online)
            {
                if (CostaUi.Button(new Rect(x, bottomY - 4f, 72f, 28f), "Chat", chatInputStyle))
                    OpenChat();
            }

            GUI.matrix = prev;
        }
    }
}
