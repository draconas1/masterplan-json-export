using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Masterplan.Data;

namespace EncounterExport.FoundryHelpers
{
    public static class FoundryPowerHelper
    {
        private static readonly char[] validIdChars = BuildIdCharsArray();
        private static char[] BuildIdCharsArray()
        {
            StringBuilder builder = new StringBuilder();
            Enumerable
                .Range(65, 26)
                .Select(e => ((char)e).ToString())
                .Concat(Enumerable.Range(97, 26).Select(e => ((char)e).ToString()))
                .Concat(Enumerable.Range(0, 10).Select(e => e.ToString()))
                .OrderBy(e => Guid.NewGuid())
                .ToList().ForEach(e => builder.Append(e));
            return builder.ToString().ToCharArray();
        }
        
        private static readonly string[] ElementalDamageTypes =
        {
            "acid",
            "cold",
            "fire",
            "force",
            "lightning",
            "necrotic",
            "poison",
            "psychic",
            "radiant",
            "thunder"
        };

        private static readonly string[] EffectTypes =
        {
            "augmentable",
            "aura",
            "beast",
            "beastForm",
            "beast form",
            "channelDiv",
            "channel divinity",
            "charm",
            "conjuration",
            "disease",
            "elemental",
            "enchantment",
            "evocation",
            "fear",
            "fullDis",
            "full discipline",
            "gaze",
            "healing",
            "illusion",
            "invigorating",
            "mount",
            "necro",
            "necromancy",
            "nether",
            "nethermancy",
            "poison",
            "polymorph",
            "rage",
            "rattling",
            "reliable",
            "runic",
            "sleep",
            "spirit",
            "stance",
            "summoning",
            "teleportation",
            "transmutation",
            "zone"
        };

        private static readonly HashSet<string> ElementalDamageTypeSet = new HashSet<string>(ElementalDamageTypes);
        
        private static readonly HashSet<string> EffectTypeSet = new HashSet<string>(EffectTypes);

        private static readonly Dictionary<string, string> EffectTypeLookup = BuildEffectLookup();

        private static Dictionary<string, string> BuildEffectLookup()
        {
            var dict = new Dictionary<string, string>();
            dict["beast form"] = "beastForm";
            dict["channel divinity"] = "channelDiv";
            dict["full discipline"] = "fullDis";
            dict["necromancy"] = "necro";
            dict["nethermancy"] = "nether";
            return dict;
        }
        
        private static string newId(int length = 16) {
            StringBuilder builder = new StringBuilder();
            Random rnd = new Random();
            for (int i = 0; i < length; i++)
            {
                builder.Append(validIdChars[rnd.Next(validIdChars.Length)]);
            }
            return builder.ToString();
        }
            
