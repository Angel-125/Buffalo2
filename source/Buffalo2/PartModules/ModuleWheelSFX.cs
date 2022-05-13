using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ModuleWheels;

namespace Buffalo2
{
    public class ModuleWheelSFX: PartModule
    {
        #region Fields
        [KSPField]
        public string runningEffect = string.Empty;

        [KSPField]
        public float revTime = 0.05f;
        #endregion

        #region Housekeeping
        ModuleWheelMotor wheelMotor;
        float runningPowerLevel = 0f;
        #endregion

        #region Overrides
        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            wheelMotor = part.FindModuleImplementing<ModuleWheelMotor>();
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            if (!HighLogic.LoadedSceneIsFlight)
                return;
            if (wheelMotor == null || (wheelMotor.state != ModuleWheelMotor.MotorState.Running && wheelMotor.state != ModuleWheelMotor.MotorState.Idle))
            {
                part.Effect(runningEffect, 0f);
                return;
            }

            if (wheelMotor.state == ModuleWheelMotor.MotorState.Running)
            {
                runningPowerLevel = Mathf.Lerp(runningPowerLevel, 1, revTime);
                if (runningPowerLevel > 0.99f)
                    runningPowerLevel = 1f;
                part.Effect(runningEffect, runningPowerLevel);
            }
            else if (wheelMotor.state == ModuleWheelMotor.MotorState.Idle)
            {
                // Now back the power level down to 0.
                runningPowerLevel = Mathf.Lerp(runningPowerLevel, 0, revTime);
                if (runningPowerLevel < 0.001)
                    runningPowerLevel = 0f;
            }
            part.Effect(runningEffect, runningPowerLevel);
        }
        #endregion
    }
}
