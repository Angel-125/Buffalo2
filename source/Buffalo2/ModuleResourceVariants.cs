using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

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
        #endregion

        #region Fields
        #endregion

        #region Overrides
        public void OnDestroy()
        {
            GameEvents.onVariantApplied.Remove(onVariantApplied);
        }

        public override void OnAwake()
        {
            GameEvents.onVariantApplied.Add(onVariantApplied);
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

            updateResource(resourceName, amtStr, maxAmtStr);
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

