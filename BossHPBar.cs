using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

// Zostawiamy aliasy dla głównych aktorów:
using P1Actor = P1.GameSystem.Actor;
using P2Actor = P2.GameSystem.Actor;

namespace BossHPBar
{
    [BepInPlugin("com.haziksx.bosshpbar", "BossHPBar", "0.3.0")]
    public class BossHealthPlugin : BasePlugin
    {
        public static ManualLogSource PluginLogger;

        public override void Load()
        {
            PluginLogger = Log;
            ClassInjector.RegisterTypeInIl2Cpp<BossHealthUIController>();
            Harmony.CreateAndPatchAll(typeof(BossHealthPatches));

            PluginLogger.LogInfo("[BossHPBar] Plugin succesfully loaded (Patapon 1 & 2).");
        }
    }

    public static class BossHealthPatches
    {
        public static IntPtr ActiveBossPointer = IntPtr.Zero;
        public static string ActiveBossName = "BOSS";
        public static float LastUpdateTime = 0f;

        public static Func<(int currentHp, int maxHp)> GetHpValues = null;

        private static bool _uiInitialized = false;

        private static readonly Dictionary<string, string> BossIds = new Dictionary<string, string>
        {
            // --- PATAPON 1 ---
            { "unit101_01_01", "Dodonga" },
            { "unit108_01_01", "Majidonga" },
            { "unit111_01_01", "Ciokina" },
            { "unit112_01_01", "Cioking" },
            { "unit113_01_01", "Shookle" },
            { "unit114_01_01", "Shooshookle" },
            { "unit115_01_01", "Gaeen" },
            { "unit116_01_01", "Dogaeen" },
            { "unit120_01_01", "Zaknel" },
            { "unit121_01_01", "Dokaknel" },
            { "unit122_01_01", "Goruru" },
            { "unit123_01_01", "Garuru" },
            { "unit126_01_01", "Manboth" },
            { "unit127_01_01", "Manboroth" },
            { "unit128_01_01", "Centura" },
            { "unit129_01_01", "Darantula" },
            { "unit132_01_01", "Gorl" },
            { "unit133_01_01", "Kharma" },

            // --- PATAPON 2 ---
            { "unit201_01_01", "Dodonga" },
            { "unit208_01_01", "Majidonga" },
            { "unit242_01_01", "Kacchindonga" },
            { "unit211_01_01", "Ciokina" },
            { "unit212_01_01", "Cioking" },
            { "unit213_01_01", "Shookle" },
            { "unit214_01_01", "Shooshookle" },
            { "unit215_01_01", "Gaeen" },
            { "unit216_01_01", "Dogaeen" },
            { "unit220_01_01", "Zaknel" },
            { "unit221_01_01", "Dokaknel" },
            { "unit222_01_01", "Goruru" },
            { "unit223_01_01", "Garuru" },
            { "unit224_01_01", "Mochichichi" },
            { "unit225_01_01", "Fenicchi" },
            { "unit226_01_01", "Manboth" },
            { "unit227_01_01", "Manboroth" },
            { "unit228_01_01", "Centura" },
            { "unit229_01_01", "Darantula" },
            { "unit230_01_01", "Kanogias" },
            { "unit231_01_01", "Ganodias" },
            { "unit232_01_01", "Dettankarmen" },
            { "unit233_01_01", "Zuttankarmen" }
        };

        private static void EnsureUIExists()
        {
            if (_uiInitialized) return;
            try
            {
                var go = new GameObject("BossHealthUIManager");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.AddComponent<BossHealthUIController>();
                _uiInitialized = true;
            }
            catch (Exception ex)
            {
                BossHealthPlugin.PluginLogger.LogError($"[UI INITIALIZATION ERROR] {ex}");
            }
        }

