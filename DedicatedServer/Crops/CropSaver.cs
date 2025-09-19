﻿using DedicatedServer.Config;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.TerrainFeatures;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DedicatedServer.Crops
{
    public class CropSaver
    {
        private IModHelper helper;
        private IMonitor monitor;
        private ModConfig config;
        private SerializableDictionary<CropLocation, CropData> cropDictionary = new SerializableDictionary<CropLocation, CropData>();
        private SerializableDictionary<CropLocation, CropComparisonData> beginningOfDayCrops = new SerializableDictionary<CropLocation, CropComparisonData>();

        public class CropSaveData
        {
            public SerializableDictionary<CropLocation, CropData> cropDictionary { get; set; } = new SerializableDictionary<CropLocation, CropData>();
            public SerializableDictionary<CropLocation, CropComparisonData> beginningOfDayCrops { get; set; } = new SerializableDictionary<CropLocation, CropComparisonData>();
        }

        public struct CropLocation
        {
            public string LocationName { get; set; }
            public int TileX { get; set; }
            public int TileY { get; set; }
        }

        public struct CropGrowthStage
        {
            public int CurrentPhase { get; set; }
            public int DayOfCurrentPhase { get; set; }
            public bool FullyGrown { get; set; }
            public List<int> PhaseDays { get; set; }
            public int OriginalRegrowAfterHarvest { get; set; }
        }

        public struct CropComparisonData
        {
            public CropGrowthStage CropGrowthStage { get; set; }
            public int RowInSpriteSheet { get; set; }
            public bool Dead { get; set; }
            public bool ForageCrop { get; set; }
            public int WhichForageCrop { get; set; }
        }

        public struct CropData
        {
            public bool MarkedForDeath { get; set; }
            public List<string> OriginalSeasonsToGrowIn { get; set; }
            public bool HasExistedInIncompatibleSeason { get; set; }
            public int OriginalRegrowAfterHarvest { get; set; }
            public bool HarvestableLastNight { get; set; }
        }

        public CropSaver(IModHelper helper, IMonitor monitor, ModConfig config)
        {
            this.helper = helper;
            this.monitor = monitor;
            this.config = config;
        }

        public void Enable()
        {
            helper.Events.GameLoop.DayStarted += onDayStarted;
            helper.Events.GameLoop.DayEnding += onDayEnding;
            helper.Events.GameLoop.Saving += onSaving;
            helper.Events.GameLoop.SaveLoaded += onLoaded;
        }

        private void onLoaded(object sender, StardewModdingAPI.Events.SaveLoadedEventArgs e)
        {
            /**
             * Loads the cropDictionary and beginningOfDayCrops using SMAPI's data API.
             */
            try
            {
                CropSaveData cropSaveData = helper.Data.ReadSaveData<CropSaveData>("AdditionalCropData") ?? new CropSaveData
                {
                    cropDictionary = new SerializableDictionary<CropLocation, CropData>(),
                    beginningOfDayCrops = new SerializableDictionary<CropLocation, CropComparisonData>()
                };
                
                beginningOfDayCrops = cropSaveData.beginningOfDayCrops;
                cropDictionary = cropSaveData.cropDictionary;
            }
            catch (Exception ex)
            {
                monitor.Log($"Error loading crop data: {ex.Message}", LogLevel.Warn);
                // Initialize empty dictionaries if loading fails
                beginningOfDayCrops = new SerializableDictionary<CropLocation, CropComparisonData>();
                cropDictionary = new SerializableDictionary<CropLocation, CropData>();
            }
        }

        private void onSaving(object sender, StardewModdingAPI.Events.SavingEventArgs e)
        {
            /**
             * Saves the cropDictionary and beginningOfDayCrops using SMAPI's data API.
             * In most cases, the day is started immediately after loading, which in-turn 
             * clears beginningOfDayCrops. However, in case some other mod is installed 
             * which allows mid-day saving and loading, it's a good idea to save both 
             * dictionaries anyways.
             */
            try
            {
                var cropSaveData = new CropSaveData 
                { 
                    cropDictionary = cropDictionary, 
                    beginningOfDayCrops = beginningOfDayCrops 
                };
                
                helper.Data.WriteSaveData("AdditionalCropData", cropSaveData);
            }
            catch (Exception ex)
            {
                monitor.Log($"Error saving crop data: {ex.Message}", LogLevel.Error);
            }
        }

        private static bool sameCrop(CropComparisonData first, CropComparisonData second)
        {
            // Two crops are considered "different" if they have different sprite sheet rows (i.e., they're
            // different crop types); one of them is dead while the other is alive; one is a forage crop
            // while the other is not; the two crops are different types of forage crops; their phases
            // of growth are different; or their current days of growth are different, except when
            // one of them is harvestable and the other is fully grown and harvested. A crop is considered
            // harvestable when it's in the last stage of growth, and its either set to not "FullyGrown", or
            // its day of current phase is less than or equal to zero (after the first harvest, its day of
            // current phase works downward). A crop is considered harvested when it's in the final phase
            // and the above sub-conditions aren't satisfied (it's set to FullyGrown and its day of current
            // phase is positive)
            var differentSprites = first.RowInSpriteSheet != second.RowInSpriteSheet;
            
            var differentDeads = first.Dead != second.Dead;
            
            var differentForages = first.ForageCrop != second.ForageCrop;
            
            var differentForageTypes = first.WhichForageCrop != second.WhichForageCrop;
            
            var differentPhases = first.CropGrowthStage.CurrentPhase != second.CropGrowthStage.CurrentPhase;

            var differentDays = first.CropGrowthStage.DayOfCurrentPhase != second.CropGrowthStage.DayOfCurrentPhase;
            var firstGrown = first.CropGrowthStage.CurrentPhase >= first.CropGrowthStage.PhaseDays.Count - 1;
            var secondGrown = second.CropGrowthStage.CurrentPhase >= second.CropGrowthStage.PhaseDays.Count - 1;
            var firstHarvestable = firstGrown && (first.CropGrowthStage.DayOfCurrentPhase <= 0 || !first.CropGrowthStage.FullyGrown);
            var secondHarvestable = secondGrown && (second.CropGrowthStage.DayOfCurrentPhase <= 0 || !second.CropGrowthStage.FullyGrown);
            var firstRegrown = firstGrown && !firstHarvestable;
            var secondRegrown = secondGrown && !secondHarvestable;
            var harvestableAndRegrown = (firstHarvestable && secondRegrown) || (firstRegrown && secondHarvestable);
            var differentMeaningfulDays = differentDays && !harvestableAndRegrown;

            return !differentSprites && !differentDeads && !differentForages && !differentForageTypes && !differentPhases && !differentMeaningfulDays;
        }

        private void onDayEnding(object sender, StardewModdingAPI.Events.DayEndingEventArgs e)
        {
            // In order to check for crops that have been destroyed and need to be removed from
            // the cropDictionary all together, we need to keep track of which crop locations
            // from the cropDictionary are found during the iteration over all crops in all
            // locations. Any which are not found must no longer exist (and have not been
            // replaced) and can be removed.
            var locationSet = new HashSet<CropLocation>();
            foreach (var location in Game1.locations)
            {
                if (location.IsOutdoors && !location.SeedsIgnoreSeasonsHere() && !(location is IslandLocation))
                {
                    // Found an outdoor location where seeds don't ignore seasons. Find all the
                    // crops here to cache necessary data for protecting them.
                    foreach (var pair in location.terrainFeatures.Pairs)
                    {
                        var tileLocation = pair.Key;
                        var terrainFeature = pair.Value;
                        if (terrainFeature is HoeDirt)
                        {
                            var hoeDirt = terrainFeature as HoeDirt;
                            var crop = hoeDirt.crop;
                            if (crop != null)
                            {
                                // Found a crop. Construct a CropLocation key
                                var cropLocation = new CropLocation
                                {
                                    LocationName = location.NameOrUniqueName,
                                    TileX = (int)tileLocation.X,
                                    TileY = (int)tileLocation.Y
                                };
                                
                                // Mark it as found via the locationSet, so we know not to remove
                                // the corresponding cropDictionary entry if one exists
                                locationSet.Add(cropLocation);

                                // Construct its growth stage so we can compare it to beginningOfDayCrops
                                // to see if it was newly-planted.
                                var cropGrowthStage = new CropGrowthStage
                                {
                                    CurrentPhase = crop.currentPhase.Value,
                                    DayOfCurrentPhase = crop.dayOfCurrentPhase.Value,
                                    FullyGrown = crop.fullyGrown.Value,
                                    PhaseDays = crop.phaseDays.ToList(),
                                    OriginalRegrowAfterHarvest = crop.GetData()?.RegrowDays ?? -1
                                };

                                var cropComparisonData = new CropComparisonData
                                {
                                    CropGrowthStage = cropGrowthStage,
                                    RowInSpriteSheet = crop.rowInSpriteSheet.Value,
                                    Dead = crop.dead.Value,
                                    ForageCrop = crop.forageCrop.Value,
                                    WhichForageCrop = int.TryParse(crop.whichForageCrop.Value, out int forageCrop) ? forageCrop : 0
                                };

                                // Determine if this crop was planted today or was pre-existing, based on whether
                                // or not it's different from the crop at this location at the beginning of the day.
                                if (!beginningOfDayCrops.ContainsKey(cropLocation) || !sameCrop(beginningOfDayCrops[cropLocation], cropComparisonData))
                                {
                                    // No crop was found at this location at the beginning of the day, or the comparison data
                                    // is different. Consider it a new crop, and add a new CropData for it in the cropDictionary.
                                    var cd = new CropData
                                    {
                                        MarkedForDeath = false,
                                        OriginalSeasonsToGrowIn = crop.GetData()?.Seasons?.Select(s => s.ToString().ToLower()).ToList() ?? new List<string>(),
                                        HasExistedInIncompatibleSeason = false,
                                        OriginalRegrowAfterHarvest = crop.GetData()?.RegrowDays ?? -1,
                                        HarvestableLastNight = false
                                    };
                                    cropDictionary[cropLocation] = cd;

                                    // TODO: In Stardew Valley 1.6+, crop seasons are read-only data from Data/Crops
                                    // and cannot be modified directly. The old logic here tried to make crops survive
                                    // in all seasons by adding all seasons to crop.seasonsToGrowIn.
                                    // This functionality may need to be implemented differently or crops may die
                                    // naturally when out of season (which might be acceptable behavior).
                                    // Original logic was:
                                    // - Add all seasons (spring, summer, fall, winter) to crop.seasonsToGrowIn
                                    // - This prevented crops from dying due to season changes
                                    // - Crops would only die when harvested completely or manually marked for death
                                }

                                // If there's a crop in the dictionary at this location (just planted today or otherwise),
                                // record whether it's harvestable tonight. This is used to help determine whether the crop
                                // should be marked for death the next morning. A crop is harvestable if and only if it's
                                // in the last phase, AND it's either a) NOT marked as "fully grown" (i.e., it hasn't been harvested
                                // at least once), or b) has a non-positive current day of phase (after harvest and regrowth,
                                // the current day of phase is set to positive and then works downward; 0 means ready-for-reharvest).
                                if (cropDictionary.TryGetValue(cropLocation, out var cropData))
                                {
                                    if ((crop.phaseDays.Count > 0 && crop.currentPhase.Value < crop.phaseDays.Count - 1) || (crop.dayOfCurrentPhase.Value > 0 && crop.fullyGrown.Value))
                                    {
                                        cropData.HarvestableLastNight = false;
                                    } else
                                    {
                                        cropData.HarvestableLastNight = true;
                                    }
                                    cropDictionary[cropLocation] = cropData;
                                }
                            }
                        }
                    }
                }
            }

            // Lastly, if there were any CropLocations in the cropDictionary that we DIDN'T see throughout the entire
            // iteration, then they must've been destroyed, AND they weren't replaced with a new crop at the same location.
            // In such a case, we can remove it from the cropDictionary.
            var locationSetComplement = new HashSet<CropLocation>();
            foreach (var kvp in cropDictionary)
            {
                if (!locationSet.Contains(kvp.Key))
                {
                    locationSetComplement.Add(kvp.Key);
                }
            }
            foreach (var cropLocation in locationSetComplement)
            {
                cropDictionary.Remove(cropLocation);
            }
        }

        private void onDayStarted(object sender, StardewModdingAPI.Events.DayStartedEventArgs e)
        {
            beginningOfDayCrops.Clear();
            foreach (var location in Game1.locations)
            {
                if (location.IsOutdoors && !location.SeedsIgnoreSeasonsHere() && location is not IslandLocation)
                {
                    // Found an outdoor location where seeds don't ignore seasons. Find all the
                    // crops here to cache necessary data for protecting them.
                    foreach (var pair in location.terrainFeatures.Pairs)
                    {
                        var tileLocation = pair.Key;
                        var terrainFeature = pair.Value;
                        if (terrainFeature is HoeDirt)
                        {
                            var hoeDirt = terrainFeature as HoeDirt;
                            var crop = hoeDirt.crop;
                            if (crop != null) {
                                // Found a crop. Construct a CropLocation key
                                var cropLocation = new CropLocation
                                {
                                    LocationName = location.NameOrUniqueName,
                                    TileX = (int) tileLocation.X,
                                    TileY = (int) tileLocation.Y
                                };

                                CropData cropData;
                                CropComparisonData cropComparisonData;
                                // Now, we have to update the properties of the CropData entry
                                // in the cropDictionary. Firstly, check if such a CropData entry exists
                                // (it won't exist for auto-spawned crops, like spring onion, since they'll
                                // never have passed the previous "newly planted test")
                                if (!cropDictionary.TryGetValue(cropLocation, out cropData))
                                {
                                    // The crop was not planted by the player. However, we do want to
                                    // record its comparison information so that we can check this evening
                                    // if it has changed, which would indicate that it HAS been replaced
                                    // by a player-planted crop.

                                    var cgs = new CropGrowthStage
                                    {
                                        CurrentPhase = crop.currentPhase.Value,
                                        DayOfCurrentPhase = crop.dayOfCurrentPhase.Value,
                                        FullyGrown = crop.fullyGrown.Value,
                                        PhaseDays = crop.phaseDays.ToList(),
                                        OriginalRegrowAfterHarvest = crop.GetData()?.RegrowDays ?? -1
                                    };

                                    cropComparisonData = new CropComparisonData
                                    {
                                        CropGrowthStage = cgs,
                                        RowInSpriteSheet = crop.rowInSpriteSheet.Value,
                                        Dead = crop.dead.Value,
                                        ForageCrop = crop.forageCrop.Value,
                                    WhichForageCrop = int.TryParse(crop.whichForageCrop.Value, out int forageCrop2) ? forageCrop2 : 0
                                };

                                    beginningOfDayCrops[cropLocation] = cropComparisonData;

                                    // Now move on to the next crop; we don't want to mess with this one.
                                    continue;
                                }

                                // As of last night, the crop at this location was considered to have been
                                // planted by the player. Let's hope that it hasn't somehow been replaced
                                // by an entirely different crop overnight; though that seems unlikely.

                                // Check if it's currently a season which is incompatible with the
                                // crop's ORIGINAL compatible seasons. If so, update the crop data to
                                // reflect this.
                                if (!cropData.OriginalSeasonsToGrowIn.Contains(Game1.currentSeason))
                                {
                                    cropData.HasExistedInIncompatibleSeason = true;
                                }

                                // Check if the crop has been out of season, AND it was not harvestable last night.
                                // If so, mark it for death. This covers the edge case of when a crop finishes
                                // growing on the first day in which it's out-of-season (it should be marked for
                                // death, in this case).

                                if (cropData.HasExistedInIncompatibleSeason && !cropData.HarvestableLastNight)
                                {
                                    cropData.MarkedForDeath = true;
                                }

                                // TODO: In Stardew Valley 1.6+, crop.regrowAfterHarvest is read-only data from Data/Crops
                                // and cannot be modified directly. The old logic here tried to set regrowAfterHarvest to -1
                                // to ensure the farmer only gets one more harvest from out-of-season crops.
                                // This functionality may need to be implemented differently, perhaps by:
                                // - Tracking regrow state in the mod's own data structures
                                // - Manually killing crops after harvest instead of relying on regrowAfterHarvest
                                // - Accepting that crops will regrow naturally according to their Data/Crops definition
                                // Original logic: if (cropData.HasExistedInIncompatibleSeason) crop.regrowAfterHarvest.Value = -1;

                                // And if the crop has been marked for death because it was planted too close to
                                // the turn of the season, then we should make sure it's killed.
                                if (cropData.MarkedForDeath)
                                {
                                    crop.Kill();
                                }

                                // Update the crop data in the crop dictionary
                                cropDictionary[cropLocation] = cropData;

                                // Lastly, now that the crop has been updated, construct the comparison data for later
                                // so that we can check if this has been replaced by a newly planted crop in the evening.

                                var cropGrowthStage = new CropGrowthStage
                                {
                                    CurrentPhase = crop.currentPhase.Value,
                                    DayOfCurrentPhase = crop.dayOfCurrentPhase.Value,
                                    FullyGrown = crop.fullyGrown.Value,
                                    PhaseDays = crop.phaseDays.ToList(),
                                    OriginalRegrowAfterHarvest = cropData.OriginalRegrowAfterHarvest
                                };
                                cropComparisonData = new CropComparisonData
                                {
                                    CropGrowthStage = cropGrowthStage,
                                    RowInSpriteSheet = crop.rowInSpriteSheet.Value,
                                    Dead = crop.dead.Value,
                                    ForageCrop = crop.forageCrop.Value,
                                    WhichForageCrop = int.TryParse(crop.whichForageCrop.Value, out int forageCrop3) ? forageCrop3 : 0
                                };

                                beginningOfDayCrops[cropLocation] = cropComparisonData;
                            }
                        }
                    }
                }
            }
        }
    }
}
