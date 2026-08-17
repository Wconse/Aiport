using System;
using AIPort.Protocol;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace AIPort
{
    public sealed class AIPortSubModule : MBSubModuleBase
    {
        private static CampaignGameStarter registeredStarter;
        private static string returnInputToken = "hero_main_options";

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            Console.WriteLine("[AIPort] Assembly loaded; build=" + AIPortProtocol.Build + ", protocol=" + AIPortProtocol.Version);
            // Intentionally headless-safe: no UIExtender, MCM, Harmony scan or campaign mutation.
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            RegisterCampaignDialogs(gameStarterObject);
        }

        public static void RegisterCampaignDialogs(IGameStarter gameStarterObject)
        {
            CampaignGameStarter starter = gameStarterObject as CampaignGameStarter;
            if (starter == null || ReferenceEquals(registeredStarter, starter)) return;
            registeredStarter = starter;

            RegisterFreeFormEntry(starter, AIPortConversationInputBridge.PlayerLineId, "hero_main_options", 120, true);
            RegisterFreeFormEntry(starter, "aiport_freeform_townsfolk_option", "town_or_village_player", 120, false);
            RegisterFreeFormEntry(starter, "aiport_freeform_prison_guard_option", "prison_guard_talk", 120, false);
            RegisterFreeFormEntry(starter, "aiport_freeform_castle_guard_option", "castle_guard_talk", 120, false);
            RegisterFreeFormEntry(starter, "aiport_freeform_alley_option", "alley_talk_start", 120, false);
            RegisterFreeFormEntry(starter, "aiport_freeform_alley_owned_option", "alley_player_owned_start", 120, false);
            RegisterFreeFormEntry(starter, "aiport_freeform_alley_followup_option", "alley_options", 120, false);

            starter.AddDialogLine(
                "aiport_freeform_waiting_response",
                AIPortConversationInputBridge.WaitingToken,
                AIPortConversationInputBridge.ResponseOptionsToken,
                "{=AIPortWaitingResponse}Секунду… Я обдумаю ответ.",
                null,
                null,
                120,
                null);

            starter.AddPlayerLine(
                AIPortConversationInputBridge.ContinueLineId,
                AIPortConversationInputBridge.ResponseOptionsToken,
                AIPortConversationInputBridge.WaitingToken,
                "{=AIPortSayMore}Сказать ещё...",
                HasActiveConversationCharacter,
                OpenFreeFormInput,
                120,
                null,
                null);

            starter.AddPlayerLine(
                AIPortConversationInputBridge.FinishLineId,
                AIPortConversationInputBridge.ResponseOptionsToken,
                AIPortConversationInputBridge.ReturnToVanillaToken,
                "{=AIPortReturnToVanilla}Вернуться к обычному разговору.",
                HasActiveConversationCharacter,
                AIPortConversationInputBridge.ExitToVanilla,
                110,
                null,
                null);

            RegisterReturnToRoot(starter, "aiport_return_to_hero_root", "hero_main_options");
            RegisterReturnToRoot(starter, "aiport_return_to_townsfolk_root", "town_or_village_player");
            RegisterReturnToRoot(starter, "aiport_return_to_prison_guard_root", "prison_guard_talk");
            RegisterReturnToRoot(starter, "aiport_return_to_castle_guard_root", "castle_guard_talk");
            RegisterReturnToRoot(starter, "aiport_return_to_alley_root", "alley_talk_start");
            RegisterReturnToRoot(starter, "aiport_return_to_alley_owned_root", "alley_player_owned_start");
            RegisterReturnToRoot(starter, "aiport_return_to_alley_followup_root", "alley_options");

            Console.WriteLine("[AIPort] Free-form hero dialogue loop registered");
        }

        private static void RegisterFreeFormEntry(CampaignGameStarter starter, string lineId, string inputToken, int priority, bool heroOnly)
        {
            starter.AddRepeatablePlayerLine(
                lineId,
                inputToken,
                AIPortConversationInputBridge.WaitingToken,
                "{=AIPortFreeFormOption}\u0421\u043a\u0430\u0437\u0430\u0442\u044c \u0441\u0432\u043e\u0438\u043c\u0438 \u0441\u043b\u043e\u0432\u0430\u043c\u0438\u2026",
                "{=!}Continue listing AI dialogue entries.",
                inputToken,
                delegate { return CanOpenFreeFormInputAtRoot(heroOnly); },
                delegate { OpenFreeFormInputAtRoot(inputToken); },
                priority,
                null);
        }

        private static void RegisterReturnToRoot(CampaignGameStarter starter, string lineId, string inputToken)
        {
            starter.AddDialogLine(
                lineId,
                AIPortConversationInputBridge.ReturnToVanillaToken,
                inputToken,
                "{=AIPortReturnAck}\u0425\u043e\u0440\u043e\u0448\u043e. \u041f\u0440\u043e\u0434\u043e\u043b\u0436\u0438\u043c \u043e\u0431\u044b\u0447\u043d\u044b\u0439 \u0440\u0430\u0437\u0433\u043e\u0432\u043e\u0440.",
                delegate { return HasActiveConversationCharacter() && string.Equals(returnInputToken, inputToken, StringComparison.Ordinal); },
                null,
                120,
                null);
        }

        private static bool HasActiveConversationCharacter()
        {
            return Campaign.Current != null
                && Campaign.Current.ConversationManager != null
                && Campaign.Current.ConversationManager.OneToOneConversationCharacter != null;
        }

        private static bool CanShowFreeFormOption()
        {
            return HasActiveConversationCharacter() && !InformationManager.IsAnyInquiryActive();
        }

        private static bool CanOpenFreeFormInputAtRoot(bool heroOnly)
        {
            if (!CanOpenFreeFormInput()) return false;
            CharacterObject character = Campaign.Current.ConversationManager.OneToOneConversationCharacter;
            return character != null && (!heroOnly || character.IsHero);
        }

        private static bool CanOpenFreeFormInput()
        {
            return CanShowFreeFormOption()
                && AIPortConversationInputBridge.IsAvailable;
        }

        private static void OpenFreeFormInputAtRoot(string inputToken)
        {
            returnInputToken = string.IsNullOrWhiteSpace(inputToken) ? "hero_main_options" : inputToken;
            OpenFreeFormInput();
        }

        private static void OpenFreeFormInput()
        {
            if (!CanOpenFreeFormInput())
            {
                InformationManager.DisplayMessage(new InformationMessage("Ответ ещё обрабатывается. Подождите немного."));
                return;
            }
            AIPortConversationInputBridge.RequestSharedPause();
            TextInquiryData inquiry = new TextInquiryData(
                "AIPort",
                "Введите реплику персонажу:",
                true,
                true,
                "Отправить",
                "Отмена",
                AIPortConversationInputBridge.Submit,
                null,
                false,
                ValidateFreeFormInput,
                string.Empty,
                string.Empty);
            InformationManager.ShowTextInquiry(inquiry, false, true);
        }

        private static Tuple<bool, string> ValidateFreeFormInput(string text)
        {
            string value = text == null ? string.Empty : text.Trim();
            if (value.Length == 0) return Tuple.Create(false, "Введите непустую реплику.");
            if (value.Length > AIPortProtocol.MaximumPlayerTextLength)
            {
                return Tuple.Create(false, "Максимальная длина — " + AIPortProtocol.MaximumPlayerTextLength + " символов.");
            }
            return Tuple.Create(true, string.Empty);
        }
    }
}
