namespace AIPort.Protocol
{
    public static class AIPortProtocol
    {
        public const int Version = 2;
        public const string Build = "0.0.99-dev";
        public const int CapabilitySchemaVersion = 1;
        public const int IntentSchemaVersion = 3;
        public const int StateSchemaVersion = 1;
        public const int CapabilityNarrative = 1;
        public const int CapabilityNoOpIntent = 2;
        public const int CapabilityStateSnapshot = 4;
        public const int CapabilityPersistentMemory = 8;
        public const int CapabilityRelationShadowIntent = 16;
        public const int CapabilityRelationConfirmation = 32;
        public const int CapabilityDiplomacySnapshot = 64;
        public const int CapabilityDiplomacyStatements = 128;
        public const int CapabilityValidationGate = 256;
        public const int CapabilityDiplomacyAuthority = 512;
        public const int CapabilityDiplomacyRecipientConsent = 1024;
        public const int CapabilityDiplomacyConflictGuard = 2048;
        public const int CapabilityDiplomacyInboxNotification = 4096;
        public const int CapabilityDiplomacyLifecycleBundle = 8192;
        public const int CapabilityNativeWarAdapter = 16384;
        public const int CapabilityNativeDiplomacyJournal = 32768;
        public const int CapabilityNativePeaceAdapter = 65536;
        public const int CapabilityNpcDiplomacyPolicy = 131072;
        public const int CapabilityDiplomacyDecisionUi = 262144;
        public const int CapabilityDiplomacyInboxList = 524288;
        public const int CapabilityNpcDiplomacyInitiativeScheduler = 1048576;
        public const int MaximumDiplomacyInboxPageSize = 8;
        public const int MaximumDiplomacyInboxItems = 16;
        public const int MaximumPlayerTextLength = 4000;
        public const int MaximumNpcDisplayTextLength = 8000;
        public const int MaximumTargetIdLength = 160;
        public const int MaximumTargetInstanceIdLength = 320;
    }
}
