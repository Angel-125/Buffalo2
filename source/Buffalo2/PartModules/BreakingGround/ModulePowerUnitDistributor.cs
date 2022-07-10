using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using WildBlueCore;

namespace Buffalo2
{
    /// <summary>
    /// Manages power unit to electric charge distribution.
    /// Add this module to parts with a ModuleGroundExpControl (Probodobodyne Experiment Control Station is one example)
    /// </summary>
    public class ModulePowerUnitDistributor: BasePartModule
    {
        #region Housekeeping
        int prevLoadedVesselCount = -1;
        ModuleGroundExpControl groundExpControl = null;
        List<ModuleGroundExpControl> groundControllers = null;
        Dictionary<Vessel, List<ModulePowerUnitConverter>> vesselPowerConverters = null;
        int originalPowerProduced = -1;
        int originalPowerActualProduced = -1;
        int prevTotalPowerAvailable = -1;
        #endregion

        #region Overrides
        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            if (!HighLogic.LoadedSceneIsFlight)
                return;

            groundExpControl = part.FindModuleImplementing<ModuleGroundExpControl>();
            if (groundExpControl != null)
            {
                findGroundControllersAndConverters();

                // Get the original power produced by the control unit.
                ConfigNode node = getPartConfigNode("ModuleGroundExpControl");
                if (node != null && node.HasValue("powerUnitsProduced"))
                    int.TryParse(node.GetValue("powerUnitsProduced"), out originalPowerProduced);

                originalPowerActualProduced = groundExpControl.ActualPowerUnitsProduced;
            }
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || groundExpControl == null || groundControllers == null || groundControllers.Count == 0)
                return;
            if (groundExpControl.ScienceClusterData == null || !groundExpControl.ScienceClusterData.ControllerPartEnabled)
                return;

            // Update our list of controllers and converters
            findGroundControllersAndConverters();

            // If we're not the first controller in the list then we're done.
            if (groundControllers[0] != groundExpControl)
            {
                // Reset power produced if needed.
                if (groundExpControl.PowerUnitsProduced != originalPowerProduced)
                {
                    groundExpControl.PowerUnitsProduced = originalPowerProduced;
                    groundExpControl.ActualPowerUnitsProduced = originalPowerActualProduced;
                    groundExpControl.ScienceClusterData.UpdatePowerState();
                    GameEvents.onGroundScienceClusterUpdated.Fire(groundExpControl, groundExpControl.ScienceClusterData);
                }

                return;
            }

            // Update our power produced
            updatePowerProduced();

            // Distribute available power to the converters.
            distributeAvailablePower();
        }
        #endregion

        #region Helpers
        void updatePowerProduced()
        {
            Vessel[] coverterVessels = vesselPowerConverters.Keys.ToArray();
            if (coverterVessels.Length <= 0)
                return;

            Vessel converterVessel = null;
            List<ModulePowerUnitConverter> converters = null;
            int converterCount;
            ModulePowerUnitConverter converter = null;
            int totalPowerAvailable = originalPowerProduced;
            for (int index = 0; index < coverterVessels.Length; index++)
            {
                converterVessel = coverterVessels[index];
                converters = vesselPowerConverters[converterVessel];
                converterCount = converters.Count;
                for (int converterIndex = 0; converterIndex < converterCount; converterIndex++)
                {
                    converter = converters[converterIndex];
                    if (!converter.CanDistributeEC)
                        continue;

                    totalPowerAvailable += converter.GetPowerAvailable(converterCount, TimeWarp.fixedDeltaTime);
                }
            }

            // Reset power units produced if needed.
            if (totalPowerAvailable <= 0 && groundExpControl.PowerUnitsProduced != originalPowerProduced)
            {
                groundExpControl.PowerUnitsProduced = originalPowerProduced;
                groundExpControl.ActualPowerUnitsProduced = originalPowerActualProduced;
                groundExpControl.ScienceClusterData.UpdatePowerState();
                GameEvents.onGroundScienceClusterUpdated.Fire(groundExpControl, groundExpControl.ScienceClusterData);
            }
            else if (prevTotalPowerAvailable != totalPowerAvailable)
            {
                prevTotalPowerAvailable = totalPowerAvailable;
                groundExpControl.PowerUnitsProduced = totalPowerAvailable;
                groundExpControl.ActualPowerUnitsProduced = totalPowerAvailable;
                groundExpControl.ScienceClusterData.UpdatePowerState();
                GameEvents.onGroundScienceClusterUpdated.Fire(groundExpControl, groundExpControl.ScienceClusterData);
            }
        }

        void distributeAvailablePower()
        {
            if (groundExpControl.ScienceClusterData.PowerAvailable <= 0)
                return;

            Vessel[] coverterVessels = vesselPowerConverters.Keys.ToArray();
            if (coverterVessels.Length <= 0)
                return;

            // Calculate the power that each vessel gets
            float powerPerVessel = groundExpControl.ScienceClusterData.PowerAvailable / coverterVessels.Length;

            // Distribute the power to each vessel
            Vessel converterVessel = null;
            List<ModulePowerUnitConverter> converters = null;
            int converterCount;
            ModulePowerUnitConverter converter = null;
            ModulePowerUnitConverter bestConverter = null;
            for (int index = 0; index < coverterVessels.Length; index++)
            {
                converterVessel = coverterVessels[index];
                converters = vesselPowerConverters[converterVessel];
                bestConverter = null;

                // Find the best converter that is enabled and is consuming power.
                converterCount = converters.Count;
                for (int converterIndex = 0; converterIndex < converterCount; converterIndex++)
                {
                    converter = converters[converterIndex];
                    if (!converter.CanConsumeEC)
                        continue;

                    if (bestConverter == null)
                        bestConverter = converter;
                    else if (converter.ecPerPowerUnit > bestConverter.ecPerPowerUnit)
                        bestConverter = converter;
                }
                if (bestConverter == null)
                    continue;

                // Distribute the available power
                bestConverter.DistributePower(powerPerVessel);
            }
        }

        void findGroundControllersAndConverters()
        {
            int count = FlightGlobals.VesselsLoaded.Count;
            if (count == prevLoadedVesselCount)
                return;
            prevLoadedVesselCount = count;
            groundControllers = new List<ModuleGroundExpControl>();
            vesselPowerConverters = new Dictionary<Vessel, List<ModulePowerUnitConverter>>();

            Vessel loadedVessel = null;
            ModuleGroundExpControl groundControl = null;
            List<ModulePowerUnitConverter> powerUnitConverters = null;
            for (int index = 0; index < count; index++)
            {
                loadedVessel = FlightGlobals.VesselsLoaded[index];
                if (loadedVessel.isEVA)
                    continue;

                // Check distance to vessel
                float distance = Vector3.Distance(part.transform.position, loadedVessel.transform.position);
                if (distance > groundExpControl.controlUnitRange)
                    continue;

                // Find a ground controller and add it to the list.
                groundControl = loadedVessel.FindPartModuleImplementing<ModuleGroundExpControl>();
                if (groundControl != null)
                    groundControllers.Add(groundControl);

                // If the vessel has power converters then add them to the map.
                powerUnitConverters = loadedVessel.FindPartModulesImplementing<ModulePowerUnitConverter>();
                if (powerUnitConverters != null && powerUnitConverters.Count > 0 && !vesselPowerConverters.ContainsKey(loadedVessel))
                    vesselPowerConverters.Add(loadedVessel, powerUnitConverters);
            }
        }
        #endregion
    }
}
