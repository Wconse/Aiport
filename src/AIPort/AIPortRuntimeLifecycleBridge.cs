using System;

namespace AIPort
{
    public static class AIPortRuntimeLifecycleBridge
    {
        private static readonly object Sync = new object();
        private static Action<float> applicationTickHandler;

        internal static void AttachApplicationTick(Action<float> handler)
        {
            lock (Sync) applicationTickHandler = handler;
        }

        internal static void DetachApplicationTick(Action<float> handler)
        {
            lock (Sync)
            {
                if (applicationTickHandler == handler) applicationTickHandler = null;
            }
        }

        public static void ApplicationTick(float deltaTime)
        {
            Action<float> handler;
            lock (Sync) handler = applicationTickHandler;
            if (handler == null) return;
            try { handler(deltaTime); }
            catch (Exception ex)
            {
                Console.WriteLine("[AIPort] Application tick bridge failed: " + ex);
            }
        }
    }
}
