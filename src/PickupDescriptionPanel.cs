using System;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using RoR2;
using UnityEngine;

namespace PickupDescriptionPanel
{
    [BepInPlugin("com.alex.pickupdescriptionpanel", "Pickup Description Panel", "1.0.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private ConfigEntry<bool> panelEnabled;
        private ConfigEntry<KeyCode> toggleKey;
        private ConfigEntry<float> x, y, width, scale;
        private ConfigEntry<int> fontSize;
        private string title, description;
        private Color titleColor;
        private GUIStyle titleStyle, bodyStyle;
        private Vector2 scroll;
        private PickupIndex previous = PickupIndex.none;
        private string previousLanguage;
        private float nextErrorLog;
        private static readonly Regex UnsupportedTags = new Regex(@"</?(?!b\b|i\b|color\b|size\b)[a-zA-Z][^>]*>", RegexOptions.Compiled);

        private void Awake()
        {
            panelEnabled = Config.Bind("General", "Enabled", true, "Show descriptions for the targeted pickup or shop terminal.");
            toggleKey = Config.Bind("General", "ToggleKey", KeyCode.F8, "Toggle the panel during gameplay.");
            x = Config.Bind("Panel", "X", 0.72f, new ConfigDescription("Horizontal position (fraction of screen).", new AcceptableValueRange<float>(0f, 1f)));
            y = Config.Bind("Panel", "Y", 0.32f, new ConfigDescription("Vertical position (fraction of screen).", new AcceptableValueRange<float>(0f, 1f)));
            width = Config.Bind("Panel", "Width", 400f, new ConfigDescription("Panel width at 1080p.", new AcceptableValueRange<float>(240f, 800f)));
            scale = Config.Bind("Panel", "Scale", 1f, new ConfigDescription("Additional UI scale.", new AcceptableValueRange<float>(0.5f, 2f)));
            fontSize = Config.Bind("Panel", "FontSize", 19, new ConfigDescription("Description font size.", new AcceptableValueRange<int>(12, 36)));
            Logger.LogInfo("Pickup Description Panel loaded. Toggle: " + toggleKey.Value);
        }

        private void Update()
        {
            title = null;
            if (Input.GetKeyDown(toggleKey.Value)) panelEnabled.Value = !panelEnabled.Value;
            if (!panelEnabled.Value || !Run.instance) return;
            try
            {
                LocalUser user = LocalUserManager.GetFirstLocalUser();
                if (user == null || user.isUIFocused || !user.cachedBody) return;
                InteractionDriver driver = user.cachedBody.GetComponent<InteractionDriver>();
                GameObject target = driver ? driver.FindBestInteractableObject() : null;
                if (!target) { previous = PickupIndex.none; return; }

                PickupIndex index = PickupIndex.none;
                ShopTerminalBehavior shop = target.GetComponent<ShopTerminalBehavior>();
                if (shop)
                {
                    // Never reveal mystery stock, or retain the description of sold stock.
                    if (shop.pickupIndexIsHidden || shop.NetworkhasBeenPurchased) return;
                    index = shop.CurrentPickup().pickupIndex;
                }
                else
                {
                    GenericPickupController pickup = target.GetComponent<GenericPickupController>();
                    // The public compatibility accessor is still available in the current
                    // game DLL; its suggested replacement is not public on this build.
#pragma warning disable CS0618
                    if (pickup) index = pickup.pickupIndex;
#pragma warning restore CS0618
                }
                PickupDef def = PickupCatalog.GetPickupDef(index);
                if (def == null) return;
                ItemDef item = ItemCatalog.GetItemDef(def.itemIndex);
                EquipmentDef equipment = EquipmentCatalog.GetEquipmentDef(def.equipmentIndex);
                if (!item && !equipment) return;
                string token = item ? item.descriptionToken : equipment.descriptionToken;
                string fallback = item ? item.pickupToken : equipment.pickupToken;
                string language = Language.currentLanguageName;
                if (index != previous || language != previousLanguage)
                {
                    description = Localize(token, fallback);
                    previous = index;
                    previousLanguage = language;
                    scroll = Vector2.zero;
                }
                title = Language.GetString(def.nameToken);
                titleColor = def.baseColor;
            }
            catch (Exception ex)
            {
                // A broken interactable must not spam the log every frame.
                title = null;
                if (Time.unscaledTime >= nextErrorLog)
                {
                    nextErrorLog = Time.unscaledTime + 10f;
                    Logger.LogWarning(ex);
                }
            }
        }

        private static string Localize(string token, string fallback)
        {
            string value = string.IsNullOrEmpty(token) ? null : Language.GetString(token);
            if (string.IsNullOrEmpty(value) || value == token) value = Language.GetString(fallback);
            return UnsupportedTags.Replace((value ?? string.Empty).Replace("<br>", "\n"), string.Empty);
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(title) || !panelEnabled.Value) return;
            if (bodyStyle == null)
            {
                bodyStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = true, padding = new RectOffset(0, 0, 0, 0) };
                bodyStyle.normal.textColor = Color.white;
                titleStyle = new GUIStyle(bodyStyle) { fontStyle = FontStyle.Bold };
            }
            bodyStyle.fontSize = fontSize.Value;
            titleStyle.fontSize = fontSize.Value + 3;
            titleStyle.normal.textColor = titleColor;
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            Color oldBackground = GUI.backgroundColor;
            try
            {
                float factor = Mathf.Max(0.25f, Screen.height / 1080f * scale.Value);
                GUI.matrix = Matrix4x4.Scale(new Vector3(factor, factor, 1f));
                float screenW = Screen.width / factor, screenH = Screen.height / factor;
                float w = Mathf.Min(width.Value, screenW - 24f);
                float inner = w - 32f;
                float heading = titleStyle.CalcHeight(new GUIContent(title), inner);
                float content = bodyStyle.CalcHeight(new GUIContent(description), inner - 20f);
                float h = Mathf.Min(heading + content + 44f, screenH * 0.65f);
                Rect panel = new Rect(Mathf.Clamp(x.Value * screenW, 12f, screenW - w - 12f),
                    Mathf.Clamp(y.Value * screenH, 12f, screenH - h - 12f), w, h);
                GUI.color = new Color(0.035f, 0.045f, 0.065f, 0.95f);
                GUI.DrawTexture(panel, Texture2D.whiteTexture);
                GUI.color = titleColor;
                GUI.DrawTexture(new Rect(panel.x, panel.y, 4f, h), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(panel.x + 16f, panel.y + 12f, inner, heading), title, titleStyle);
                Rect viewport = new Rect(panel.x + 16f, panel.y + heading + 22f, inner, h - heading - 34f);
                scroll = GUI.BeginScrollView(viewport, scroll, new Rect(0, 0, inner - 20f, content));
                GUI.Label(new Rect(0, 0, inner - 20f, content), description, bodyStyle);
                GUI.EndScrollView();
            }
            finally
            {
                GUI.matrix = oldMatrix;
                GUI.color = oldColor;
                GUI.backgroundColor = oldBackground;
            }
        }
    }
}
