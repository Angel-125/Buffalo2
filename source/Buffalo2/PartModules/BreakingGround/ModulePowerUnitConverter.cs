using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;
using KSP.UI.Screens;
using KSP.Localization;
using WildBlueCore;

namespace Buffalo2
{
    /// <summary>
    /// This module converts Power Units from Breaking Ground Science to Electric Charge and vice-versa.
    /// </summary>
    public class ModulePowerUnitConverter: BasePartModule
    {
        #region Constants
        private const double secondsPerCycle = 1800;
        #endregion

        #region Fields
        /// <summary>
        /// Indicates whether or not the converter is running.
        /// </summary>
        [KSPField(isPersistant = true, guiName = "#LOC_BUFFALO_coverterStatus", guiActive = true, guiActiveEditor = true, guiActiveUnfocused = true, unfocusedRange = 5.0f)]
        [UI_Toggle(enabledText = "#LOC_BUFFALO_coverterOn", disabledText = "#LOC_BUFFALO_coverterOff")]
        public bool isActive = true;

        /// <summary>
        /// Indicates whether or not the converter is consuming (true) or sharing (false).
        /// </summary>
        [KSPField(isPersistant = true, guiName = "#LOC_BUFFALO_coverterMode", guiActive = true, guiActiveEditor = true, guiActiveUnfocused = true, unfocusedRange = 5.0f)]
        [UI_Toggle(enabledText = "#LOC_BUFFALO_coverterModeConsumer", disabledText = "#LOC_BUFFALO_coverterModeDistributor")]
        public bool isConsuming = true;

        /// <summary>
        /// The maximum number of Power Units that the part may produce. This value ranges between 1 and maxPowerUnitsProduced.
        /// </summary>
        [KSPField(isPersistant = true, guiName = "#LOC_BUFFALO_converterMaxPowerUnits", guiActive = true, guiActiveEditor = true)]
        [UI_FloatRange(stepIncrement = 1f, maxValue = 4f, minValue = 1f)]
        public float maxPowerAvailable = 4f;

        /// <summary>
        /// In Breaking Ground Science, Power Unit is an integer, but resources like ElectricCharge use decimals. The default is 0.25, so 1.0 EC = 4 PU.
        /// This number was derived by comparing the size of the Breaking Ground Mini-NUK-PB RTG to the stock PB-NUK RTG,
        /// and looking how how much ElectricCharge the stock RTG produces. That actually gives us 0.375 (the Mini-NUK is about half as tall as the stock RTG),
        /// but we dropped that to 0.25 to make the math easier.
        /// </summary>
        [KSPField]
        public double ecPerPowerUnit = 0.25;

        /// <summary>
        /// The maximum number of Power Units that the converter can provide. Note that this is an integer value. The default is 10.
        /// Multiply by ecPerPowerUnit to calculate how much ElectricCharge/sec that the power converter will consume.
        /// If you leave focus on the vessel and come back, then the E.C. will be drained accordingly.
        /// </summary>
        [KSPField]
        public int maxPowerUnitsProduced = 10;

        /// <summary>
        /// Timestamp of the last time the module was updated.
        /// </summary>
        [KSPField(isPersistant = true)]
        protected double lastUpdated = -1f;
        #endregion

        #region Housekeeping
        PartResourceDefinition definition = null;
        double elapsedTime = -1f;
        int totalDistributors = 0;
        UI_FloatRange maxPowerAvailableField = null;
        #endregion

        #region Overrides
        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            Fields.TryGetFieldUIControl("maxPowerAvailable", out maxPowerAvailableField);
            if (maxPowerAvailableField != null)
                maxPowerAvailableField.maxValue = maxPowerUnitsProduced;

            if (!HighLogic.LoadedSceneIsFlight)
                return;

            PartResourceDefinitionList definitions = PartResourceLibrary.Instance.resourceDefinitions;
            definition = definitions["ElectricCharge"];

            // Run catchup mechanic to drain EC.
            if (lastUpdated <= 0 || !CanDistributeEC)
                return;

            elapsedTime = Planetarium.GetUniversalTime() - lastUpdated;
            if (elapsedTime < TimeWarp.fixedDeltaTime)
                return;

            // Get numb of converters that are distributing.
            List<ModulePowerUnitConverter> vesselConverters = part.vessel.FindPartModulesImplementing<ModulePowerUnitConverter>();
            totalDistributors = 0;
            for (int index = 0; index < vesselConverters.Count; index++)
            {
                if (vesselConverters[index].CanDistributeEC)
                    totalDistributors += 1;
            }           
        }

        public override string GetInfo()
        {
            return Localizer.Format("#LOC_BUFFALO_converterModuleInfo");
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            if (!HighLogic.LoadedSceneIsFlight)
                return;

            if (!isActive || !isConsuming)
                lastUpdated = 0;

            bool guiActive = Fields["maxPowerAvailable"].guiActive;
            if (guiActive && isConsuming)
            {
                Fields["maxPowerAvailable"].guiActive = false;
                Fields["maxPowerAvailable"].guiActiveEditor = false;
            }
            else if (!guiActive && !isConsuming)
            {
                Fields["maxPowerAvailable"].guiActive = true;
                Fields["maxPowerAvailable"].guiActiveEditor = true;
            }
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;

            // Catchup mechanic
            if (elapsedTime <= 0)
                return;

            if (elapsedTime >= secondsPerCycle)
            {
                elapsedTime -= secondsPerCycle;
                int powerAvailable = GetPowerAvailable(totalDistributors, secondsPerCycle);
                if (powerAvailable <= 0)
                {
                    elapsedTime = 0f;
                }
            }
            else
            {
                GetPowerAvailable(totalDistributors, elapsedTime);
                elapsedTime = 0f;
            }
        }
        #endregion

        #region API
        public bool CanDistributeEC
        {
            get
            {
                return isActive && !isConsuming;
            }
        }

        public bool CanConsumeEC
        {
            get
            {
                return isActive && isConsuming;
            }
        }

        public int GetPowerAvailable(int totalConverterCount, double deltaTime)
        {
            // Get total EC in the vessel.
            double amount;
            double maxAmount;
            part.GetConnectedResourceTotals(definition.id, out amount, out maxAmount);

            // Calculate EC per converter.
            double amountPerConverter = amount / totalConverterCount;

            // Divide by ecPerPowerUnit to get the power units produced.
            int powerProduced = (int)(amountPerConverter / ecPerPowerUnit);

            // If the result is > maxPowerAvailable, then use maxPowerAvailable instead.
            int maxPower = (int)maxPowerAvailable;
            if (powerProduced > maxPower)
                powerProduced = maxPower;

            // Calculate the EC to consume.
            double ecToConsume = powerProduced * ecPerPowerUnit * deltaTime;

            // Make sure we have enough
            if (ecToConsume > amount)
                return 0;

            // Consume the EC.
            part.RequestResource(definition.id, ecToConsume);
            lastUpdated = Planetarium.GetUniversalTime();

            // Return the power points produced.
            return powerProduced;
        }

        public void DistributePower(float availablePower)
        {
            double ecToDistribute = availablePower * ecPerPowerUnit * TimeWarp.fixedDeltaTime;

            part.RequestResource("ElectricCharge", -ecToDistribute, ResourceFlowMode.ALL_VESSEL);
        }
        #endregion

        #region Helpers
        #endregion
    }
}
