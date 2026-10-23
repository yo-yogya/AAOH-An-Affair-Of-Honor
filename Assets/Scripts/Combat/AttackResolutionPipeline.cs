using System;
using System.Collections.Generic;
using UnityEngine;
using ArGuildEncounter.Spells;
using ArGuildEncounter.Guilds;
using ArGuildEncounter.Environment;
using ArGuildEncounter.StatusEffects;

namespace ArGuildEncounter.Combat
{
    /// <summary>
    /// Result structure produced after executing the authoritative attack resolution pipeline.
    /// </summary>
    public struct AttackResolutionResult
    {
        public bool isCastSuccessful;
        public bool isTargetValid;
        public bool isHit;
        public bool isCritical;
        public float finalMissChance;
        public int rawBaseDamage;
        public float typeMultiplier;
        public float offensiveBonus;
        public int finalDamageDealt;
        public int healingApplied;
        public bool didSelfDamageTrigger;
        public int selfDamageDealt;
        public bool didBackfireTrigger;
        public int backfireDamageDealt;
        public bool didMoveSkipApply;
        public bool didForceFallbackApply;
        public string appliedStatusName;
        public string summaryText;
        public CombatDiagnosticData diagnosticData;
    }

    /// <summary>
    /// Authoritative attack resolution pipeline per COMBAT-BALANCE-V1.
    /// Follows:
    /// Validate -> Deduct Mana -> Validate Cast/Target -> 7.5% Base Miss (capped at 35%) ->
    /// Dual-Type Multiplier -> Guild/Terrain/Buffs (+50% cap) -> Critical (Gambit 45%/1.25x) ->
    /// Final Damage -> Heal (40%) -> Self-Damage (100 HP) -> Control/Status -> HP Check -> Advance.
    /// </summary>
    public class AttackResolutionPipeline
    {
        private readonly CombatConfig _config;

        public bool ForceMissDebug { get; set; } = false;
        public bool ForceCritDebug { get; set; } = false;
        public bool ForceHitDebug { get; set; } = false;