        public static FoundryPower ProcessAction(CreaturePower power, List<string> errors,
            bool attackPower)
        {
            var resultPower = new FoundryPower
            {
                name = power.Name,
                _id = newId()
            };
            var powerData = resultPower.data;
            powerData.keywords = power.Keywords.Split(CommonHelpers.separator, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim()).ToList();

            foreach (var powerDataKeyword in powerData.keywords)
            {
                if (ElementalDamageTypeSet.Contains(powerDataKeyword.ToLowerInvariant()))
                {
                    powerData.damageType.Add(powerDataKeyword.ToLowerInvariant(), true);
                }
            
                if (EffectTypeSet.Contains(powerDataKeyword.ToLowerInvariant()))
                {
                    var effectType = powerDataKeyword.ToLowerInvariant();
                    if (EffectTypeLookup.ContainsKey(effectType))
                    {
                        effectType = EffectTypeLookup[effectType];
                    }
                    
                    powerData.effectType.Add(effectType, true);
                }
            }

            var detailString = $"{power.Action.Action}, {power.Action.Use}, ";
            var use = "atwill";

            switch (power.Action.Use)
            {
                case PowerUseType.Basic:
                    powerData.basicAttack = true;
                    powerData.subName = "Basic Attack";
                    break;
                case PowerUseType.AtWill: break;
                default:
                    use = power.Action.Use.ToString().ToLowerInvariant();
                    break;
            }

            if (!string.IsNullOrEmpty(power.Action.Recharge))
            {
                use = "recharge";
            }

            powerData.useType = use;
            powerData.actionType = power.Action.Action.ToString().ToLowerInvariant();
            powerData.requirement = power.Condition;
            powerData.trigger = power.Action.Trigger;
            if (!string.IsNullOrEmpty(power.Action.Trigger))
            {
                detailString += $"({power.Action.Trigger}), ";
            }
            // range text not displayed for monster powers, perhaps player?
            powerData.rangeTextShort = power.Range;
            powerData.rangeText = power.Range;

            powerData.target = power.Range;
            
            powerData.rechargeCondition = power.Action.Recharge;
            if (power.Action.Recharge != null)
            {
                switch (power.Action.Recharge)
                {
                    case PowerAction.RECHARGE_2: powerData.rechargeRoll = 2;
                        break;
                    case PowerAction.RECHARGE_3: powerData.rechargeRoll = 3;
                        break;
                    case PowerAction.RECHARGE_4: powerData.rechargeRoll = 4;
                        break;
                    case PowerAction.RECHARGE_5: powerData.rechargeRoll = 5;
                        break;
                    case PowerAction.RECHARGE_6: powerData.rechargeRoll = 6;
                        break;
                    default:
                        try
                        {
                            powerData.rechargeRoll = Int32.Parse(power.Action.Recharge.Trim());
                        }
                        catch (Exception)
                        {
                            // do nothing, its probably a text condition
                        }

                        break;
                }
            }

            powerData.chatFlavor = power.Description;
            powerData.sustain.actionType = power.Action.SustainAction.ToString().ToLowerInvariant();

            // sometimes details are in the range field for traits
            powerData.effect.detail = string.IsNullOrEmpty(power.Details) ? power.Range : power.Details;
            
            // if however we now have an identical target and range should fix that to avoid duplicate descriptions
            if (powerData.effect.detail == powerData.target)
            {
                powerData.target = null;
            }
            
            if (!attackPower)
            {
                detailString += $"{powerData.effect.detail}";
            }
          
            resultPower.data.attack.isAttack = false;
            resultPower.data.hit.isDamage = false;
            resultPower.data.description.chat = detailString;

            return resultPower;
        }


