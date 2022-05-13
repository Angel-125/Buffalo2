using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Buffalo2.Wrappers;

namespace Buffalo2
{
    /// <summary>
    /// A small helper class to update a part's resources when a part variant is applied.
    /// </summary>
    public class ModuleResourceVariants : PartModule
    {
        #region Constants
        const string kResourceName = "resourceName";
        const string kAmount = "amount";
        const string kMaxAmount = "maxAmount";
        const string kPackedVolumeLimit = "packedVolumeLimit";
        #endregion

        #region Fields
        #endregion

        #region Housekeeping
        WBIOmniStorageWrapper omniStorage;
        #endregion

        #region Overrides
        public void OnDestroy()
        {
            GameEvents.onVariantApplied.Remove(onVariantApplied);
        }

        public override void OnAwake()
        {
            GameEvents.onVariantApplied.Add(onVariantApplied);
            omniStorage = WBIOmniStorageWrapper.GetOmniStorage(part);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            omniStorage = WBIOmniStorageWrapper.GetOmniStorage(part);
        }
        #endregion

        #region Helpers
        private void onVariantApplied(Part variantPart, PartVariant variant)
        {
            if (variantPart != part)
                return;

            string resourceName = variant.GetExtraInfoValue(kResourceName);
            string amtStr = variant.GetExtraInfoValue(kAmount);
            string maxAmtStr = variant.GetExtraInfoValue(kMaxAmount);
            string packedVolumeLimitStr = variant.GetExtraInfoValue(kPackedVolumeLimit);

            updateResource(resourceName, amtStr, maxAmtStr);
            updateCargoCapacity(packedVolumeLimitStr);

            //Dirty the GUI
            MonoUtilities.RefreshContextWindows(this.part);
        }

        private void updateCargoCapacity(string packedVolumeLimitStr)
        {
            float packedVolumeLimit = 0;
            ModuleInventoryPart inventory = null;

            if (string.IsNullOrEmpty(packedVolumeLimitStr) || !float.TryParse(packedVolumeLimitStr, out packedVolumeLimit))
                return;

            inventory = part.FindModuleImplementing<ModuleInventoryPart>();
            if (inventory == null || !inventory.HasPackedVolumeLimit)
                return;

            inventory.packedVolumeLimit = packedVolumeLimit;

            updateOmniStorage(packedVolumeLimit);
        }

        private void updateOmniStorage(float storageVolume)
        {
            if (omniStorage == null)
                return;

            omniStorage.UpdateStorageVolume(storageVolume);
        }

        private void updateResource(string resourceName, string amtStr, string maxAmtStr)
        {
            double amount = 0;
            double maxAmount = 0;

            if (string.IsNullOrEmpty(resourceName) || !part.Resources.Contains(resourceName))
                return;

            if (HighLogic.LoadedSceneIsEditor && !string.IsNullOrEmpty(amtStr) & double.TryParse(amtStr, out amount))
                part.Resources[resourceName].amount = amount;

            if (!string.IsNullOrEmpty(maxAmtStr) & double.TryParse(maxAmtStr, out maxAmount))
            {
                part.Resources[resourceName].maxAmount = maxAmount;

                if ((part.Resources[resourceName].amount > part.Resources[resourceName].maxAmount) || HighLogic.LoadedSceneIsEditor)
                    part.Resources[resourceName].amount = part.Resources[resourceName].maxAmount;
            }

            MonoUtilities.RefreshContextWindows(part);
            GameEvents.onPartResourceListChange.Fire(part);
        }
        #endregion
    }
}

