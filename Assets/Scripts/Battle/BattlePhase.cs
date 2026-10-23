namespace ArGuildEncounter.Battle
{
    /// <summary>
    /// Authoritative battle lifecycle and turn phases required by AR Guild Encounter specification.
    /// </summary>
    public enum BattlePhase
    {
        // Lobby & Match Setup
        WaitingForPlayer = 0,
        RoomCreated = 1,
        PlayerJoined = 2,
        ARCalibration = 3,
        GuildSelection = 4,
        Loadout = 5,
        BattleReady = 6,

        // Turn & Attack Execution Loop
        PlayerTurn = 7,
        SpellSelection = 8,
        VoiceProcessing = 9,
        SpellSelected = 10,
        GestureWindow = 11,
        AimPhase = 12,
        AttackLaunched = 13,
        AttackResolution = 14,
        HPCheck = 15,
        NextTurn = 16,
        BattleEnd = 17,

        // Transient / Exception System States
        Reconnecting = 18,
        ARTrackingLost = 19,
        OpponentLost = 20,
        OpponentTeleported = 21,
        Forfeit = 22,

        // Pre-Battle Preparation
        PathSelection = 23
    }
}
