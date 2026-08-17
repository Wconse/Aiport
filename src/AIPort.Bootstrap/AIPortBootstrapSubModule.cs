using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace AIPort
{
    public sealed class AIPortBootstrapSubModule : MBSubModuleBase
    {
        private static readonly object Gate = new object();
        private static bool resolverInstalled;
        private static bool runtimeLoaded;
        private static Type runtimeSubModuleType;
        private static Action<float> runtimeApplicationTick;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            Console.WriteLine("[AIPort] Bootstrap OnSubModuleLoad");
            try
            {
                InstallResolver();
                LoadRuntime();
            }
            catch (Exception ex)
            {
                Console.WriteLine("[AIPort] Bootstrap failed: " + ex);
                throw;
            }
        }

        protected override void OnApplicationTick(float deltaTime)
        {
            base.OnApplicationTick(deltaTime);
            Action<float> tick = runtimeApplicationTick;
            if (tick != null) tick(deltaTime);
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            try
            {
                InstallResolver();
                LoadRuntime();
                Type subModuleType;
                lock (Gate) subModuleType = runtimeSubModuleType;
                if (subModuleType == null) throw new InvalidOperationException("AIPort runtime submodule type is unavailable.");
                MethodInfo register = subModuleType.GetMethod("RegisterCampaignDialogs", BindingFlags.Public | BindingFlags.Static);
                if (register == null) throw new MissingMethodException(subModuleType.FullName, "RegisterCampaignDialogs");
                register.Invoke(null, new object[] { gameStarterObject });
                Console.WriteLine("[AIPort] Bootstrap delegated OnGameStart to runtime dialogue registration");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[AIPort] Bootstrap OnGameStart delegation failed: " + ex);
                throw;
            }
        }

        private static void InstallResolver()
        {
            lock (Gate)
            {
                if (resolverInstalled)
                {
                    return;
                }

                AppDomain.CurrentDomain.AssemblyResolve += ResolveFromCoopBins;
                resolverInstalled = true;
                Console.WriteLine("[AIPort] AssemblyResolve installed");
            }
        }

        private static void LoadRuntime()
        {
            lock (Gate)
            {
                if (runtimeLoaded)
                {
                    return;
                }

                string bootstrapDir = Path.GetDirectoryName(typeof(AIPortBootstrapSubModule).Assembly.Location);
                string runtimePath = Path.Combine(bootstrapDir ?? string.Empty, "AIPort.dll");
                if (!File.Exists(runtimePath))
                {
                    throw new FileNotFoundException("AIPort runtime DLL was not found beside the bootstrap.", runtimePath);
                }

                Assembly runtime = Assembly.LoadFrom(runtimePath);
                runtimeSubModuleType = runtime.GetType("AIPort.AIPortSubModule", true);
                Type lifecycleBridge = runtime.GetType("AIPort.AIPortRuntimeLifecycleBridge", true);
                MethodInfo applicationTick = lifecycleBridge.GetMethod("ApplicationTick", BindingFlags.Public | BindingFlags.Static);
                if (applicationTick == null) throw new MissingMethodException(lifecycleBridge.FullName, "ApplicationTick");
                runtimeApplicationTick = (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), applicationTick);
                runtimeLoaded = true;
                Type protocol = runtime.GetType("AIPort.Protocol.AIPortProtocol");
                string build = protocol == null ? "unknown" : (string)protocol.GetField("Build").GetValue(null);
                string version = protocol == null ? "unknown" : protocol.GetField("Version").GetValue(null).ToString();
                Console.WriteLine("[AIPort] Runtime loaded from " + runtimePath + "; build=" + build + ", protocol=" + version + ", applicationTickBridge=true");
            }
        }

        private static Assembly ResolveFromCoopBins(object sender, ResolveEventArgs args)
        {
            try
            {
                string simpleName = new AssemblyName(args.Name).Name;
                if (string.IsNullOrEmpty(simpleName) || simpleName.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                foreach (string directory in ProbeDirectories())
                {
                    string candidate = Path.Combine(directory, simpleName + ".dll");
                    if (File.Exists(candidate))
                    {
                        Console.WriteLine("[AIPort] Resolving " + simpleName + " from " + candidate);
                        return Assembly.LoadFrom(candidate);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[AIPort] AssemblyResolve error: " + ex.Message);
            }

            return null;
        }

        private static IEnumerable<string> ProbeDirectories()
        {
            string bootstrapDir = Path.GetDirectoryName(typeof(AIPortBootstrapSubModule).Assembly.Location);
            if (string.IsNullOrEmpty(bootstrapDir))
            {
                yield break;
            }

            yield return bootstrapDir;

            string modulesDir = Path.GetFullPath(Path.Combine(bootstrapDir, "..", "..", ".."));
            yield return Path.Combine(modulesDir, "Coop", "bin", "Win64_Shipping_Server");
            yield return Path.Combine(modulesDir, "Coop", "bin", "Win64_Shipping_Client");
        }
    }
}
