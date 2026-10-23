namespace ArGuildEncounter.Spells
{
    /// <summary>
    /// Ten authoritative combat types per COMBAT-BALANCE-V1 specification.
    /// Lightning is used consistently (never Electric).
    /// Null is used consistently (never Normal).
    /// </summary>
    public enum CombatElement
    {
        Fire = 0,
        Water = 1,
        Ice = 2,
        Lightning = 3,
        Wind = 4,
        Ground = 5,
        Grass = 6,
        Light = 7,
        Dark = 8,
        Null = 9
    }

    /// <summary>
    /// Authoritative damage classes per COMBAT-BALANCE-V1.
    /// Separate from castTier.
    /// L: 60 base damage
    /// M: 100 base damage
    /// H: 140 base damage
    /// SH: 180 base damage
    /// Fallback: 40 base damage (Struggle), 0 mana cost
    /// </summary>
    public enum DamageClass
    {
        L = 0,
        M = 1,
        H = 2,
        SH = 3,
        Fallback = 4
    }

    /// <summary>
    /// Three spell tiers dictating authoritative somatic gesture cast duration windows.
    /// Tier 1: 5.0s
    /// Tier 2: 4.0s
    /// Tier 3: 3.0s
    /// </summary>
    public enum SpellTier
    {
        Tier1 = 1, // 5.0s cast window
        Tier2 = 2, // 4.0s cast window
        Tier3 = 3  // 3.0s cast window
    }
}

