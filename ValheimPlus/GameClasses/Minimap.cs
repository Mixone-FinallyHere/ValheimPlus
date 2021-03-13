using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ValheimPlus.Configurations;
using ValheimPlus.RPC;
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
        public static GameObject iconPanel;
        public static bool devMode = true;
        public static Dropdown iconSelected;
        public static Vector3 pinPos;

        [HarmonyPatch(typeof(Minimap), "Awake")]
        public static class MapPinEditor_Patches_Awake
        {            
            private static void Postfix(ref Minimap __instance)
            {
                if (devMode)
                {
                    DefaultControls.Resources uiResources = new DefaultControls.Resources();                                  
                    iconPanel = Helper.GetChildComponentByName<Transform>("IconPanel", __instance.m_largeRoot).gameObject;
                    for (int i = 0; i < 5; i++)
                    {
                        Helper.GetChildComponentByName<Transform>("Icon" + i.ToString(), iconPanel).gameObject.SetActive(false);
                    }
                    __instance.m_nameInput.gameObject.SetActive(false);
                    iconPanel.transform.localPosition = new Vector3(0, 0, 0);
                    RectTransform iconPanelRect = iconPanel.GetComponent<RectTransform>();
                    iconPanelRect.sizeDelta = new Vector2(500, 400);
                    Helper.GetChildComponentByName<Transform>("Bkg", iconPanel).gameObject.SetActive(false);
                    iconPanel.GetComponent<Image>().type = Image.Type.Simple;
                    iconPanel.SetActive(false);

                    GameObject iconDropdown = DefaultControls.CreateDropdown(uiResources);
                    iconDropdown.transform.SetParent(iconPanel.transform, false);
                    iconDropdown.transform.localScale = new Vector3(1.5f, 1.5f);
                    List<string> list = new List<string> { "Fire", "Home", "Hammer", "Circle", "Rune" };
                    iconSelected = iconDropdown.GetComponent<Dropdown>();
                    iconSelected.options.Clear();
                    int ind = 0;
                    foreach (string option in list)
                    {
                        iconSelected.options.Add(new Dropdown.OptionData(option, __instance.m_icons[ind].m_icon));
                        ind++;
                    }
                    GameObject closeButton = DefaultControls.CreateButton(uiResources);
                    closeButton.transform.SetParent(iconPanel.transform, false);
                    closeButton.GetComponent<RectTransform>().anchorMin = new Vector2(0, 1);
                    closeButton.GetComponent<RectTransform>().anchorMax = new Vector2(0, 1);
                    closeButton.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
                    closeButton.GetComponent<RectTransform>().localPosition = new Vector2(100, -50);
                    closeButton.GetComponent<Button>().onClick.AddListener(() => iconPanel.SetActive(false));
                    closeButton.GetComponent<Button>().gameObject.GetComponentInChildren<Text>().text = "Close";

                    GameObject okButton = DefaultControls.CreateButton(uiResources);
                    okButton.transform.SetParent(iconPanel.transform, false);
                    okButton.GetComponent<RectTransform>().anchorMin = new Vector2(1, 1);
                    okButton.GetComponent<RectTransform>().anchorMax = new Vector2(1, 1);
                    okButton.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
                    okButton.GetComponent<RectTransform>().localPosition = new Vector2(-100, -50);
                    okButton.GetComponent<Button>().onClick.AddListener(() => iconPanel.SetActive(false));
                    okButton.GetComponent<Button>().gameObject.GetComponentInChildren<Text>().text = "Ok";

                    __instance.m_nameInput.gameObject.SetActive(false);

                }
            }
        }

        [HarmonyPatch(typeof(Minimap), "OnMapDblClick")]
        public static class MapPinEditor_Patches_OnMapDblClick
        {
            private static bool Prefix(ref Minimap __instance)
            {
                if (!iconPanel.activeSelf)
                {
                    iconPanel.SetActive(true);
                    __instance.m_nameInput.gameObject.SetActive(true);
                }
                pinPos = __instance.ScreenToWorldPoint(Input.mousePosition);
                //iconPanel.transform.localPosition = pinPos;
                return false;
            }
        }

        [HarmonyPatch(typeof(Minimap), "UpdateNameInput")]
        public static class MapPinEditor_Patches_UpdateNameInput
        {
            private static bool Prefix(ref Minimap __instance)
            {
                if (__instance.m_namePin != null && __instance.m_mode == Minimap.MapMode.Large)
                {
                    if (Input.GetKeyDown(KeyCode.Escape))
                    {
                        __instance.m_namePin = null;
                    }
                    if (!__instance.m_nameInput.isFocused)
                    {
                        EventSystem.current.SetSelectedGameObject(__instance.m_nameInput.gameObject);
                    }
                    if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    {
                        string text = __instance.m_nameInput.text;
                        text = text.Replace('$', ' ');
                        text = text.Replace('<', ' ');
                        text = text.Replace('>', ' ');
                        Minimap.PinData pin = __instance.AddPin(pinPos, (Minimap.PinType)iconSelected.value, text, true, false);
                        iconPanel.SetActive(false);
                        __instance.m_namePin = null;
                    }
                    __instance.m_wasFocused = true;                    
                } else
                    __instance.m_nameInput.gameObject.SetActive(false);
                return false;
            }
        }
    }
}
