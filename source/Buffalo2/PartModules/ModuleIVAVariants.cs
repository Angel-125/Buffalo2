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
    /// This class works in conjunction with the stock ModulePartVariants. When the event onVariantApplied is received from the same part that has ModuleIVAVariants,
    /// and the name of the new variant matches the name of one of ModuleIVAVariants' VARIANT nodes, then the GAMEOBJECTS in the node will be enabled/disabled accordingly.
    /// The meshes must appear in the IVA meshes or in the depth mask.
    /// </summary>
    public class ModuleIVAVariants: BasePartModule
    {
        #region Constants
        const double waitDuration = 0.5;
        #endregion

        #region Fields
        [KSPField(isPersistant = true)]
        public string selectedVariant = string.Empty;
        #endregion

        #region Housekeeping
        double waitStartTime = 0;
        #endregion

        #region Overrides
        public void OnDestroy()
        {
            GameEvents.onVariantApplied.Remove(onVariantApplied);
            GameEvents.onCrewBoardVessel.Remove(onCrewBoardVessel);
            GameEvents.onCrewTransferred.Remove(onCrewTransferred);
        }

        public override void OnAwake()
        {
            GameEvents.onVariantApplied.Add(onVariantApplied);
            GameEvents.onCrewBoardVessel.Add(onCrewBoardVessel);
            GameEvents.onCrewTransferred.Add(onCrewTransferred);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (!string.IsNullOrEmpty(selectedVariant))
                applyVariant();
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            if (!HighLogic.LoadedSceneIsFlight || waitStartTime <= 0)
                return;

            double currentTime = Planetarium.GetUniversalTime();
            if (currentTime - waitStartTime >= waitDuration)
            {
                waitStartTime = 0;
                applyVariant();
            }
        }
        #endregion

        #region Helpers
        private void onCrewTransferred(GameEvents.HostedFromToAction<ProtoCrewMember, Part> data)
        {
            ProtoCrewMember astronaut = data.host;
            Part fromPart = data.from;
            Part toPart = data.to;
            if (!HighLogic.LoadedSceneIsFlight || toPart != part)
                return;
            waitStartTime = Planetarium.GetUniversalTime();
        }

        private void onCrewBoardVessel(GameEvents.FromToAction<Part, Part> data)
        {
            Part evaKerbal = data.from;
            Part boardedPart = data.to;
            if (!HighLogic.LoadedSceneIsFlight || boardedPart != part)
                return;
            waitStartTime = Planetarium.GetUniversalTime();
        }

        private void onVariantApplied(Part variantPart, PartVariant variant)
        {
            if (variantPart != part || !HighLogic.LoadedSceneIsFlight)
                return;

            selectedVariant = variant.Name;
            applyVariant();
        }

        private void applyVariant()
        {
            if (!HighLogic.LoadedSceneIsFlight || string.IsNullOrEmpty(selectedVariant))
                return;
            if (part.internalModel == null)
            {
                waitStartTime = Planetarium.GetUniversalTime();
                return;
            }
            ConfigNode node = getPartConfigNode();
            if (node == null)
                return;

            if (!node.HasNode("VARIANT"))
                return;
            ConfigNode[] nodes = node.GetNodes("VARIANT");
            node = findNode(selectedVariant, nodes);
            if (node == null)
                return;

            if (!node.HasNode("GAMEOBJECTS"))
                return;
            node = node.GetNode("GAMEOBJECTS");

            string[] objectNames = node.GetValues();
            string objectName = string.Empty;
            Transform transform = null;
            bool isEnabled = false;
            foreach (ConfigNode.Value value in node.values)
            {
                objectName = value.name;
                bool.TryParse(value.value, out isEnabled);
                transform = part.internalModel.FindModelTransform(objectName);
                if (transform != null)
                    transform.gameObject.SetActive(isEnabled);
            }
        }

        private ConfigNode findNode(string name, ConfigNode[] nodes)
        {
            for (int index = 0; index < nodes.Length; index++)
            {
                if (!nodes[index].HasValue("name"))
                    continue;
                if (nodes[index].GetValue("name") == name)
                    return nodes[index];
            }
            return null;
        }
        #endregion
    }
}