        private static readonly Regex MissRgx =
            new Regex(@"miss: (.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        private static readonly Regex EffectRgx =
            new Regex(@"effect: (.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        private static readonly Regex SpecialRgx =
            new Regex(@"special: (.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        private static readonly Regex DamageTypeRegex1 =
            new Regex(@"([a-z]+) damage", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex DamageTypeRegex2 =
            new Regex(@"([a-z]+) and ([a-z]+) damage", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static FoundryPower ProcessAttack(CreaturePower power, List<string> errors)
        {
            var resultPower = ProcessAction(power, errors, true);
            var powerData = resultPower.data;
            resultPower.data.attack.isAttack = true;
            resultPower.data.hit.isDamage = true;

            // check to see if it was an elemental damage type, if not we fall back tp physcal 
            var shouldAddPhysicalDamage = true;
            var keywordsHashSet = new HashSet<string>(powerData.keywords.Select(x => x.ToLowerInvariant()));
            if (keywordsHashSet.Intersect(ElementalDamageTypeSet).Any())
            {
                shouldAddPhysicalDamage = false;
            }

            // parse the power text just in case they didn't add the damage type to the keywords
            // single type
            {
                var damageTypeInPowerData = DamageTypeRegex1.Match(power.Details);
                if (damageTypeInPowerData.Success)
                {
                    var damageType = damageTypeInPowerData.Groups[1].Value.ToLowerInvariant();
                    powerData.damageType[damageType] = true;
                    shouldAddPhysicalDamage = false;
                }
            }
            //multiple types
            {
                var damageTypeInPowerData = DamageTypeRegex2.Match(power.Details);
                if (damageTypeInPowerData.Success)
                {
                    var damageType1 = damageTypeInPowerData.Groups[1].Value.ToLowerInvariant();
                    powerData.damageType[damageType1] = true;
                    var damageType2 = damageTypeInPowerData.Groups[2].Value.ToLowerInvariant();
                    powerData.damageType[damageType2] = true;
                    shouldAddPhysicalDamage = false;
                }
            }
            // more than 2 types is incredibly rare, if they have done that they need to have done it in the keywords like a sensible person

            if (shouldAddPhysicalDamage)
            {
                powerData.damageType["physical"] = true;
            }

            ProcessRangeAndWeapon(power, powerData, errors);
            var def = "AC";
            switch (power.Attack.Defence)
            {
                case DefenceType.Fortitude:
                    def = "Fort";
                    break;
                case DefenceType.Reflex:
                    def = "Ref";
                    break;
                case DefenceType.Will:
                    def = "Wil";
                    break;
                default: break;
            }

            powerData.attack.def = def.ToLowerInvariant();
            powerData.attack.formula = power.Attack.Bonus.ToString();
            powerData.description.chat += $"{power.Range}, {power.Attack.Bonus} vs {def}";

            // reminder that details is the entire block and that Damage is Masterplans attempt to parse it out
            powerData.hit.detail = power.Details;
            var damage = CommonHelpers.parseDamageString(power.Damage, errors, power.Name);
            powerData.hit.formula = damage.NumDice > 0
                ? damage.NumDice + "d" + damage.DiceSize + "+" + damage.Bonus
                : damage.Bonus.ToString();
            powerData.hit.critFormula = damage.NumDice + "*" + damage.DiceSize + "+" + damage.Bonus;

            // attempt to get miss, effect and special out of the details.  
            {
                var match = MissRgx.Match(power.Details);
                if (match.Success)
                {
                    powerData.miss.detail = match.Groups[1].Value;
                    powerData.hit.detail = powerData.hit.detail.Replace(match.Value, "");
                }
            }
            {
                // effect detail was set by trait call
                powerData.effect.detail = null;
                var match = EffectRgx.Match(power.Details);
                if (match.Success)
                {
                    powerData.effect.detail = match.Groups[1].Value;
                    powerData.hit.detail = powerData.hit.detail.Replace(match.Value, "");
                }
            }
            {
                var match = SpecialRgx.Match(power.Details);
                if (match.Success)
                {
                    powerData.special = match.Groups[1].Value;
                    powerData.hit.detail = powerData.hit.detail.Replace(match.Value, "");
                }
            }
            powerData.hit.detail = powerData.hit.detail.Replace("\r\n", "");
            powerData.level = powerData.basicAttack ? "B" : "";
            
            if (powerData.isMelee)
            {
                resultPower.img = powerData.basicAttack
                    ? "modules/foundry-4e-tools/icons/melee-basic.svg"
                    : "modules/foundry-4e-tools/icons/melee.svg";
            }
            else
            {
                switch (powerData.rangeType)
                {
                    case "melee" : // in theory should not hit this, but just in case
                    case "reach" :  resultPower.img = powerData.basicAttack
                        ? "modules/foundry-4e-tools/icons/melee-basic.svg"
                        : "modules/foundry-4e-tools/icons/melee.svg";
                        break;
                    case "range":
                        resultPower.img = powerData.basicAttack
                            ? "modules/foundry-4e-tools/icons/ranged-basic.svg"
                            : "modules/foundry-4e-tools/icons/ranged.svg";
                        break;
                    case "closeBurst":
                    case "closeBlast":
                        resultPower.img = "modules/foundry-4e-tools/icons/close-blast.svg";
                        break;
                    case "rangeBurst":
                    case "rangeBlast":
                    case "wall":
                        resultPower.img = "modules/foundry-4e-tools/icons/area-burst.svg";
                        break;
                    default:
                        break;
                }
            }

            return resultPower;
        }
        
        private static readonly Regex AreaBurstRgx =
            new Regex(@"area burst ([1-9][0-9]*) within ([1-9][0-9]*)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex CloseBlastRgx =
            new Regex(@"close blast ([1-9][0-9]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex CloseBurstRgx =
            new Regex(@"close burst ([1-9][0-9]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex MeleeNRgx =
            new Regex(@"melee ([1-9][0-9]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex RangedNRgx =
            new Regex(@"ranged ([1-9][0-9]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ReachNRgx =
            new Regex(@"reach ([1-9][0-9]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex Wall1Rgx =
            new Regex(@"wall ([1-9][0-9]*) within ([1-9][0-9]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex Wall2Rgx =
            new Regex(@"wall ([1-9][0-9]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static void ProcessRangeAndWeapon(CreaturePower power, FoundryPowerData data, List<string> errors)
        {
            var range = power.Range;
            var weapon = data.keywords.Contains("weapon") || data.keywords.Contains("Weapon");
            var implement = data.keywords.Contains("implement") || data.keywords.Contains("Implement");
            if (implement)
            {
                data.weaponType = "implement";
            }
            try
            {
                var hasMatched = false;
                {
                    var match = AreaBurstRgx.Match(range);
                    if (match.Success)
                    {
                        data.rangeType = "rangeBurst";
                        data.rangePower = Int32.Parse(match.Groups[2].Value);
                        data.area = Int32.Parse(match.Groups[1].Value);
                        if (weapon)
                        {
                            data.weaponType = "range";
                        }
                        hasMatched = true;
                    }
                }
                {
                    var match = CloseBlastRgx.Match(range);
                    if (match.Success)
                    {
                        data.rangeType = "closeBlast";
                        data.area = Int32.Parse(match.Groups[1].Value);
                        if (weapon)
                        {
                            data.weaponType = "melee";
                        }
                        hasMatched = true;
                    }
                }
                {
                    var match = CloseBurstRgx.Match(range);
                    if (match.Success)
                    {
                        data.rangeType = "closeBurst";
                        data.area = Int32.Parse(match.Groups[1].Value);
                        if (weapon)
                        {
                            data.weaponType = "melee";
                        }
                        hasMatched = true;
                    }
                }
                {
                    var match = MeleeNRgx.Match(range);
                    if (match.Success)
                    {
                        data.rangeType = "melee";
                        data.rangePower = Int32.Parse(match.Groups[1].Value);
                        data.isMelee = true;
                        if (weapon)
                        {
                            data.weaponType = "melee";
                        }
                        hasMatched = true;
                    }
                }
                {
                    var match = RangedNRgx.Match(range);
                    if (match.Success)
                    {
                        data.rangeType = "range";
                        data.rangePower = Int32.Parse(match.Groups[1].Value);
                        if (weapon)
                        {
                            data.weaponType = "range";
                        }
                        hasMatched = true;
                    }
                }
                {
                    var match = ReachNRgx.Match(range);
                    if (match.Success)
                    {
                        data.rangeType = "reach";
                        data.rangePower = Int32.Parse(match.Groups[1].Value);
                        data.isMelee = true;
                        if (weapon)
                        {
                            data.weaponType = "melee";
                        }
                        hasMatched = true;
                    }
                }
                {
                    var match = Wall1Rgx.Match(range);
                    if (match.Success)
                    {
                        data.rangeType = "wall";
                        data.rangePower = Int32.Parse(match.Groups[2].Value);
                        data.area = Int32.Parse(match.Groups[1].Value);
                        hasMatched = true;
                    }
                    else
                    {
                        match = Wall2Rgx.Match(range);
                        if (match.Success)
                        {
                            data.rangeType = "wall";
                            data.area = Int32.Parse(match.Groups[1].Value);
                            hasMatched = true;
                        }
                    }
                }
                
                if (!hasMatched)
                {
                    var rangeLower = range.ToLowerInvariant();
                    if (rangeLower.Contains("personal"))
                    {
                        data.rangeType = "personal";
                    }
                    else if (rangeLower.Contains("touch"))
                    {
                        data.rangeType = "touch";
                        data.isMelee = true;
                    }
                    else if (rangeLower.Contains("melee"))
                    {
                        data.rangeType = "melee";
                        data.rangePower = 1;
                        data.isMelee = true;
                    }
                    // try to work around unset ranges
                    else if (string.IsNullOrEmpty(range))
                    {
                        if ((power.Action.Use == PowerUseType.Basic) || (power.Attack != null))
                        {
                            data.rangeType = "melee";
                            data.rangePower = 1;
                            data.isMelee = true;
                            errors.Add($"Attack {power.Name} did not have range set, assuming melee");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                errors.Add($"Failed to Parse Range: '{range}' for power {power.Name}:" + e.Message);
            }
        }
    }
}