using System;
using System.Linq;
using System.Reflection;

namespace Buffalo2.Wrappers
{
    public class WBIOmniStorageWrapper
    {
        #region Housekeeping
        static Type typeWBIOmniStorage;
        static MethodInfo miUpdateStorageVolume;

        public PartModule partModule;
        #endregion

        #region Initializers
        public static void InitClass(Assembly assembly)
        {
            typeWBIOmniStorage = assembly.GetTypes().First(t => t.Name.Equals("WBIOmniStorage"));
            miUpdateStorageVolume = typeWBIOmniStorage.GetMethod("UpdateStorageVolume", new[] {typeof(float) });
        }

        public static WBIOmniStorageWrapper GetOmniStorage(Part part)
        {
            int count = part.Modules.Count;
            PartModule module;

            for (int index = 0; index < count; index++)
            {
                module = part.Modules[index];
                if (module.moduleName == "WBIOmniStorage")
                {
                    WBIOmniStorageWrapper omniStorage = new WBIOmniStorageWrapper(module);
                    return omniStorage;
                }
            }

            return null;
        }

        public WBIOmniStorageWrapper(PartModule module)
        {
            if (WBTWrapper.assembly == null)
                WBTWrapper.Init();

            partModule = module;
        }
        #endregion

        #region API
        public void UpdateStorageVolume(float storageVolume)
        {
            if (miUpdateStorageVolume == null)
                return;

            miUpdateStorageVolume.Invoke(partModule, new object[] { storageVolume });
        }
        #endregion
    }
}
