using System;
using System.Collections.Generic;
using TaleWorlds.Library;

namespace AIPort.Server
{
    public static class AIPortServerSimulationBridge
    {
        private static readonly object Gate = new object();
        private static Func<List<string>, string> handler;

        internal static void Bind(Func<List<string>, string> value)
        {
            lock (Gate) handler = value;
        }

        internal static void Unbind(Func<List<string>, string> value)
        {
            lock (Gate)
            {
                if (handler == value) handler = null;
            }
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("simulate_npc_offer", "aiport")]
        public static string SimulateNpcOffer(List<string> args)
        {
            Func<List<string>, string> current;
            lock (Gate) current = handler;
            if (current == null) return "AIPort server handler is unavailable.";
            return current(args ?? new List<string>());
        }
    }
}
