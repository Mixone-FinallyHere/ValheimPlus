using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.U2D;
using UnityEngine.UI;
using ValheimPlus.Configurations;
using ValheimPlus.RPC;
using ValheimPlus.Utility;
using Random = UnityEngine.Random;

// ToDo add packet system to convey map markers
namespace ValheimPlus.GameClasses
{
    /// <summary>
    /// Hooks base explore method
    /// </summary>
    [HarmonyPatch(typeof(Minimap))]
    public class HookExplore
    {
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(Minimap), "Explore", new Type[] { typeof(Vector3), typeof(float) })]
        public static void call_Explore(object instance, Vector3 p, float radius) => throw new NotImplementedException();
    }

    /// <summary>
    /// Update exploration for all players
    /// </summary>
    [HarmonyPatch(typeof(Minimap), "UpdateExplore")]
    public static class ChangeMapBehavior
    {
        private static void Prefix(ref float dt, ref Player player, ref Minimap __instance, ref float ___m_exploreTimer, ref float ___m_exploreInterval)
        {
            if (Configuration.Current.Map.exploreRadius > 10000) Configuration.Current.Map.exploreRadius = 10000;

            if (!Configuration.Current.Map.IsEnabled) return;

            if (Configuration.Current.Map.shareMapProgression)
            {
                float explorerTime = ___m_exploreTimer;
                explorerTime += Time.deltaTime;
                if (explorerTime > ___m_exploreInterval)
                {
                    if (ZNet.instance.m_players.Any())
                    {
                        foreach (ZNet.PlayerInfo m_Player in ZNet.instance.m_players)
                        {
                            HookExplore.call_Explore(__instance, m_Player.m_position, Configuration.Current.Map.exploreRadius);
                        }
                    }
                }
            }

            // Always reveal for your own, we do this non the less to apply the potentially bigger exploreRadius
            HookExplore.call_Explore(__instance, player.transform.position, Configuration.Current.Map.exploreRadius);
        }
    }

    [HarmonyPatch(typeof(Minimap), "Awake")]
    public static class MinimapAwake
    {
        private static void Postfix(ref Minimap __instance)
        {
            if (ZNet.m_isServer && Configuration.Current.Map.IsEnabled && Configuration.Current.Map.shareMapProgression)
            {
                //Init map array
                VPlusMapSync.ServerMapData = new bool[Minimap.instance.m_textureSize * Minimap.instance.m_textureSize];

                //Load map data from disk
                VPlusMapSync.LoadMapDataFromDisk();

                //Start map data save timer
                ValheimPlusPlugin.mapSyncSaveTimer.Start();
            }
        }
    }

    public static class MapPinEditor_Patches
    {
        public static GameObject pinEditorPanel;
        public static bool devMode = true;
        public static Dropdown iconSelected;
        public static InputField pinName;
        public static Toggle sharePin;
        public static Vector3 pinPos;
        public static DefaultControls.Resources uiResources;
        public static Sprite[] gameSprites;
        public static Font[] gameFonts;
        public static Texture2D[] gameTextures;
        public static SpriteAtlas[] spriteAtlases;

        [HarmonyPatch(typeof(Minimap), "Awake")]
        public static class MapPinEditor_Patches_Awake
        {    
            private static void AddPin(ref Minimap __instance)
            {
                __instance.AddPin(pinPos, (Minimap.PinType)iconSelected.value, pinName.text, true, false);
                pinEditorPanel.SetActive(false);
            }

            private static void Postfix(ref Minimap __instance)
            {
                gameSprites = Resources.FindObjectsOfTypeAll(typeof(Sprite)) as Sprite[];
                gameFonts = Resources.FindObjectsOfTypeAll(typeof(Font))as Font[];
                gameTextures = Resources.FindObjectsOfTypeAll(typeof(Texture2D)) as Texture2D[];
                spriteAtlases = Resources.FindObjectsOfTypeAll(typeof(SpriteAtlas)) as SpriteAtlas[];
                Font norseFont = gameFonts.First(x => x.name == "Norse");
                if (devMode)
                {
                    uiResources = new DefaultControls.Resources();
                    uiResources.background = gameSprites.First(sprite => sprite.name == "panel_bkg_128");
                    uiResources.standard = gameSprites.First(sprite => sprite.name == "button");
                    uiResources.dropdown = gameSprites.First(sprite => sprite.name == "mapicon_randevent");
                    uiResources.inputField = gameSprites.First(sprite => sprite.name == "button");
                    uiResources.knob = gameSprites.First(sprite => sprite.name == "button");
                    uiResources.checkmark = gameSprites.First(sprite => sprite.name == "mapicon_checked");

                    GameObject iconPanelOld = Helper.GetChildComponentByName<Transform>("IconPanel", __instance.m_largeRoot).gameObject;
                    for (int i = 0; i < 5; i++)
                    {
                        Helper.GetChildComponentByName<Transform>("Icon" + i.ToString(), iconPanelOld).gameObject.SetActive(false);
                    }
                    Helper.GetChildComponentByName<Transform>("Bkg", iconPanelOld).gameObject.SetActive(false);
                    iconPanelOld.SetActive(false);
                    __instance.m_nameInput.gameObject.SetActive(false);

                    pinEditorPanel = DefaultControls.CreatePanel(uiResources);
                    pinEditorPanel.name = "Pin Editor";
                    pinEditorPanel.transform.SetParent(__instance.m_largeRoot.transform, false);
                    //pinEditorPanel.transform.localPosition = new Vector3(0, 0, 0);
                    pinEditorPanel.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 0.5f);
                    pinEditorPanel.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0.5f);
                    pinEditorPanel.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
                    pinEditorPanel.GetComponent<RectTransform>().localPosition = new Vector2(0, 0);
                    pinEditorPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(600, 400);

                    pinEditorPanel.GetComponent<Image>().type = Image.Type.Simple;
                    pinEditorPanel.GetComponent<Image>().sprite = uiResources.background;
                    pinEditorPanel.GetComponent<Image>().color = new Color32(255,255,255,255);
                    pinEditorPanel.SetActive(false);

                    /*
                    Stream logoStream = EmbeddedAsset.LoadEmbeddedAsset("Assets.valheimPlusIcon.png");
                    if (logoStream != null)
                        Debug.Log("Stream loaded properly");
                    Texture2D logoTexture = Helper.LoadPng(logoStream);
                    if (logoTexture != null)
                        Debug.Log("Texture loaded properly");
                    Sprite logoSprite = Sprite.Create(logoTexture, new Rect(0, 0, logoTexture.width, logoTexture.height), new Vector2(0.5f, 0.5f));
                    if (logoSprite != null)
                        Debug.Log("Sprite loaded properly");
                    logoStream.Dispose();
                    */

                    GameObject titlePinEditor = DefaultControls.CreateText(uiResources);
                    titlePinEditor.transform.SetParent(pinEditorPanel.transform, false);
                    titlePinEditor.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 0.5f);
                    titlePinEditor.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0.5f);
                    titlePinEditor.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
                    titlePinEditor.GetComponent<RectTransform>().localPosition = new Vector2(0, 135);
                    titlePinEditor.GetComponent<RectTransform>().sizeDelta = new Vector2(484, 80);
                    titlePinEditor.GetComponent<Text>().font = norseFont;
                    titlePinEditor.GetComponent<Text>().fontSize = 60;
                    titlePinEditor.GetComponent<Text>().fontStyle = FontStyle.Bold;
                    titlePinEditor.GetComponent<Text>().color = new Color32(240, 172, 87, 255);
                    titlePinEditor.GetComponent<Text>().gameObject.AddComponent<Outline>();
                    titlePinEditor.GetComponent<Text>().text = "V+ Map Pin Editor";
                    titlePinEditor.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
                    /*
                    GameObject titleImage = DefaultControls.CreateImage(uiResources);
                    titleImage.transform.SetParent(titlePinEditor.transform, false);
                    titleImage.GetComponent<Image>().sprite = logoSprite;
                    titleImage.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 0.5f);
                    titleImage.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0.5f);
                    titleImage.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
                    titleImage.GetComponent<RectTransform>().localPosition = new Vector2(-162, 135);
                    titleImage.GetComponent<RectTransform>().sizeDelta = new Vector2(75, 75);
                    */

                    GameObject iconDropdown = DefaultControls.CreateDropdown(uiResources);
                    iconDropdown.transform.SetParent(pinEditorPanel.transform, false);
                    iconDropdown.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 0.5f);
                    iconDropdown.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0.5f);
                    iconDropdown.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
                    iconDropdown.GetComponent<RectTransform>().localPosition = new Vector2(0, -25);
                    iconDropdown.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 50);
                    iconDropdown.GetComponentInChildren<Text>().font = norseFont;
                    iconDropdown.GetComponentInChildren<Text>().fontSize = 26;
                    iconDropdown.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
                    iconDropdown.GetComponentInChildren<Text>().color = new Color32(240, 172, 87, 255);
                    iconDropdown.GetComponentInChildren<Text>().gameObject.AddComponent<Outline>();
                    iconDropdown.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleCenter;
                    //Helper.GetChildComponentByName<Image>("Content", iconDropdown).sprite = uiResources.standard;
                    Helper.GetChildComponentByName<Text>("Item Label", iconDropdown).alignment = TextAnchor.MiddleCenter;
                    Helper.GetChildComponentByName<Text>("Item Label", iconDropdown).font = norseFont;
                    Helper.GetChildComponentByName<Text>("Item Label", iconDropdown).fontSize = 26;
                    Helper.GetChildComponentByName<Text>("Item Label", iconDropdown).fontStyle = FontStyle.Bold;
                    Helper.GetChildComponentByName<Text>("Item Label", iconDropdown).color = new Color32(240, 172, 87, 255);
                    Helper.GetChildComponentByName<Text>("Item Label", iconDropdown).gameObject.AddComponent<Outline>();
                    List<string> list = new List<string> { "Fire", "Home", "Hammer", "Circle", "Rune" };
                    iconSelected = iconDropdown.GetComponent<Dropdown>();
                    iconSelected.options.Clear();
                    int ind = 0;
                    foreach (string option in list)
                    {
                        iconSelected.options.Add(new Dropdown.OptionData(option, __instance.m_icons[ind].m_icon));
                        ind++;
                    }

                    if (iconDropdown != null)
                        Debug.Log("Dropdown loaded properly");

                    GameObject sharePinObj = DefaultControls.CreateToggle(uiResources);
                    sharePinObj.transform.SetParent(pinEditorPanel.transform, false);
                    sharePinObj.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 0.5f);
                    sharePinObj.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0.5f);
                    sharePinObj.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
                    sharePinObj.GetComponent<RectTransform>().localPosition = new Vector2(0, 25);
                    sharePinObj.GetComponent<RectTransform>().sizeDelta = new Vector2(160, 40);
                    sharePinObj.GetComponentInChildren<Text>().text = "Share this pin";
                    sharePinObj.GetComponentInChildren<Text>().font = norseFont;
                    sharePinObj.GetComponentInChildren<Text>().fontSize = 20;
                    sharePinObj.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
                    sharePinObj.GetComponentInChildren<Text>().color = new Color32(240, 172, 87, 255);
                    sharePinObj.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleCenter;
                    sharePinObj.GetComponentInChildren<Text>().gameObject.AddComponent<Outline>();
                    sharePin = sharePinObj.GetComponent<Toggle>();

                    if (sharePinObj != null)
                        Debug.Log("Share pin loaded properly");

                    GameObject pinNameObj = DefaultControls.CreateInputField(uiResources);
                    pinNameObj.transform.SetParent(pinEditorPanel.transform, false);
                    pinNameObj.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 0.5f);
                    pinNameObj.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0.5f);
                    pinNameObj.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
                    pinNameObj.GetComponent<RectTransform>().localPosition = new Vector2(0, 75);
                    pinNameObj.GetComponent<RectTransform>().sizeDelta = new Vector2(455, 50);
                    pinNameObj.transform.GetChild(0).GetComponent<Text>().font = norseFont;
                    pinNameObj.transform.GetChild(0).GetComponent<Text>().fontSize = 27;
                    pinNameObj.transform.GetChild(0).GetComponent<Text>().fontStyle = FontStyle.BoldAndItalic;
                    pinNameObj.transform.GetChild(0).GetComponent<Text>().color = new Color32(240, 172, 87, 255);
                    pinNameObj.transform.GetChild(0).gameObject.AddComponent<Outline>();
                    pinNameObj.transform.GetChild(1).GetComponent<Text>().font = norseFont;
                    pinNameObj.transform.GetChild(1).GetComponent<Text>().fontSize = 27;
                    pinNameObj.transform.GetChild(1).GetComponent<Text>().fontStyle = FontStyle.BoldAndItalic;
                    pinNameObj.transform.GetChild(1).GetComponent<Text>().color = new Color32(240, 172, 87, 255);
                    pinNameObj.transform.GetChild(1).gameObject.AddComponent<Outline>();
                    pinName = pinNameObj.GetComponent<InputField>();

                    if (pinNameObj != null)
                        Debug.Log("Pin Name loaded properly");

                    GameObject closeButton = DefaultControls.CreateButton(uiResources);
                    closeButton.transform.SetParent(pinEditorPanel.transform, false);
                    closeButton.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 0.5f);
                    closeButton.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0.5f);
                    closeButton.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
                    closeButton.GetComponent<RectTransform>().localPosition = new Vector2(-150, -125);
                    closeButton.GetComponent<RectTransform>().sizeDelta = new Vector2(150, 50);
                    closeButton.GetComponent<Button>().onClick.AddListener(() => pinEditorPanel.SetActive(false));
                    closeButton.GetComponentInChildren<Text>().text = "Close";
                    closeButton.GetComponentInChildren<Text>().font = norseFont;
                    closeButton.GetComponentInChildren<Text>().fontSize = 25;
                    closeButton.GetComponentInChildren<Text>().color = new Color32(240, 172, 87, 255);
                    closeButton.AddComponent<Outline>();

                    if (closeButton != null)
                        Debug.Log("Close button loaded properly");

                    GameObject okButton = DefaultControls.CreateButton(uiResources);
                    okButton.transform.SetParent(pinEditorPanel.transform, false);
                    okButton.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 0.5f);
                    okButton.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0.5f);
                    okButton.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
                    okButton.GetComponent<RectTransform>().localPosition = new Vector2(150, -125);
                    okButton.GetComponent<RectTransform>().sizeDelta = new Vector2(150, 50);
                    Minimap theInstance = __instance;
                    okButton.GetComponent<Button>().onClick.AddListener(delegate { AddPin(ref theInstance); });
                    okButton.GetComponentInChildren<Text>().text = "Ok";
                    okButton.GetComponentInChildren<Text>().font = norseFont;
                    okButton.GetComponentInChildren<Text>().fontSize = 25;
                    okButton.GetComponentInChildren<Text>().color = new Color32(240, 172, 87, 255);
                    okButton.AddComponent<Outline>();

                    if (okButton != null)
                        Debug.Log("OK button loaded properly");

                    __instance.m_nameInput.gameObject.SetActive(false);

                }
            }
        }

        [HarmonyPatch(typeof(Minimap), "OnMapDblClick")]
        public static class MapPinEditor_Patches_OnMapDblClick
        {
            private static bool Prefix(ref Minimap __instance)
            {                
                if (!pinEditorPanel.activeSelf)
                {
                    pinEditorPanel.SetActive(true);
                }
                if (!pinName.isFocused)
                {
                    EventSystem.current.SetSelectedGameObject(pinName.gameObject);
                    __instance.m_wasFocused = true;
                }
                pinPos = __instance.ScreenToWorldPoint(Input.mousePosition);
                //iconPanel.transform.localPosition = pinPos;
                return false;
            }
        }
    }
}
