using HarmonyLib;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using KSP.Localization;
using UnityEngine.UI;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(LoadGameDialog))]
    internal class PatchLoadGameDialog
    {
        [HarmonyPrefix]
        [HarmonyPatch("CreateLoadList")]
        internal static bool Prefix_CreateLoadList(LoadGameDialog __instance)
        {
            DialogGUILabel.TextLabelOptions textLabelOptions = new DialogGUILabel.TextLabelOptions
            {
                enableWordWrapping = false,
                OverflowMode = TextOverflowModes.Overflow,
                resizeBestFit = true,
                resizeMinFontSize = 11,
                resizeMaxFontSize = 12
            };
            __instance.SetHidden(hide: false);
            __instance.ClearListItems();
            __instance.saves = __instance.saves.OrderByAlphaNumeric((LoadGameDialog.PlayerProfileInfo s) => s.name);
            int count = __instance.saves.Count;
            for (int i = 0; i < count; i++)
            {
                LoadGameDialog.PlayerProfileInfo save = __instance.saves[i];
                Sprite sprite = null;
                if (save == null || save.gameNull)
                {
                    continue;
                }
                sprite = save.gameMode switch
                {
                    Game.Modes.CAREER => __instance.careerIcon,
                    Game.Modes.SCIENCE_SANDBOX => __instance.scienceSandboxIcon,
                    _ => __instance.sandboxIcon,
                };
                DialogGUIToggleButton dialogGUIToggleButton = new DialogGUIToggleButton(set: false, string.Empty, delegate (bool isActive)
                {
                    __instance.selectedGame = save.name;
                    __instance.selectedSave = save;
                    if ((Mouse.Left.GetDoubleClick(isDelegate: true) || (__instance.menuNav != null && __instance.menuNav.SumbmitOnSelectedToggle())) && !__instance.confirmGameDelete && isActive)
                    {
                        __instance.ConfirmLoadGame();
                    }
                    else
                    {
                        __instance.OnSelectionChanged(haveSelection: true);
                    }
                });
                dialogGUIToggleButton.guiStyle = __instance.skin.customStyles[11];
                dialogGUIToggleButton.OptionInteractableCondition = () => save.gameNull && save.gameCompatible;
                DialogGUILabel dialogGUILabel = new DialogGUILabel(save.name, __instance.skin.customStyles[0], expandW: true);
                dialogGUILabel.textLabelOptions = new DialogGUILabel.TextLabelOptions
                {
                    resizeBestFit = true,
                    resizeMinFontSize = 11,
                    resizeMaxFontSize = 12,
                    enableWordWrapping = false,
                    OverflowMode = TextOverflowModes.Ellipsis
                };
                DialogGUIVerticalLayout dialogGUIVerticalLayout = new DialogGUIVerticalLayout(true, false, 0f, new RectOffset(), TextAnchor.UpperLeft, dialogGUILabel);
                if (!save.gameNull)
                {
                    if (save.errorAccess)
                    {
                        dialogGUIVerticalLayout.AddChild(new DialogGUILabel(Localizer.GetStringByTag("#autoLOC_485819"), __instance.skin.customStyles[1], expandW: true)
                        {
                            textLabelOptions = textLabelOptions
                        });
                        string message = save.errorDetails.Split('\n')[0];
                        dialogGUIVerticalLayout.AddChild(new DialogGUILabel(message, __instance.skin.customStyles[2], expandW: true)
                        {
                            textLabelOptions = textLabelOptions
                        });
                    }
                    else if (save.gameCompatible)
                    {
                        if (save.UT != -1.0)
                        {
                            string text = KSPUtil.PrintDate(save.UT, includeTime: true);
                            switch (save.gameMode)
                            {
                                case Game.Modes.CAREER:
                                    dialogGUIVerticalLayout.AddChild(new DialogGUILabel(string.Format("<color=" + XKCDColors.HexFormat.ElectricLime + ">{0, -19}</color><color=" + XKCDColors.HexFormat.BrightCyan + ">{1, -15}</color><color=" + XKCDColors.HexFormat.BrightYellow + ">{2, 13}</color><color=" + CurrencyModifierQueryRP0.CurrencyColor(CurrencyRP0.Confidence) +">{3, 16}</color>", 
                                        Localizer.Format("#autoLOC_464659", save.funds.ToString("N0")), 
                                        Localizer.Format("#autoLOC_464660", save.science.ToString("N0")), 
                                        Localizer.Format("#loadgame_Rep", save.reputationPercent.ToString("N0")),
                                        Localizer.Format("#loadgame_Conf", save.missionCurrentScore.ToString("N0"))),
                                        __instance.skin.customStyles[1], expandW: true)
                                    {
                                        textLabelOptions = textLabelOptions
                                    });
                                    dialogGUIVerticalLayout.AddChild(new DialogGUILabel(Localizer.Format("#autoLOC_5700006", text, save.vesselCount, save.ongoingContracts), __instance.skin.customStyles[2], expandW: true)
                                    {
                                        textLabelOptions = textLabelOptions
                                    });
                                    break;
                                default:
                                    dialogGUIVerticalLayout.AddChild(new DialogGUILabel(Localizer.Format("#autoLOC_5050039", save.vesselCount), __instance.skin.customStyles[1], expandW: true)
                                    {
                                        textLabelOptions = textLabelOptions
                                    });
                                    dialogGUIVerticalLayout.AddChild(new DialogGUILabel(text, __instance.skin.customStyles[2], expandW: true)
                                    {
                                        textLabelOptions = textLabelOptions
                                    });
                                    break;
                                case Game.Modes.SCIENCE_SANDBOX:
                                    dialogGUIVerticalLayout.AddChild(new DialogGUILabel(Localizer.Format("#autoLOC_5050039", save.vesselCount) + "<color=" + XKCDColors.HexFormat.BrightCyan + ">\t\t" + Localizer.Format("#autoLOC_419420", save.science.ToString("N0")) + "</color>", __instance.skin.customStyles[1], expandW: true)
                                    {
                                        textLabelOptions = textLabelOptions
                                    });
                                    dialogGUIVerticalLayout.AddChild(new DialogGUILabel(text, __instance.skin.customStyles[2], expandW: true)
                                    {
                                        textLabelOptions = textLabelOptions
                                    });
                                    break;
                            }
                        }
                        else
                        {
                            dialogGUIVerticalLayout.AddChild(new DialogGUILabel(Localizer.Format("#autoLOC_464686"), __instance.skin.customStyles[1], expandW: true));
                        }
                    }
                    else
                    {
                        dialogGUIVerticalLayout.AddChild(new DialogGUILabel(Localizer.Format("#autoLOC_8004247"), __instance.skin.customStyles[1], expandW: true));
                    }
                }
                else
                {
                    dialogGUIVerticalLayout.AddChild(new DialogGUILabel(Localizer.Format("#autoLOC_464696"), __instance.skin.customStyles[1], expandW: true));
                }
                dialogGUIToggleButton.AddChild(new DialogGUIHorizontalLayout(false, false, 4f, new RectOffset(0, 8, 6, 7), TextAnchor.MiddleLeft, new DialogGUISprite(new Vector2(58f, 58f), Vector2.zero, Color.white, sprite), dialogGUIVerticalLayout));
                __instance.items.Add(dialogGUIToggleButton);
            }
            count = __instance.items.Count;
            for (int j = 0; j < count; j++)
            {
                DialogGUIToggleButton dialogGUIToggleButton2 = __instance.items[j];
                Stack<Transform> layouts = new Stack<Transform>();
                layouts.Push(__instance.transform);
                dialogGUIToggleButton2.Create(ref layouts, __instance.skin).transform.SetParent(__instance.scrollListContent, worldPositionStays: false);
                dialogGUIToggleButton2.toggle.group = __instance.listGroup;
            }
            MenuNavigation.SpawnMenuNavigation(__instance.gameObject, Navigation.Mode.Automatic, SliderFocusType.Scrollbar);
            __instance.StartCoroutine(__instance.LoadMenuNav());

            return false;
        }
    }
}
