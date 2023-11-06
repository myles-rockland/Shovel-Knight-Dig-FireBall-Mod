using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using TMPro;
using I2;
using BepInEx.Logging;
using I2.Loc;
using System.IO;

namespace FireBallPlugin
{
    //Plugin to load patch
    [BepInPlugin("rockm3000.skdig.firedig", "FireDig Mod", "1.0.0.2")]
    [BepInProcess("skDig64.exe")]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo("Plugin rockm3000.skdig.firedig is loaded!");

            //Applying the mechanic patch
            var harmony = new Harmony("rockm3000.skdig.firedig");
            var original = typeof(Player).GetMethod(nameof(Player.ActionPressedDown));
            var postfix = typeof(FireBallPatch).GetMethod(nameof(FireBallPatch.SpawnFireball));
            harmony.Patch(original, postfix: new HarmonyMethod(postfix));

            //Applying the popup patch
            original = typeof(TitleScreen).GetMethod(nameof(TitleScreen.TitleScreenJingle));
            var prefix = typeof(ShowingPopupPatch).GetMethod(nameof(ShowingPopupPatch.ChangeShowingPopup));
            harmony.Patch(original, prefix: new HarmonyMethod(prefix));

            //Applying the popup patch
            original = typeof(GenericMessagePopup).GetMethod(nameof(GenericMessagePopup.OpenQueue));
            prefix = typeof(FireBallPopupPatch).GetMethod(nameof(FireBallPopupPatch.SpawnFireballPopup));
            postfix = typeof(FireBallPopupPatch).GetMethod(nameof(FireBallPopupPatch.ChangePopupText));
            harmony.Patch(original, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));

            //Applying the close popup patch
            original = typeof(GenericMessagePopup).GetMethod(nameof(GenericMessagePopup.Close));
            postfix = typeof(FireBallClosePopupPatch).GetMethod(nameof(FireBallClosePopupPatch.ChangeNextPopupText));
            harmony.Patch(original, postfix: new HarmonyMethod(postfix));

            Logger.LogInfo("FireDig patch should be applied");
        }
    }

    //FireBall mechanic patch
    [HarmonyPatch(typeof(Player), nameof(Player.ActionPressedDown))]
    class FireBallPatch
    {
        private static ManualLogSource fireMechPatchLog = BepInEx.Logging.Logger.CreateLogSource("FireMechPatchLog");
        private static bool firePowerActive = false;
        [HarmonyPostfix]
        public static void SpawnFireball(Player __instance)
        {
            if (__instance.Input.m_HoldingUp && !__instance.Input.DisabledInput)
            {
                firePowerActive = !firePowerActive;
                fireMechPatchLog.LogInfo("Fire Power: " + (firePowerActive ? "ON" : "OFF"));
                __instance.m_Anim.PlayAnimation("first land");
                __instance.m_Anim.SetCurrentFrame(20);
                /*
                //Add/Remove second player
                if (firePowerActive)
                {
                    StageController.Instance.AddPlayer2();
                }
                else
                {
                    StageController.Instance.RemovePlayer2();
                }
                */

                /*
                // Code for getting all dialogue
                List<string> list = LocalisationToolPanel.GetList(LocalisationToolPanel.CATEGORY.DIALOGUES);
                for(int i = 0; i < list.Count; i++)
                {
                    int num2 = 1;
                    string idWithoutNumber = DialogueBox.GetIdWithoutNumber(list[i]);
                    int num3 = i + 1;
                    while (num3 < list.Count && DialogueBox.GetIdWithoutNumber(list[num3]) == idWithoutNumber)
                    {
                        num2++;
                        num3++;
                    }
                    for (int j = 0; j < num2; j++)
                    {
                        int index = i + j;
                        string text = list[index];
                        string speakerId = LocalizationManager.GetTranslation(text, true, 0, true, false, null, "Speaker", true);
                        string speakerName = LocalizationManager.GetTranslation("Speakers/" + speakerId, true, 0, true, false, null, null, true);
                        string newText = RTLFixer.GetTranslationRTLFix(text);
                        //FileStream file = File.Create("skdDialogue.txt");
                        File.AppendAllText("skdDialogue.txt", speakerName + ": " + newText + Environment.NewLine);
                    }
                }
                //Dialogue code ends here
                */
            }
            else if (firePowerActive && !__instance.Input.DisabledInput)
            {
                //Spawn fireball
                GameObject item = FireRod.Spawn(__instance.m_FireRod_prefab, __instance.m_GoingRight, __instance.transform.position + new Vector3(__instance.m_GoingRight ? 5f : -5f, 0f, 0f));
                __instance.m_CurrentSpawnedItems.Add(item);
                SoundManager.GetInstance().PlaySound("player_flare_wand_cast", 0.7f, false, 8, false, 1f, 128, -1, SoundManager.SourceData.SOUND_FLAGS.NONE, 0f);
            }
        }
    }

    //Show popup patch
    [HarmonyPatch(typeof(TitleScreen), nameof(TitleScreen.TitleScreenJingle))]
    class ShowingPopupPatch
    {
        [HarmonyPrefix]
        public static void ChangeShowingPopup(ref bool ___m_ShowingPopups)
        {
            ___m_ShowingPopups = true;
        }
    }

    //FireBall active mod popup patch
    [HarmonyPatch(typeof(GenericMessagePopup), nameof(GenericMessagePopup.OpenQueue))]
    class FireBallPopupPatch
    {
        private static ManualLogSource popupPatchLog = BepInEx.Logging.Logger.CreateLogSource("PopupPatchLog");
        public static bool textChanged = false;
        [HarmonyPrefix]
        public static void SpawnFireballPopup(GenericMessagePopup __instance, ref Queue<GenericMessagePopup.QueuedPopup> popupQueue)
        {
            popupQueue.Enqueue(new GenericMessagePopup.QueuedPopup(GenericMessagePopup.TYPE.TRUE_ENDING_COMPLETE, new Action(OverworldEvents.SetShownKnightmareModePopup), string.Empty, true));
        }

        [HarmonyPostfix]
        public static void ChangePopupText(GenericMessagePopup __instance)
        {
            if(!textChanged)
            {
                Transform childByName = Utilities.GetChildByName(__instance.m_Pannels[4].transform, "title text");
                Transform childByName2 = Utilities.GetChildByName(__instance.m_Pannels[4].transform, "body text");
                childByName.GetComponent<TextMeshProUGUI>().text = "fireball mod activated!";
                childByName2.GetComponent<TextMeshProUGUI>().text = "you gained the power of <color=#FF0000>fire<color=#FFFFFF>! press up + attack to toggle your power!";
                popupPatchLog.LogInfo("Changed text to FireBall mod text.");
                textChanged = true;
            }
        }
    }

    //Popup close patch
    [HarmonyPatch(typeof(GenericMessagePopup), nameof(GenericMessagePopup.Close))]
    class FireBallClosePopupPatch
    {
        private static ManualLogSource nextPopupPatchLog = BepInEx.Logging.Logger.CreateLogSource("NextPopupPatchLog");
        [HarmonyPostfix]
        public static void ChangeNextPopupText(GenericMessagePopup __instance)
        {
            nextPopupPatchLog.LogInfo("Popup was closed.");
            if (!FireBallPopupPatch.textChanged)
            {
                Transform childByName = Utilities.GetChildByName(__instance.m_Pannels[4].transform, "title text");
                Transform childByName2 = Utilities.GetChildByName(__instance.m_Pannels[4].transform, "body text");
                childByName.GetComponent<TextMeshProUGUI>().text = "fireball mod activated!";
                childByName2.GetComponent<TextMeshProUGUI>().text = "you gained the power of <color=#FF0000>fire<color=#FFFFFF>! press up + attack to toggle your power!";
                FireBallPopupPatch.textChanged = true;
                nextPopupPatchLog.LogInfo("Changed text to FireBall mod text.");
            }
        }
    }
}
