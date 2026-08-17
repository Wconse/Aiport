using System;

namespace AIPort
{
    internal static class AIPortConversationInputBridge
    {
        internal const string PlayerLineId = "aiport_freeform_player_option";
        internal const string ContinueLineId = "aiport_continue_player_option";
        internal const string FinishLineId = "aiport_finish_player_option";
        internal const string WaitingToken = "aiport_freeform_waiting";
        internal const string ResponseOptionsToken = "aiport_response_options";
        internal const string ReturnToVanillaToken = "aiport_return_to_vanilla";

        private static readonly object Sync = new object();
        private static Action<string> submitHandler;
        private static Func<bool> canSubmitHandler;
        private static Action requestPauseHandler;
        private static Action exitToVanillaHandler;

        internal static bool IsAvailable
        {
            get
            {
                Action<string> submit;
                Func<bool> canSubmit;
                lock (Sync)
                {
                    submit = submitHandler;
                    canSubmit = canSubmitHandler;
                }
                if (submit == null || canSubmit == null) return false;
                try { return canSubmit(); }
                catch { return false; }
            }
        }


        internal static bool IsControlLineId(string lineId)
        {
            return string.Equals(lineId, PlayerLineId, StringComparison.Ordinal)
                || string.Equals(lineId, ContinueLineId, StringComparison.Ordinal)
                || string.Equals(lineId, FinishLineId, StringComparison.Ordinal);
        }

        internal static void Attach(Action<string> submit, Func<bool> canSubmit, Action requestPause, Action exitToVanilla)
        {
            lock (Sync)
            {
                submitHandler = submit;
                canSubmitHandler = canSubmit;
                requestPauseHandler = requestPause;
                exitToVanillaHandler = exitToVanilla;
            }
        }

        internal static void Detach(Action<string> submit, Func<bool> canSubmit, Action requestPause, Action exitToVanilla)
        {
            lock (Sync)
            {
                if (submitHandler == submit) submitHandler = null;
                if (canSubmitHandler == canSubmit) canSubmitHandler = null;
                if (requestPauseHandler == requestPause) requestPauseHandler = null;
                if (exitToVanillaHandler == exitToVanilla) exitToVanillaHandler = null;
            }
        }

        internal static void RequestSharedPause()
        {
            Action requestPause;
            lock (Sync) requestPause = requestPauseHandler;
            if (requestPause == null) return;
            try { requestPause(); }
            catch { }
        }

        internal static void Submit(string text)
        {
            Action<string> submit;
            lock (Sync) submit = submitHandler;
            if (submit != null) submit(text);
        }

        internal static void ExitToVanilla()
        {
            Action exitToVanilla;
            lock (Sync) exitToVanilla = exitToVanillaHandler;
            if (exitToVanilla == null) return;
            try { exitToVanilla(); }
            catch { }
        }
    }
}