        // ==========================================
        // HOOKI DLA PATAPON 2
        // ==========================================
        [HarmonyPatch(typeof(P2Actor.ActorObj), "createActorStatus")]
        [HarmonyPostfix]
        public static void PostSpawnBossHookP2(string pArcResourceNode, P2Actor.ActorObj __instance)
        {
            EnsureUIExists();
            if (string.IsNullOrEmpty(pArcResourceNode)) return;

            foreach (var kvp in BossIds)
            {
                if (pArcResourceNode.Contains(kvp.Key))
                {
                    ActiveBossName = kvp.Value;
                    ActiveBossPointer = __instance.Pointer;
                    LastUpdateTime = Time.time;

                    GetHpValues = () =>
                    {
                        if (__instance == null || __instance.Pointer == IntPtr.Zero || __instance.WasCollected) return (0, 0);

                        // Używamy "var" - kompilator sam dowie się, czym jest pActorStatus_
                        var statusObj = __instance.pActorStatus_;
                        if (statusObj == null || statusObj.Pointer == IntPtr.Zero || statusObj.WasCollected) return (0, 0);

                        // Jawnie rzutujemy na GameStatus z przestrzeni P2.GameSystem
                        var stats = statusObj.TryCast<P2.GameSystem.GameStatus>();
                        if (stats == null || stats.WasCollected) return (0, 0);

                        return (stats.getHitPoint(), stats.getMaxHitPoint());
                    };
                    break;
                }
            }
        }

        [HarmonyPatch(typeof(P2Actor.ActorObj), "update")]
        [HarmonyPostfix]
        public static void PostActorUpdateHookP2(P2Actor.ActorObj __instance)
        {
            EnsureUIExists();
            if (ActiveBossPointer != IntPtr.Zero && __instance.Pointer == ActiveBossPointer)
            {
                LastUpdateTime = Time.time;
            }
        }

        // ==========================================
        // HOOKI DLA PATAPON 1
        // ==========================================
        [HarmonyPatch(typeof(P1Actor.ActorObj), "createActorStatus")]
        [HarmonyPostfix]
        public static void PostSpawnBossHookP1(string pArcResourceNode, P1Actor.ActorObj __instance)
        {
            EnsureUIExists();
            if (string.IsNullOrEmpty(pArcResourceNode)) return;

            foreach (var kvp in BossIds)
            {
                if (pArcResourceNode.Contains(kvp.Key))
                {
                    ActiveBossName = kvp.Value;
                    ActiveBossPointer = __instance.Pointer;
                    LastUpdateTime = Time.time;

                    GetHpValues = () =>
                    {
                        if (__instance == null || __instance.Pointer == IntPtr.Zero || __instance.WasCollected) return (0, 0);

                        // Używamy "var"
                        var statusObj = __instance.pActorStatus_;
                        if (statusObj == null || statusObj.Pointer == IntPtr.Zero || statusObj.WasCollected) return (0, 0);

                        // Jawnie rzutujemy na GameStatus z przestrzeni P1.GameSystem
                        var stats = statusObj.TryCast<P1.GameSystem.GameStatus>();
                        if (stats == null || stats.WasCollected) return (0, 0);

                        return (stats.getHitPoint(), stats.getMaxHitPoint());
                    };
                    break;
                }
            }
        }

        [HarmonyPatch(typeof(P1Actor.ActorObj), "update")]
        [HarmonyPostfix]
        public static void PostActorUpdateHookP1(P1Actor.ActorObj __instance)
        {
            EnsureUIExists();
            if (ActiveBossPointer != IntPtr.Zero && __instance.Pointer == ActiveBossPointer)
            {
                LastUpdateTime = Time.time;
            }
        }
    }

    public class BossHealthUIController : MonoBehaviour
    {
        public BossHealthUIController(IntPtr handle) : base(handle) { }

        private float _animatedHp = -1f;
        private IntPtr _lastBossPointer = IntPtr.Zero;

