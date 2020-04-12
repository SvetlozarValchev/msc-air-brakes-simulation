using System.Reflection;
using MSCLoader;
using UnityEngine;
using Harmony;
using HutongGames.PlayMaker;

namespace AirBrakesSimulation
{
    public class AirBrakesSimulation : Mod
    {
        public override string ID => "AirBrakesSimulation"; //Your mod ID (unique)
        public override string Name => "Air Brakes Simulation"; //You mod name
        public override string Author => "cbethax"; //Your Username
        public override string Version => "1.0.0"; //Version

        public static Settings brakesApplyTime = new Settings("brakesApplyTime", "Brakes Apply Time (s)", 1.5f);
        public static Settings brakesReleaseTime = new Settings("brakesReleaseTime", "Brakes Release Time (s)", 0.2f);
        public static Settings brakesShowGUI = new Settings("brakesShowGUI", "Show GUI", false);

        public static GameObject guiBrake;
        public static GameObject guiBrakeBar;

        bool isSeated = false;

        public override void OnLoad()
        {
            var harmony = HarmonyInstance.Create("AirBrakesSimulation");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            InitGUI();
        }

        void InitGUI()
        {
            guiBrake = UnityEngine.GameObject.Instantiate<GameObject>(GameObject.Find("GUI/HUD/Hunger"));
            guiBrake.transform.SetParent(GameObject.Find("GUI/HUD").transform);
            guiBrake.name = "Brake Apply";
            guiBrake.transform.localPosition = new Vector3(-11.5f, 4.8f, 0.0f);

            TextMesh guiBrakeText = guiBrake.transform.FindChild("HUDLabel").GetComponent<TextMesh>();
            guiBrakeText.text = "Brake Apply";
            guiBrakeText.transform.FindChild("HUDLabelShadow").GetComponent<TextMesh>().text = "Brake Apply";

            guiBrakeBar = guiBrake.transform.FindChild("Pivot").gameObject;
            UnityEngine.Object.Destroy(guiBrakeBar.GetComponent<PlayMakerFSM>());
            guiBrakeBar.transform.localScale = new Vector3(0f, 1f, 1f);

            guiBrake.SetActive(false);
        }

        public override void ModSettings()
        {
            Settings.AddSlider(this, brakesApplyTime, 0.5f, 2.5f);
            Settings.AddSlider(this, brakesReleaseTime, 0.1f, 1f);
            Settings.AddCheckBox(this, brakesShowGUI);
        }

        public override void Update()
        {
            string playerCurrentVehicle = FsmVariables.GlobalVariables.FindFsmString("PlayerCurrentVehicle").Value;

            if (playerCurrentVehicle == "Gifu" && !isSeated)
            {
                isSeated = true;

                if ((bool)brakesShowGUI.GetValue())
                {
                    guiBrake.SetActive(true);
                }
            }
            else if (playerCurrentVehicle == "" && isSeated)
            {
                isSeated = false;
                guiBrake.SetActive(false);
            }
        }
    }

        [HarmonyPatch(typeof(CarController))]
    [HarmonyPatch("FixedUpdate")]
    class CarController_FixedUpdate_Patch
    {
        public static float gifuBrake = 0f;

        [HarmonyAfter(new string[] { "AdvancedKBMControls" })]
        static void Postfix(CarController __instance, Wheel[] ___allWheels, float ___veloKmh)
        {
            if (__instance.gameObject.name == "GIFU(750/450psi)")
            {
                float brakesReleaseTime = float.Parse(AirBrakesSimulation.brakesReleaseTime.GetValue().ToString());
                float brakesApplyTime = float.Parse(AirBrakesSimulation.brakesApplyTime.GetValue().ToString());

                if (__instance.brakeInput > 0.0f)
                {
                    if (__instance.brakeInput < gifuBrake)
                    {
                        gifuBrake -= Time.fixedDeltaTime / brakesReleaseTime;

                        gifuBrake = Mathf.Max(gifuBrake, __instance.brakeInput);
                    }
                    else
                    {
                        gifuBrake += Time.fixedDeltaTime / brakesApplyTime;

                        gifuBrake = Mathf.Min(gifuBrake, __instance.brakeInput);
                    }
                }
                else
                {
                    gifuBrake -= Time.fixedDeltaTime / brakesReleaseTime;
                }
                
                gifuBrake = Mathf.Clamp01(gifuBrake);
                __instance.brake = gifuBrake;

                AirBrakesSimulation.guiBrakeBar.transform.localScale = new Vector3(__instance.brake, 1f, 1f);

                foreach (Wheel allWheel in ___allWheels)
                {
                    if (!__instance.ABS || ___veloKmh <= __instance.ABSMinVelocity || __instance.brakeInput <= 0.0f)
                    {
                        allWheel.brake = __instance.brake;
                    }
                }
            }
        }
    }
}
