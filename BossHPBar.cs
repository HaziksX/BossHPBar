using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using P2.GameSystem;
using P2.GameSystem.Actor;
using P2.GameSystem.Actor.Status;
using UnityEngine;

namespace BossHPBar
{
    [BepInPlugin("com.haziksx.bosshpbar", "BossHPBar", "0.1.0")]
    public class BossHealthPlugin : BasePlugin
    {
        public static ManualLogSource PluginLogger;

        public override void Load()
        {
            PluginLogger = Log;
            ClassInjector.RegisterTypeInIl2Cpp<BossHealthUIController>();
            Harmony.CreateAndPatchAll(typeof(BossHealthPatches));

            PluginLogger.LogInfo("[Boss Health Bar] Mod załadowany pomyślnie.");
        }
    }

    public static class BossHealthPatches
    {
        public static ActorObj ActiveBossActor = null;
        public static string ActiveBossName = "BOSS";
        private static bool _uiInitialized = false;

        private static readonly Dictionary<string, string> BossIds = new Dictionary<string, string>
        {
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
                // Zmieniono na {ex}, by wyświetlało pełny stack trace
                BossHealthPlugin.PluginLogger.LogError($"[UI INITIALIZATION ERROR] {ex}");
            }
        }

        [HarmonyPatch(typeof(ActorObj), "createActorStatus")]
        [HarmonyPostfix]
        public static void PostSpawnBossHook(string pArcResourceNode, ActorObj __instance)
        {
            EnsureUIExists();
            if (string.IsNullOrEmpty(pArcResourceNode)) return;

            foreach (var kvp in BossIds)
            {
                if (pArcResourceNode.Contains(kvp.Key))
                {
                    ActiveBossName = kvp.Value;
                    ActiveBossActor = __instance;
                    break;
                }
            }
        }

        [HarmonyPatch(typeof(ActorObj), "update")]
        [HarmonyPostfix]
        public static void PostActorUpdateHook(ActorObj __instance)
        {
            EnsureUIExists();
        }
    }

    public class BossHealthUIController : MonoBehaviour
    {
        public BossHealthUIController(IntPtr handle) : base(handle) { }

        private float _animatedHp = -1f;
        private ActorObj _lastBoss = null;

        public void OnGUI()
        {
            var boss = BossHealthPatches.ActiveBossActor;
            if (boss == null || boss.Pointer == IntPtr.Zero) return;

            // --- ZAPISYWANIE ORYGINALNYCH USTAWIEŃ GUI ---
            Color origBg = GUI.backgroundColor;
            Color origContent = GUI.contentColor;
            Color origColor = GUI.color;
            Texture2D origBoxBg = GUI.skin.box.normal.background;

            TextAnchor origAlignment = GUI.skin.label.alignment;
            int origFontSize = GUI.skin.label.fontSize;
            FontStyle origFontStyle = GUI.skin.label.fontStyle;

            try
            {
                ActorStatusObj statusObj = boss.pActorStatus_;
                if (statusObj == null || statusObj.Pointer == IntPtr.Zero) return;

                GameStatus bossStats = statusObj.TryCast<GameStatus>();
                if (bossStats == null) return;

                int currentHp = bossStats.getHitPoint();
                int maxHp = bossStats.getMaxHitPoint();

                if (currentHp <= 0 || maxHp <= 0) return;

                if (_lastBoss != boss)
                {
                    _animatedHp = currentHp;
                    _lastBoss = boss;
                }

                _animatedHp = Mathf.Lerp(_animatedHp, currentHp, Time.deltaTime * 6f);

                // --- SKALOWANIE INTERFEJSU ---
                float scale = Screen.height / 1080f;

                // Trochę węższy (było 850, jest 700)
                float barWidth = 700f * scale;
                float barHeight = 32f * scale; // Lekko grubszy, by dobrze pomieścić wszystkie teksty w środku
                float border = 3f * scale;

                float x = (Screen.width - barWidth) / 2f;
                // Pasek wyżej (było 100, jest 55)
                float y = 55f * scale;

                GUI.skin.box.normal.background = Texture2D.whiteTexture;

                // 1. Biała obwódka
                GUI.backgroundColor = Color.white;
                GUI.Box(new Rect(x - border, y - border, barWidth + (border * 2), barHeight + (border * 2)), "");

                // 2. Ciemne tło paska
                GUI.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.9f);
                GUI.Box(new Rect(x, y, barWidth, barHeight), "");

                // 3. Czerwony pasek z 3-stopniowym gradientem
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

                // --- TEKSTY W ŚRODKU PASKA ---
                string bossName = BossHealthPatches.ActiveBossName.ToUpper();
                string hpText = $"{currentHp} / {maxHp}";
                int pct = Mathf.RoundToInt(((float)currentHp / maxHp) * 100f);
                string pctText = $"{pct}%";

                float shadow = 2f * scale;
                float padding = 15f * scale; // Odstęp od krawędzi paska dla nazwy i procentów

                // Definiujemy obszary dla tekstów - wszystkie mają wysokość "barHeight" żeby ładnie wyśrodkowały się w pionie
                Rect leftRect = new Rect(x + padding, y, barWidth, barHeight);
                Rect centerRect = new Rect(x, y, barWidth, barHeight);
                Rect rightRect = new Rect(x, y, barWidth - padding, barHeight);

                GUI.skin.label.fontStyle = FontStyle.Bold;
                GUI.skin.label.fontSize = (int)(20 * scale); // Jednakowy rozmiar fontu dla wszystkiego

                // NAZWA (Złota, Lewa strona w środku paska)
                GUI.skin.label.alignment = TextAnchor.MiddleLeft;

                GUI.contentColor = Color.black; // Cień
                GUI.Label(new Rect(leftRect.x + shadow, leftRect.y + shadow, leftRect.width, leftRect.height), bossName);
                GUI.contentColor = new Color(1f, 0.85f, 0f); // Złoty
                GUI.Label(leftRect, bossName);

                // PROCENTY (Białe, Prawa strona w środku paska)
                GUI.skin.label.alignment = TextAnchor.MiddleRight;

                GUI.contentColor = Color.black; // Cień
                GUI.Label(new Rect(rightRect.x + shadow, rightRect.y + shadow, rightRect.width, rightRect.height), pctText);
                GUI.contentColor = Color.white; // Biały
                GUI.Label(rightRect, pctText);

                // HP (Białe, Środek paska)
                GUI.skin.label.alignment = TextAnchor.MiddleCenter;

                GUI.contentColor = Color.black; // Cień
                GUI.Label(new Rect(centerRect.x + shadow, centerRect.y + shadow, centerRect.width, centerRect.height), hpText);
                GUI.contentColor = Color.white; // Biały
                GUI.Label(centerRect, hpText);
            }
            catch (Exception ex)
            {
                BossHealthPlugin.PluginLogger.LogError($"[ONGUI ERROR] {ex}");
            }
            finally
            {
                // --- PRZYWRACANIE ORYGINALNYCH USTAWIEŃ GUI ---
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