﻿using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using Harmony;

namespace RidgesideVillage
{
    public class ModEntry : Mod
    {
        internal static IMonitor ModMonitor { get; set; }
        internal new static IModHelper Helper { get; set; }
        internal static IJsonAssetsApi JsonAssetsAPI { get; set; }

        internal static ModConfig Config;

        private ConfigMenu ConfigMenu;
        private CustomCPTokens CustomCPTokens;

        public override void Entry(IModHelper helper)
        {
            ModMonitor = Monitor;
            Helper = helper;

            ConfigMenu = new ConfigMenu(this);
            CustomCPTokens = new CustomCPTokens(this);

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        }

        private void OnGameLaunched(object sender, EventArgs e)
        {

            Config = Helper.ReadConfig<ModConfig>();

            if (!Helper.ModRegistry.IsLoaded("spacechase0.JsonAssets"))
            {
                return;
            }
            JsonAssetsAPI = Helper.ModRegistry.GetApi<IJsonAssetsApi>("spacechase0.JsonAssets");
            var harmony = HarmonyInstance.Create("Rafseazz.RidgesideVillage");
            harmony.Patch(
                original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.getFish)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.GetFish_Postfix))
            );

            // Custom CP Token Set-up
            CustomCPTokens.RegisterTokens();

            // Generic Mod Config Menu setup
            ConfigMenu.RegisterMenu();
        }

        private void OnSaveLoaded(object sender, EventArgs ex)
        {
            try
            {
                Config = Helper.ReadConfig<ModConfig>();
            }
            catch (Exception e)
            {
                Monitor.Log($"Failed to load config settings. Will use default settings instead. Error: {e}", LogLevel.Debug);
                Config = new ModConfig();
            }
        }

        [HarmonyPostfix]
        public static void GetFish_Postfix(GameLocation __instance, float millisecondsAfterNibble, int bait, int waterDepth, Farmer who, double baitPotency, Vector2 bobberTile, string locationName, ref StardewValley.Object __result)
        {
            try
            {
                if (!__instance.IsUsingMagicBait(who))
                {
                    return;
                }
                string nameToUse = (locationName == null) ? __instance.Name : locationName;
                ModMonitor.Log($"Player {who.Name} using magic bait at {nameToUse} with original fish result is {__result.Name}", LogLevel.Trace);
                float bobberAddition = 0f;
                if (who != null && who.CurrentTool is StardewValley.Tools.FishingRod && (who.CurrentTool as StardewValley.Tools.FishingRod).getBobberAttachmentIndex() == 856) // Curiosity Lure increases chance by 7%
                {
                    bobberAddition += 0.07f;
                }
                List<string> fish_names = new List<string>();
                switch (nameToUse)
                {
                    // Custom locations are added to the game without their prefixes
                    case "RidgesideVillage":
                        fish_names.Add("Bladetail Sturgeon");
                        fish_names.Add("Harvester Trout");
                        fish_names.Add("Lullaby Carp");
                        fish_names.Add("Pebble Back Crab");
                        break;
                    case "Ridge":
                        fish_names.Add("Caped Tree Frog");
                        fish_names.Add("Fixer Eel");
                        fish_names.Add("Golden Rose Fin");
                        break;
                    case "Beach":
                        // Although Beach has it's own location class, it calls the base class getFish function, so we're okay to just postfix that.
                        fish_names.Add("Cardia Septal Jellyfish");
                        fish_names.Add("Crimson Spiked Clam");
                        fish_names.Add("Fairytale Lionfish");
                        break;
                    default:
                        return;
                }
                foreach (string fish in fish_names)
                {
                    int fish_id = JsonAssetsAPI.GetObjectId(fish);
                    // Currently this gives each fish a 20% chance to be caught, could be lower if we add more configuration
                    if (fish_id != -1 && !who.fishCaught.ContainsKey(fish_id) && who.FishingLevel >= 3 && Game1.random.NextDouble() < 0.2 + (double)bobberAddition)
                    {
                        ModMonitor.Log($"Fish {fish} (ID: {fish_id}) is caught: {who.fishCaught.ContainsKey(fish_id)}, setting fish result to this fish", LogLevel.Trace);
                        __result = new StardewValley.Object(fish_id, 1);
                        return;
                    }
                    else
                    {
                        ModMonitor.Log($"Fish {fish} (ID: {fish_id}) is caught: {who.fishCaught.ContainsKey(fish_id)}", LogLevel.Trace);
                    }
                }
                return;
            }
            catch (Exception ex)
            {
                ModMonitor.Log($"Failed in {nameof(GetFish_Postfix)}:\n{ex}", LogLevel.Error);
            }
        }
    }
}