        public void OnGUI()
        {
            IntPtr bossPtr = BossHealthPatches.ActiveBossPointer;

            if (bossPtr == IntPtr.Zero || BossHealthPatches.GetHpValues == null)
            {
                return;
            }

            if (Time.time - BossHealthPatches.LastUpdateTime > 0.5f)
            {
                BossHealthPatches.ActiveBossPointer = IntPtr.Zero;
                BossHealthPatches.GetHpValues = null;
                return;
            }

            var (currentHp, maxHp) = BossHealthPatches.GetHpValues();

            if (currentHp <= 0 || maxHp <= 1)
            {
                BossHealthPatches.ActiveBossPointer = IntPtr.Zero;
                BossHealthPatches.GetHpValues = null;
                return;
            }

            if (_lastBossPointer != bossPtr)
            {
                _animatedHp = currentHp;
                _lastBossPointer = bossPtr;
            }

            _animatedHp = Mathf.Lerp(_animatedHp, currentHp, Time.deltaTime * 6f);

            Color origBg = GUI.backgroundColor;
            Color origContent = GUI.contentColor;
            Color origColor = GUI.color;
            Texture2D origBoxBg = GUI.skin.box.normal.background;

            TextAnchor origAlignment = GUI.skin.label.alignment;
            int origFontSize = GUI.skin.label.fontSize;
            FontStyle origFontStyle = GUI.skin.label.fontStyle;

            try
            {
                float scale = Screen.height / 1080f;

                float barWidth = 700f * scale;
                float barHeight = 32f * scale;
                float border = 3f * scale;

                float x = (Screen.width - barWidth) / 2f;
                float y = 55f * scale;

                GUI.skin.box.normal.background = Texture2D.whiteTexture;

                GUI.backgroundColor = Color.white;
                GUI.Box(new Rect(x - border, y - border, barWidth + (border * 2), barHeight + (border * 2)), "");

                GUI.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.9f);
                GUI.Box(new Rect(x, y, barWidth, barHeight), "");

                float fillRatio = Mathf.Clamp01(_animatedHp / maxHp);
                if (fillRatio > 0f)
                {
                    Color jasnaCzerwien = new Color(1.0f, 0.25f, 0.2f, 1f);
                    Color ciemnaCzerwien = new Color(0.35f, 0.0f, 0.0f, 1f);

                    float filledWidth = barWidth * fillRatio;
                    int totalSlices = 80;
                    float sliceWidth = barWidth / totalSlices;

                    for (int i = 0; i < totalSlices; i++)
                    {
                        float sliceX = x + (i * sliceWidth);
                        if (sliceX >= x + filledWidth) break;

                        float currentSliceWidth = sliceWidth;
                        if (sliceX + sliceWidth > x + filledWidth)
                        {
                            currentSliceWidth = (x + filledWidth) - sliceX;
                        }

                        float t = (float)i / (totalSlices - 1);
                        float odlegloscOdSrodka = Mathf.Abs(t - 0.5f) * 2f;

                        GUI.backgroundColor = Color.Lerp(jasnaCzerwien, ciemnaCzerwien, odlegloscOdSrodka);
                        GUI.Box(new Rect(sliceX, y, currentSliceWidth, barHeight), "");
                    }
                }

                string bossName = BossHealthPatches.ActiveBossName.ToUpper();
                string hpText = $"{currentHp} / {maxHp}";
                int pct = Mathf.RoundToInt(((float)currentHp / maxHp) * 100f);
                string pctText = $"{pct}%";

                float shadow = 2f * scale;
                float padding = 15f * scale;

                Rect leftRect = new Rect(x + padding, y, barWidth, barHeight);
                Rect centerRect = new Rect(x, y, barWidth, barHeight);
                Rect rightRect = new Rect(x, y, barWidth - padding, barHeight);

                GUI.skin.label.fontStyle = FontStyle.Bold;
                GUI.skin.label.fontSize = (int)(20 * scale);

                GUI.skin.label.alignment = TextAnchor.MiddleLeft;
                GUI.contentColor = Color.black;
                GUI.Label(new Rect(leftRect.x + shadow, leftRect.y + shadow, leftRect.width, leftRect.height), bossName);
                GUI.contentColor = new Color(1f, 0.85f, 0f);
                GUI.Label(leftRect, bossName);

                GUI.skin.label.alignment = TextAnchor.MiddleRight;
                GUI.contentColor = Color.black;
                GUI.Label(new Rect(rightRect.x + shadow, rightRect.y + shadow, rightRect.width, rightRect.height), pctText);
                GUI.contentColor = Color.white;
                GUI.Label(rightRect, pctText);

                GUI.skin.label.alignment = TextAnchor.MiddleCenter;
                GUI.contentColor = Color.black;
                GUI.Label(new Rect(centerRect.x + shadow, centerRect.y + shadow, centerRect.width, centerRect.height), hpText);
                GUI.contentColor = Color.white;
                GUI.Label(centerRect, hpText);
            }
            catch (Exception ex)
            {
                BossHealthPlugin.PluginLogger.LogError($"[ONGUI ERROR] {ex}");
            }
            finally
            {
                GUI.backgroundColor = origBg;
                GUI.contentColor = origContent;
                GUI.color = origColor;
                GUI.skin.box.normal.background = origBoxBg;

                GUI.skin.label.alignment = origAlignment;
                GUI.skin.label.fontSize = origFontSize;
                GUI.skin.label.fontStyle = origFontStyle;
            }
        }
    }
}