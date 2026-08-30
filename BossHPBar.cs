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
                BossHealthPlugin.PluginLogger.LogError($"[UI INITIALIZATION ERROR] {ex.Message}");
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

                float barWidth = 600f;
                float barHeight = 24f;
                float border = 3f;

                float x = (Screen.width - barWidth) / 2f;
                float y = 35f;

                Color origBg = GUI.backgroundColor;
                Color origContent = GUI.contentColor;

                GUI.backgroundColor = Color.black;
                GUI.Box(new Rect(x - border, y - border, barWidth + (border * 2), barHeight + (border * 2)), "");

                GUI.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.9f);
                GUI.Box(new Rect(x, y, barWidth, barHeight), "");

                float fillRatio = Mathf.Clamp01(_animatedHp / maxHp);
                if (fillRatio > 0f)
                {
                    GUI.backgroundColor = new Color(0.85f, 0.15f, 0.15f, 1f);
                    GUI.Box(new Rect(x, y, barWidth * fillRatio, barHeight), "");
                }

                GUI.backgroundColor = origBg;

                string bossName = BossHealthPatches.ActiveBossName.ToUpper();

                GUI.contentColor = Color.black;
                GUI.Label(new Rect(x + 1, y - 24, barWidth, 22), bossName);
                // Nagłówek
                GUI.contentColor = new Color(1f, 0.85f, 0f);
                GUI.Label(new Rect(x, y - 25, barWidth, 22), bossName);

                int pct = Mathf.RoundToInt(((float)currentHp / maxHp) * 100f);
                string hpText = $"{currentHp} / {maxHp} ({pct}%)";

                GUI.contentColor = Color.black;
                GUI.Label(new Rect(x + 1, y + 2, barWidth, barHeight), hpText);
                GUI.contentColor = Color.white;
                GUI.Label(new Rect(x, y + 1, barWidth, barHeight), hpText);

                GUI.contentColor = origContent;
            }
            catch (Exception ex)
            {
                BossHealthPlugin.PluginLogger.LogError($"[ONGUI ERROR] {ex.Message}");
            }
        }
    }
}