        public AttackResolutionPipeline(CombatConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public static bool ValidatePreconditions(PlayerCombatState attacker, PlayerCombatState defender, SpellData spell, out string error)
        {
            error = string.Empty;
            if (attacker == null)
            {
                error = "Attacker combat state is null.";
                return false;
            }
            if (defender == null)
            {
                error = "Defender combat state is null.";
                return false;
            }
            if (!attacker.IsAlive)
            {
                error = $"Attacker '{attacker.PlayerId}' is defeated.";
                return false;
            }
            if (!defender.IsAlive)
            {
                error = $"Defender '{defender.PlayerId}' is defeated.";
                return false;
            }
            if ((object)spell == null)
            {
                error = "Selected spell is null.";
                return false;
            }
            return true;
        }

        public AttackResolutionResult ResolveAttack(
            PlayerCombatState attacker,
            PlayerCombatState defender,
            SpellData spell,
            EnvironmentData environment,
            TerrainData terrain,
            bool isGestureValid,
            bool isTargetAcquired,
            Func<float> rngProvider = null,
            List<SpellData> bluffPool = null)
        {
            var result = new AttackResolutionResult();
            var diag = new CombatDiagnosticData();

            // Precondition validation
            if (!ValidatePreconditions(attacker, defender, spell, out string validationError))
            {
                result.isCastSuccessful = false;
                result.isHit = false;
                result.summaryText = $"Attack resolution aborted: {validationError}";
                diag.hpTrace = validationError;
                diag.damageTrace = "0 DMG (Validation Error)";
                result.diagnosticData = diag;
                return result;
            }

            Func<float> roll = rngProvider ?? (() => (float)new System.Random().NextDouble());

            // Track initial vitals for trace capture
            diag.attackerId = attacker?.PlayerId ?? "Player";
            diag.defenderId = defender?.PlayerId ?? "Opponent";
            diag.spellName = spell?.displayName ?? "Unknown";
            diag.damageClass = spell != null ? spell.damageClass.ToString() : "None";
            diag.castTier = spell != null ? spell.castTier.ToString() : "None";

            diag.previousAttackerHp = attacker?.CurrentHP ?? 0;
            diag.previousDefenderHp = defender?.CurrentHP ?? 0;
            diag.previousAttackerMana = attacker?.CurrentMana ?? 0;
            diag.manaCostDeducted = spell?.manaCost ?? 0;
            diag.afterCostMana = Mathf.Max(0, diag.previousAttackerMana - diag.manaCostDeducted);
            diag.manaRegenAdded = _config.defaultManaRegenPerTurn;
            diag.finalAttackerMana = Mathf.Min(attacker?.MaxMana ?? 100, diag.afterCostMana + diag.manaRegenAdded);

            // 0. Compute Authoritative Miss Chance Upfront (COMBAT-BALANCE-V1 Section 16)
            float baseMiss = _config.globalBaseMissChance; // 0.075f
            float attackMissMod = (spell is not null) ? spell.missModifier : 0.0f;
            float envMissMod = (environment is not null) ? environment.environmentMissModifier : 0.0f;
            float statusMissMod = (attacker != null) ? attacker.GetTotalStatusMissModifier() : 0.0f;
            float rawMissChance = baseMiss + attackMissMod + envMissMod + statusMissMod;
            result.finalMissChance = Mathf.Clamp(rawMissChance, 0f, _config.maxFinalMissChance);

            diag.baseMissChance = baseMiss;
            diag.spellMissMod = attackMissMod;
            diag.envMissMod = envMissMod;
            diag.statusMissMod = statusMissMod;
            diag.rawMissChance = rawMissChance;
            diag.finalMissChance = result.finalMissChance;

            // 1. Cast Validation
            result.isCastSuccessful = isGestureValid;
            if (!result.isCastSuccessful)
            {
                // Failed cast: full mana deducted, attack does not launch, turn ends
                attacker.TryDeductMana(spell.manaCost);
                result.isHit = false;
                result.summaryText = $"Cast FAILED for {spell.displayName}. Mana deducted, turn lost.";

                diag.hpTrace = $"Defender HP: {diag.previousDefenderHp} (No Change - Cast Failed)";
                diag.manaTrace = $"Attacker Mana: {diag.previousAttackerMana} -> Cost: -{diag.manaCostDeducted} -> Remaining: {diag.afterCostMana} -> End-Turn Regen: +{diag.manaRegenAdded} -> Final: {diag.finalAttackerMana}";
                diag.missTrace = "Cast Validation FAILED: Somatic gesture incorrect or timed out.";
                diag.damageTrace = "0 DMG (Cast Failed)";
                result.diagnosticData = diag;
                return result;
            }

            // Normal cast commits full mana cost
            attacker.TryDeductMana(spell.manaCost);

            // 2. Target Validation
            result.isTargetValid = isTargetAcquired;
            if (!result.isTargetValid)
            {
                result.isHit = false;
                result.summaryText = $"Target NOT acquired. Attack missed! (Miss Chance: {result.finalMissChance * 100f:0.0}%)";

                diag.hpTrace = $"Defender HP: {diag.previousDefenderHp} (No Change - Target Not Acquired)";
                diag.manaTrace = $"Attacker Mana: {diag.previousAttackerMana} -> Cost: -{diag.manaCostDeducted} -> Remaining: {diag.afterCostMana} -> End-Turn Regen: +{diag.manaRegenAdded} -> Final: {diag.finalAttackerMana}";
                diag.missTrace = $"Target NOT acquired in AR reticle. Authoritative miss chance was {result.finalMissChance * 100f:0.0}%.";
                diag.damageTrace = "0 DMG (Target Missed)";
                result.diagnosticData = diag;
                return result;
            }

            // 3. Resolve Bluff specifics if Bluff ability was cast
            SpellData resolvedSpell = spell;
            bool isBluffCast = attacker.Guild is not null && attacker.Guild.usesBluffSlot && string.Equals(spell.spellId, "BLUFF", StringComparison.OrdinalIgnoreCase);
            int baseDamage = spell.baseDamage;

            if (isBluffCast)
            {
                var eligibleSpells = new List<SpellData>();
                if (bluffPool != null && bluffPool.Count > 0)
                {
                    for (int i = 0; i < bluffPool.Count; i++)
                    {
                        var s = bluffPool[i];
                        if (s is not null && s.isBluffEligible && !s.isMoveSkip && !s.isForceFallback)
                        {
                            eligibleSpells.Add(s);
                        }
                    }
                }
                else if (attacker?.SpellSlots != null)
                {
                    for (int i = 0; i < 5 && i < attacker.SpellSlots.Length; i++)
                    {
                        var s = attacker.SpellSlots[i];
                        if (s is not null && s.isBluffEligible && !s.isMoveSkip && !s.isForceFallback)
                        {
                            eligibleSpells.Add(s);
                        }
                    }
                }

                if (eligibleSpells.Count > 0)
                {
                    int pickIndex = Mathf.FloorToInt(roll() * eligibleSpells.Count);
                    resolvedSpell = eligibleSpells[Mathf.Clamp(pickIndex, 0, eligibleSpells.Count - 1)];
                    baseDamage = Mathf.RoundToInt(resolvedSpell.baseDamage * _config.bluffDamageMultiplier);
                }
                else
                {
                    baseDamage = _config.struggleBaseDamage;
                }

                if (roll() < _config.bluffSelfDamageChance)
                {
                    result.didBackfireTrigger = true;
                    result.didSelfDamageTrigger = true;
                    int backfireDmg = Mathf.RoundToInt(attacker.MaxHP * _config.bluffSelfDamagePercentOfMaxHp);
                    result.backfireDamageDealt = backfireDmg;
                    result.selfDamageDealt = backfireDmg;
                    diag.backfireAmount = backfireDmg;
                    attacker.ApplyDamage(backfireDmg);
                }
            }

            result.rawBaseDamage = baseDamage;
            diag.rawBaseDamage = baseDamage;

            // 4. Base Miss & Additive Modifier Pipeline
            baseMiss = _config.globalBaseMissChance;
            attackMissMod = resolvedSpell.missModifier;
            envMissMod = environment is not null ? environment.environmentMissModifier : 0.0f;
            statusMissMod = attacker.GetTotalStatusMissModifier();

            rawMissChance = baseMiss + attackMissMod + envMissMod + statusMissMod;
            float finalMissChance = Mathf.Clamp(rawMissChance, 0f, _config.maxFinalMissChance);
            result.finalMissChance = finalMissChance;
            diag.finalMissChance = finalMissChance;

            float missRollValue = roll();
            diag.missRoll = missRollValue;
            bool isMiss = !ForceHitDebug && (ForceMissDebug || (missRollValue < finalMissChance));
            ForceMissDebug = false;
            ForceHitDebug = false;

            if (isMiss)
            {
                result.isHit = false;
                diag.isHit = false;
                attacker.ConsumeAttackBuffsAndDebuffs(false);
                if (attacker.IsForceFallbackActive) attacker.ConsumeForceFallback();

                string backfireMsg = result.didBackfireTrigger ? $" (Bluff Backfire: -{result.backfireDamageDealt} HP)" : "";
                result.summaryText = $"Attack MISSED! (Miss Chance: {finalMissChance * 100f:0.0}%){backfireMsg}";

                diag.hpTrace = $"Defender HP: {diag.previousDefenderHp} (No Change - Attack Missed)";
                if (result.didBackfireTrigger)
                {
                    diag.hpTrace += $" | Attacker HP: {diag.previousAttackerHp} -> Backfire: -{result.backfireDamageDealt} -> {attacker.CurrentHP}";
                }
                diag.manaTrace = $"Attacker Mana: {diag.previousAttackerMana} -> Cost: -{diag.manaCostDeducted} -> Remaining: {diag.afterCostMana} -> End-Turn Regen: +{diag.manaRegenAdded} -> Final: {diag.finalAttackerMana}";
                diag.missTrace = $"Base: {baseMiss * 100f:0.0}% + Spell: {attackMissMod * 100f:0.0}% + Env: {envMissMod * 100f:0.0}% + Status: {statusMissMod * 100f:0.0}% = {rawMissChance * 100f:0.0}% (Cap: {finalMissChance * 100f:0.0}%) -> Roll: {missRollValue:0.000} -> MISSED";
                diag.damageTrace = "0 DMG (Attack Missed)";
                result.diagnosticData = diag;
                return result;
            }

            result.isHit = true;
            diag.isHit = true;

            // 5. Type Effectiveness
            CombatElement defenderElement = (defender.Guild is not null && defender.Guild.affinityTypes is not null && defender.Guild.affinityTypes.Count > 0)
                ? defender.Guild.affinityTypes[0]
                : CombatElement.Null;

            float typeMultiplier = _config.GetDualTypeMultiplier(
                resolvedSpell.type1,
                resolvedSpell.type2,
                resolvedSpell.isDualType,
                defenderElement);
            result.typeMultiplier = typeMultiplier;
            diag.typeMultiplier = typeMultiplier;

            // 6. Offensive Bonus & Cap
            float guildBonus = 0f;
            if (!isBluffCast && attacker.Guild is not null && attacker.Guild.HasAffinityBonus(resolvedSpell))
            {
                guildBonus = _config.guildOffensiveBonus; // 0.25f
            }

            float terrainBonus = 0f;
            if (terrain is not null && resolvedSpell.HasType(terrain.primaryAffinity))
            {
                terrainBonus = _config.terrainOffensiveBonus; // 0.25f
            }

            float buffBonus = attacker.GetOffensiveBuffForElement(resolvedSpell.type1);
            if (resolvedSpell.isDualType)
            {
                buffBonus += attacker.GetOffensiveBuffForElement(resolvedSpell.type2);
            }

            float pathBonus = attacker != null ? attacker.PathAttackBonus : 0f;
            float totalOffensiveBonus = guildBonus + terrainBonus + buffBonus + pathBonus;
            float effectiveOffensiveBonus = Mathf.Clamp(totalOffensiveBonus, 0f, _config.offensiveBonusCap + pathBonus);
            result.offensiveBonus = effectiveOffensiveBonus;

            diag.guildBonus = guildBonus;
            diag.terrainBonus = terrainBonus;
            diag.buffBonus = buffBonus;
            diag.totalOffensiveBonus = totalOffensiveBonus;
            diag.effectiveOffensiveBonus = effectiveOffensiveBonus;

            // 7. Critical Calculation
            bool isGambit = attacker.Guild is not null && attacker.Guild.guildType == GuildType.Gambit;
            float critChance = isGambit ? _config.gambitCriticalChance : 0.0f;
            float critMult = isGambit ? _config.gambitCriticalMultiplier : 1.0f;

            bool isCrit = ForceCritDebug || (resolvedSpell.isCriticalEligible && (roll() < critChance));
            ForceCritDebug = false;
            result.isCritical = isCrit;
            float finalCritMultiplier = isCrit ? critMult : 1.0f;

            diag.isCrit = isCrit;
            diag.critMultiplier = finalCritMultiplier;

            // 8. Damage Calculation
            float calculatedDamage = baseDamage * typeMultiplier * (1f + effectiveOffensiveBonus) * finalCritMultiplier;
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(calculatedDamage));
            result.finalDamageDealt = finalDamage;
            diag.finalDamage = finalDamage;
            diag.damageDealt = finalDamage;

            // 9. Apply Damage to Defender
            defender.ApplyDamage(finalDamage);
            diag.newDefenderHp = defender.CurrentHP;

            // 10. Healing
            if (resolvedSpell.healsCaster)
            {
                int healAmount = Mathf.RoundToInt(finalDamage * _config.healingPercentOfFinalDamage);
                result.healingApplied = healAmount;
                diag.healingAmount = healAmount;
                attacker.ApplyHealing(healAmount);
            }

            // 11. Self-Damage
            if (resolvedSpell.dealsSelfDamage)
            {
                result.didSelfDamageTrigger = true;
                result.selfDamageDealt = resolvedSpell.selfDamageAmount;
                diag.selfDamageAmount = resolvedSpell.selfDamageAmount;
                attacker.ApplySelfDamage(resolvedSpell.selfDamageAmount);
            }

            // 12. Control & Status Application
            if (resolvedSpell.isMoveSkip)
            {
                result.didMoveSkipApply = defender.TryApplyMoveSkip();
            }
            if (resolvedSpell.isForceFallback)
            {
                result.didForceFallbackApply = defender.TryApplyForceFallback();
            }

            if (resolvedSpell.displayName == "Fire Cloak") attacker.ApplyEmberWard();
            if (resolvedSpell.displayName == "Flame Legion") attacker.ApplyFlameLegion();
            if (resolvedSpell.displayName == "Ground Fall") attacker.ApplyHydrationSurge();

            if (resolvedSpell.displayName == "Blind") defender.ApplyBlindness();
            if (resolvedSpell.displayName == "Electro Swamp") defender.ApplyDampenedSight();
            if (resolvedSpell.displayName == "Suffocating Gas") defender.ApplySuffocation();

            attacker.ConsumeAttackBuffsAndDebuffs(true);
            if (attacker.IsForceFallbackActive) attacker.ConsumeForceFallback();

            diag.newAttackerHp = attacker.CurrentHP;

            // Complete Traces
            string hpTraceStr = $"Defender HP: {diag.previousDefenderHp} -> Damage: -{finalDamage} -> {diag.newDefenderHp}";
            if (result.healingApplied > 0)
                hpTraceStr += $" | Attacker Heal: +{result.healingApplied} -> {diag.newAttackerHp}";
            if (result.didSelfDamageTrigger)
                hpTraceStr += $" | Attacker Recoil: -{result.selfDamageDealt} -> {diag.newAttackerHp}";
            if (result.didBackfireTrigger)
                hpTraceStr += $" | Bluff Backfire: -{result.backfireDamageDealt} -> {diag.newAttackerHp}";
            diag.hpTrace = hpTraceStr;

            diag.manaTrace = $"Attacker Mana: {diag.previousAttackerMana} -> Cost: -{diag.manaCostDeducted} -> Remaining: {diag.afterCostMana} -> End-Turn Regen: +{diag.manaRegenAdded} -> Final: {diag.finalAttackerMana}";
            diag.missTrace = $"Base: {baseMiss * 100f:0.0}% + Spell: {attackMissMod * 100f:0.0}% + Env: {envMissMod * 100f:0.0}% + Status: {statusMissMod * 100f:0.0}% = {rawMissChance * 100f:0.0}% (Cap: {finalMissChance * 100f:0.0}%) -> Roll: {missRollValue:0.000} -> HIT";
            diag.damageTrace = $"Base: {baseDamage} x Type: {typeMultiplier:0.00} x (1 + Bonus: {effectiveOffensiveBonus * 100f:0.0}%) x Crit: {finalCritMultiplier:0.00}x = {finalDamage} DMG";
            diag.activeModifiers = $"Guild: {(attacker.Guild != null ? attacker.Guild.displayName : "None")} (+{guildBonus * 100f:0.0}%), Terrain: {(terrain != null ? terrain.terrainName : "None")} (+{terrainBonus * 100f:0.0}%), Buffs: (+{buffBonus * 100f:0.0}%), Effective: (+{effectiveOffensiveBonus * 100f:0.0}%)";
            diag.attackerStatuses = attacker.GetActiveStatusesDisplay();
            diag.defenderStatuses = defender.GetActiveStatusesDisplay();

            result.diagnosticData = diag;

            // Summary text
            string critText = isCrit ? " [CRITICAL! 1.25x]" : "";
            string healText = result.healingApplied > 0 ? $" (Healed: +{result.healingApplied} HP)" : "";
            string selfDmgText = result.didSelfDamageTrigger ? $" (Self-Damage: -{result.selfDamageDealt} HP)" : "";
            string backfireText = result.didBackfireTrigger ? $" (Bluff Backfire: -{result.backfireDamageDealt} HP)" : "";
            string controlText = result.didMoveSkipApply ? " [Opponent MOVE SKIPPED!]" : (result.didForceFallbackApply ? " [Opponent FORCED FALLBACK!]" : "");

            result.summaryText = $"Dealt {finalDamage} DMG with {resolvedSpell.displayName}{critText}!{healText}{selfDmgText}{backfireText}{controlText}";
            return result;
        }
    }
}

