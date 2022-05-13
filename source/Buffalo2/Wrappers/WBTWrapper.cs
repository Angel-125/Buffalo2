using System.Reflection;

namespace Buffalo2.Wrappers
{
    public class WBTWrapper
    {
        public static Assembly assembly;

        public static void Init()
        {
            //Get the assembly
            foreach (AssemblyLoader.LoadedAssembly loadedAssembly in AssemblyLoader.loadedAssemblies)
            {
                if (loadedAssembly.name == "WildBlueTools")
                {
                    assembly = loadedAssembly.assembly;
                    break;
                }

            }
            if (assembly == null)
                return;

            //Now init the classes
            WBIOmniStorageWrapper.InitClass(assembly);
        }

        public static bool IsInstalled()
        {
            Init();
            return WBTWrapper.assembly != null;
        }
    }
}