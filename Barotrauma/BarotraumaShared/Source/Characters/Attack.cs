﻿using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Xml.Linq;


namespace Barotrauma
{
    enum CauseOfDeath
    {
        Damage, Bloodloss, Pressure, Suffocation, Drowning, Burn, Husk, Disconnected
    }

    public enum DamageType { None, Blunt, Slash, Burn }

    struct AttackResult
    {
        public readonly float Damage;
        public readonly float Bleeding;
        
        public readonly bool HitArmor;

        public AttackResult(float damage, float bleeding, bool hitArmor=false)
        {
            this.Damage = damage;
            this.Bleeding = bleeding;

            this.HitArmor = hitArmor;
        }
    }

    partial class Attack
    {
        public readonly float Range;
        public readonly float Duration;

        public readonly DamageType DamageType;

        private readonly float structureDamage;
        private readonly float damage;
        private readonly float bleedingDamage;

        private readonly List<StatusEffect> statusEffects;

        public readonly float Force;

        public readonly float Torque;

        public readonly float TargetForce;

        public readonly float SeverLimbsProbability;

        //the indices of the limbs Force is applied on 
        //(if none, force is applied only to the limb the attack is attached to)
        public readonly List<int> ApplyForceOnLimbs;
        
        public readonly float Stun;

        private float priority;

        public float GetDamage(float deltaTime)
        {
            return (Duration == 0.0f) ? damage : damage * deltaTime;
        }

        public float GetBleedingDamage(float deltaTime)
        {
            return (Duration == 0.0f) ? bleedingDamage : bleedingDamage * deltaTime;
        }

        public float GetStructureDamage(float deltaTime)
        {
            return (Duration == 0.0f) ? structureDamage : structureDamage * deltaTime;
        }

        public Attack(float damage, float structureDamage, float bleedingDamage, float range = 0.0f)
        {
            Range = range;
            this.damage = damage;
            this.structureDamage = structureDamage;
            this.bleedingDamage = bleedingDamage;
        }

        public Attack(XElement element)
        {
            try
            {
                DamageType = (DamageType)Enum.Parse(typeof(DamageType), ToolBox.GetAttributeString(element, "damagetype", "None"), true);
            }
            catch
            {
                DamageType = DamageType.None;
            }

            damage          = ToolBox.GetAttributeFloat(element, "damage", 0.0f);
            structureDamage = ToolBox.GetAttributeFloat(element, "structuredamage", 0.0f);
            bleedingDamage  = ToolBox.GetAttributeFloat(element, "bleedingdamage", 0.0f);
            Stun            = ToolBox.GetAttributeFloat(element, "stun", 0.0f);

            SeverLimbsProbability = ToolBox.GetAttributeFloat(element, "severlimbsprobability", 0.0f);

            Force = ToolBox.GetAttributeFloat(element,"force", 0.0f);
            TargetForce = ToolBox.GetAttributeFloat(element, "targetforce", 0.0f);
            Torque = ToolBox.GetAttributeFloat(element, "torque", 0.0f);
            
            Range = ToolBox.GetAttributeFloat(element, "range", 0.0f);
            Duration = ToolBox.GetAttributeFloat(element, "duration", 0.0f); 

            priority = ToolBox.GetAttributeFloat(element, "priority", 1.0f);

            InitProjSpecific(element);

            string limbIndicesStr = ToolBox.GetAttributeString(element, "applyforceonlimbs", "");
            if (!string.IsNullOrWhiteSpace(limbIndicesStr))
            {
                ApplyForceOnLimbs = new List<int>();
                foreach (string limbIndexStr in limbIndicesStr.Split(','))
                {
                    int limbIndex;
                    if (int.TryParse(limbIndexStr, out limbIndex))
                    {
                        ApplyForceOnLimbs.Add(limbIndex);
                    }
                }
            }

            foreach (XElement subElement in element.Elements())
            {
                switch (subElement.Name.ToString().ToLowerInvariant())
                {
                    case "statuseffect":
                        if (statusEffects == null)
                        {
                            statusEffects = new List<StatusEffect>();
                        }
                        statusEffects.Add(StatusEffect.Load(subElement));
                        break;
                }

            }
        }
        partial void InitProjSpecific(XElement element);
        
        public AttackResult DoDamage(IDamageable attacker, IDamageable target, Vector2 worldPosition, float deltaTime, bool playSound = true)
        {
            DamageParticles(worldPosition);

            var attackResult = target.AddDamage(attacker, worldPosition, this, deltaTime, playSound);

            var effectType = attackResult.Damage > 0.0f ? ActionType.OnUse : ActionType.OnFailure;

            if (statusEffects == null)
            {
                return attackResult;
            }

            foreach (StatusEffect effect in statusEffects)
            {
                if (effect.Targets.HasFlag(StatusEffect.TargetType.This) && attacker is Character)
                {
                    effect.Apply(effectType, deltaTime, (Character)attacker, (Character)attacker);
                }
                if (effect.Targets.HasFlag(StatusEffect.TargetType.Character) && target is Character)
                {
                    effect.Apply(effectType, deltaTime, (Character)target, (Character)target);
                }
            }

            return attackResult;
        }
        partial void DamageParticles(Vector2 worldPosition);
    }
}
