using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Text.RegularExpressions;
using Masterplan.Data;
using Newtonsoft.Json;

namespace EncounterExport
{
    public static class FoundryCreatureHelper
    {
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

        private static readonly HashSet<string> ElementalDamageTypeSet = new HashSet<string>(ElementalDamageTypes);

        public static FoundryCreatureAndErrors CreateCreature(EncounterCreature encounterCreature)
        {
            var input = encounterCreature.Creature;
            try
            {
                List<String> errors = new List<string>();
                var result = new FoundryCreatureData();
                var monsterKnowledgeHardDescription = new FoundryPowerDescription();
                //result.original = input;
                result.name = input.Name;
                result.abilities.str.value = input.Strength.Score;
                result.abilities.con.value = input.Constitution.Score;
                result.abilities.dex.value = input.Dexterity.Score;
                result.abilities.intValue.value = input.Intelligence.Score;
                result.abilities.wis.value = input.Wisdom.Score;
                result.abilities.cha.value = input.Charisma.Score;

                result.attributes.hp.value = input.HP;
                result.attributes.init.value = input.Initiative;

                var halfLevel = input.Level / 2;

                result.defences.ac.value = input.AC - halfLevel;
                result.defences.fort.value = input.Fortitude - halfLevel;
                result.defences.refValue.value = input.Reflex - halfLevel;
                result.defences.wil.value = input.Will - halfLevel;

                var details = result.details;
                details.bloodied = input.HP / 2;
                details.surgeValue = input.HP / 4;
                details.surges.value = 1;
                details.origin = input.Origin.ToString().ToLowerInvariant();
                details.typeValue = input.Type.ToString().ToLowerInvariant();
                details.level = input.Level;
                details.size = SizeToString(input.Size);
                details.exp = encounterCreature.Card.XP;

                switch (input.Role)
                {
                    case ComplexRole role:
                        details.role.primary = role.Type.ToString().ToLowerInvariant();
                        details.role.leader = role.Leader;
                        switch (role.Flag)
                        {
                            case RoleFlag.Elite:
                                var eliteBonus = new Bonus
                                {
                                    name = "Elite",
                                    value = 2
                                };

                                details.saves.bonus.Add(eliteBonus);
                                result.actionpoints.value = 1;
                                details.role.secondary = "elite";
                                break;
                            case RoleFlag.Solo:
                                result.actionpoints.value = 2;
                                details.role.secondary = "solo";
                                var soloBonus = new Bonus
                                {
                                    name = "Solo",
                                    value = 5
                                };

                                details.saves.bonus.Add(soloBonus);
                                break;
                            case RoleFlag.Standard:
                                details.role.secondary = "regular";
                                break;
                        }

                        break;
                    case Minion minion:
                        details.role.secondary = "minion";
                        details.role.primary = minion.Type.ToString().ToLowerInvariant();
                        break;
                    default:
                        details.role.primary = input.Info;
                        details.role.secondary = "regular";
                        break;
                }

                var movetest = input.Movement.Split(',').First();
                if (Int32.TryParse(movetest.Trim(), out var moveDist))
                {
                    result.movement.baseValue.value = moveDist;
                }
                else
                {
                    var movetest2 = input.Movement.Split(' ').First();
                    if (Int32.TryParse(movetest2.Trim(), out moveDist))
                    {
                        result.movement.baseValue.value = moveDist;
                    }
                    else
                    {
                        errors.Add("Unable to parse base movement distance from: " + input.Movement);
                    }
                }

                result.movement.notes = input.Movement;
                details.other = input.Keywords;
                details.alignment = input.Alignment;

                var skills = ProcessSkills(input, errors);
                result.skills = skills;

                AddValueToBio(input.Languages, "Languages", result);
                AddValueToBio(input.Tactics, "Tactics", result, monsterKnowledgeHardDescription);

                if (input.Regeneration != null)
                {
                    AddValueToBio(input.Regeneration.ToString(), "Regeneration", result, monsterKnowledgeHardDescription);
                }

                result.senses = ProcessSenses(input.Senses);

                result.auras = ProcessAuras(input, result, errors);

                ProcessDamageModifiers(input, result, monsterKnowledgeHardDescription);

                AddValueToBio(input.Equipment, "Equipment", result);

                // result.creature = input;
                // result.card = encounterCreature.Card;

                // result.Details = input.Details;
                // result.Origin = input.Origin;
                // result.FullTypeDesc = input.Phenotype;

                // result.Category = input.Category;


                List<FoundryPower> powers = new List<FoundryPower>();
                List<FoundryTrait> traits = new List<FoundryTrait>();

                foreach (var power in input.CreaturePowers)
                {
                    if (power.Category == CreaturePowerCategory.Trait)
                    {
                        traits.Add(ProcessTrait(power, errors));
                    }
                    else
                    {
                        if (power.Attack != null)
                        {
                            powers.Add(ProcessAttack(power, errors));
                        }
                        else
                        {
                            powers.Add(ProcessAction(power, errors, false));
                        }
                    }
                }
                
                powers.Sort(new PowerComparer());

                var output = new FoundryCreatureAndErrors
                {
                    Creature =
                    {
                        Data = result,
                        Powers = powers,
                        Traits = traits
                    },
                    Errors = errors
                };

                GenerateMonsterKnowledgeBlocks(output, monsterKnowledgeHardDescription);
                
                var usefulStuff = new FoundryTrait
                {
                    name = "Misc NPC Info",
                    type = "classFeats",
                    img = "icons/svg/mystery-man.svg"
                    
                };
                usefulStuff.data.description.value = result.biography;
                result.biography = "";
                traits.Add(usefulStuff);

                return output;
            }
            catch (Exception e)
            {
                throw new Exception("Error mapping creature " + input.Name, e);
            }
        }

        private static void GenerateMonsterKnowledgeBlocks(FoundryCreatureAndErrors creatureAndErrors,
            FoundryPowerDescription hardDescription)
        {
            var medKnowledge = new FoundryTrait
            {
                name = "Monster Knowledge (med): ",
                type = "classFeats",
                img = "icons/svg/book.svg"
                    
            };
            var description = medKnowledge.data.description;
            description.value += $"<h1>{creatureAndErrors.Name}</h1>\n";
            var data = creatureAndErrors.Creature.Data;
            var secondaryType = data.details.role.secondary == "regular" ? "" : data.details.role.secondary;
            description.value += $"<p><b>Role: </b>level {data.details.level} {secondaryType} {data.details.role.primary}";
            if (data.details.role.leader)
            {
                description.value += " (leader)</p>\n";
            }
            else
            {
                description.value += "</p>\n";
            }
            description.value += $"<p><b>Type: </b>{data.details.origin} {data.details.typeValue}";
            if (string.IsNullOrEmpty(data.details.other))
            {
                description.value += $"</p>\n";
            }
            else
            {
                description.value += $" ({data.details.other})</p>\n";
            }

            description.value += $"<p><b>Typical Alignment:</b> {data.details.alignment}</p>\n";
            description.value += $"<p>Ask your DM for typical temperament</p>\n";
            
            var hardKnowledge = new FoundryTrait
            {
                name = "Monster Knowledge (hard): ",
                type = "classFeats",
                img = "icons/svg/book.svg"
            };
            hardKnowledge.data.description = hardDescription;
            // prefix on the medium stuff
            hardDescription.value = medKnowledge.data.description.value + hardDescription.value;

            hardDescription.value += $"<h2>Powers</h2>\n<table>";
            
            foreach (var power in creatureAndErrors.Creature.Powers)
            {
                hardDescription.value += $"<tr><td><b>{power.name}</b></td><td>{power.data.description.chat}</td></tr>\n";
                power.data.description.chat = "";
            }
            hardDescription.value += $"</table>\n";

            if (creatureAndErrors.Creature.Traits.Any())
            {
                hardDescription.value += $"<h2>Traits</h2>\n<table>";
                foreach (var trait in creatureAndErrors.Creature.Traits)
                {
                    hardDescription.value += $"<tr><td><b>{trait.name}</b></td><td>{trait.data.description.value}</td></tr>\n";
                }
                hardDescription.value += $"</table>\n";
            }
            
            if (creatureAndErrors.Creature.Data.auras != null && creatureAndErrors.Creature.Data.auras.Any())
            {
                hardDescription.value += $"<h2>Auras</h2>\n<table>";
                foreach (var aura in creatureAndErrors.Creature.Data.auras)
                {
                    hardDescription.value += $"<tr><td><b>{aura.Name}</b></td><td>{aura.Desc}</td></tr>\n";
                }
                hardDescription.value += $"</table>\n";
            }
            
            creatureAndErrors.Creature.Traits.Add(medKnowledge);
            creatureAndErrors.Creature.Traits.Add(hardKnowledge);
        }

        private static void AddValueToBio(string inputValue, string header, FoundryCreatureData creatureData, FoundryPowerDescription monsterKnowledgeHardDescription = null)
        {
            if (!string.IsNullOrEmpty(inputValue))
            {
                creatureData.biography += $"<h1>{header}</h1>\n";
                creatureData.biography += $"<p>{inputValue}</p>\n";

                if (monsterKnowledgeHardDescription != null)
                {
                    monsterKnowledgeHardDescription.value += $"<h2>{header}</h2>\n";
                    monsterKnowledgeHardDescription.value += $"<p>{inputValue}</p>\n";
                }
            }
        }

        private static void AddValuesToBio(IList<string> inputValues, string header,
            FoundryCreatureData creatureData, FoundryPowerDescription monsterKnowledgeHardDescription = null)
        {
            if (inputValues.Any(x => !string.IsNullOrEmpty(x)))
            {
                creatureData.biography += $"<h1>{header}</h1>\n";
                if (monsterKnowledgeHardDescription != null)
                {
                    monsterKnowledgeHardDescription.value += $"<h2>{header}</h2>\n";
                }
                foreach (var value in inputValues.Where(x => !string.IsNullOrEmpty(x)))
                {
                    creatureData.biography += $"<p>{value}</p>\n";
                    if (monsterKnowledgeHardDescription != null)
                    {
                        monsterKnowledgeHardDescription.value += $"<p>{value}</p>\n";
                    }
                }
            }
        }

        private static readonly Regex BlindsightRgx =
            new Regex(@"blindsight ([1-9][0-9]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex TremorsenseRgx =
            new Regex(@"tremorsense ([1-9][0-9]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static Senses ProcessSenses(string inputSenses)
        {
            var senses = new Senses();
            var inputSenseLower = inputSenses.ToLowerInvariant();
            if (inputSenseLower.Contains("low-light"))
            {
                var ll = new List<string>();
                ll.Add("lv");
                ll.Add("");
                senses.special.value.Add(ll);
                inputSenseLower = inputSenseLower.Replace("low-light vision", "").Replace("low-light", "");
            }

            if (inputSenseLower.Contains("darkvision"))
            {
                var ll = new List<string>();
                ll.Add("dv");
                ll.Add("");
                senses.special.value.Add(ll);
                inputSenseLower = inputSenseLower.Replace("darkvision", "");
            }

            var bsMatch = BlindsightRgx.Match(inputSenseLower);
            if (bsMatch.Success)
            {
                var range = bsMatch.Groups[1].Value;
                var ll = new List<string>();
                ll.Add("bs");
                ll.Add(range);
                senses.special.value.Add(ll);
                inputSenseLower = inputSenseLower.Replace(bsMatch.Value, "");
            }

            var tsMatch = TremorsenseRgx.Match(inputSenseLower);
            if (tsMatch.Success)
            {
                var range = tsMatch.Groups[1].Value;
                var ll = new List<string>();
                ll.Add("ts");
                ll.Add(range);
                senses.special.value.Add(ll);
                inputSenseLower = inputSenseLower.Replace(tsMatch.Value, "");
            }

            senses.special.custom = inputSenseLower;
            return senses;
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

        private static void ProcessRangeAndWeapon(string range, FoundryPowerData data, string power,
            List<string> errors)
        {
            bool weapon = data.keywords.Contains("weapon") || data.keywords.Contains("Weapon");
            bool implement = data.keywords.Contains("implement") || data.keywords.Contains("Implement");
            if (implement)
            {
                data.weaponType = "implement";
            }

            try
            {
                bool hasMatched = false;
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
                        data.rangeType = "weapon";
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
                        data.rangeType = "weapon";
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
                        if (weapon)
                        {
                            data.weaponType = "melee";
                        }
                    }
                    else if (rangeLower.Contains("melee"))
                    {
                        data.rangeType = "weapon";
                        data.isMelee = true;
                        if (weapon)
                        {
                            data.weaponType = "melee";
                        }
                    }
                }

                if (data.weaponType != "none")
                {
                    data.weaponUse = "default";
                }
            }
            catch (Exception e)
            {
                errors.Add("Failed to Parse Range: '" + range + "' for power " + power + ":" + e.Message);
            }
        }


        private static FoundryTrait ProcessTrait(CreaturePower power, List<string> errors)
        {
            var result = new FoundryTrait
            {
                name = power.Name
            };
            // sometimes details are in the range field for traits
            result.data.description.value = string.IsNullOrEmpty(power.Details) ? power.Range : power.Details;
            return result;
        }

        private static FoundryPower ProcessAction(CreaturePower power, List<string> errors,
            bool attackPower)
        {
            var resultPower = new FoundryPower
            {
                name = power.Name
            };
            var powerData = resultPower.data;
            powerData.keywords = power.Keywords.Split(CommonHelpers.separator, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim()).ToList();

            foreach (var powerDataKeyword in powerData.keywords)
            {
                powerData.damageType.Add(powerDataKeyword.ToLowerInvariant(), true);
                powerData.effectType.Add(powerDataKeyword.ToLowerInvariant(), true);
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
            powerData.rechargeRoll = power.Action.Recharge;

            powerData.chatFlavor = power.Description;
            powerData.sustain.actionType = power.Action.SustainAction.ToString().ToLowerInvariant();

            // sometimes details are in the range field for traits
            powerData.effect.detail = string.IsNullOrEmpty(power.Details) ? power.Range : power.Details;
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

        private static FoundryPower ProcessAttack(CreaturePower power, List<string> errors)
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

            ProcessRangeAndWeapon(power.Range, powerData, power.Name, errors);
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

        private static void ProcessDamageModifiers(ICreature input, FoundryCreatureData result,
            FoundryPowerDescription monsterKnowledgeHardDescription)
        {
            var immunities = "";
            if (input.DamageModifiers != null)
            {
                var resistances = "";
                var vunerables = "";
                foreach (var mod in input.DamageModifiers)
                {
                    var str = ", " + mod.Value + " " + mod.Type;

                    if (mod.Value < 0)
                    {
                        resistances += str;
                    }
                    else if (mod.Value > 0)
                    {
                        vunerables += str;
                    }


                    var damageMod = new DamageMod();

                    if (mod.Value == 0)
                    {
                        damageMod.immune = true;
                        immunities += ", " + mod.Type;
                    }
                    else
                    {
                        var bonus = new Bonus()
                        {
                            value = mod.Value,
                            name = "Monster"
                        };
                        damageMod.bonus.Add(bonus);
                    }

                    switch (mod.Type)
                    {
                        case DamageType.Untyped:
                            result.resistances.Add("damage", damageMod);
                            break;
                        default:
                            result.resistances.Add(mod.Type.ToString().ToLowerInvariant(), damageMod);
                            break;
                    }
                }

                immunities = removeComma(immunities);
                AddValueToBio(removeComma(resistances), "Resistances", result, monsterKnowledgeHardDescription);
                AddValueToBio(removeComma(vunerables), "Vulnerabilities", result, monsterKnowledgeHardDescription);
            }

            var immuneStringList = new List<string>();
            immuneStringList.Add(immunities);
            immuneStringList.Add(input.Immune);
            AddValuesToBio(immuneStringList, "Immune", result, monsterKnowledgeHardDescription);

            if (!string.IsNullOrEmpty(input.Immune))
            {
                var immuneStringLower = input.Immune.ToLowerInvariant();
                var damageTypes = Enum.GetValues(typeof(DamageType))
                    .Cast<DamageType>()
                    .Select(x => x.ToString().ToLowerInvariant());
                foreach (var damageType in damageTypes)
                {
                    if (immuneStringLower.Contains(damageType))
                    {
                        if (result.resistances.ContainsKey(damageType))
                        {
                            result.resistances[damageType].immune = true;
                        }
                        else
                        {
                            result.resistances.Add(damageType, new DamageMod()
                            {
                                immune = true
                            });
                        }
                    }
                }
            }
        }

        private static string SizeToString(CreatureSize size)
        {
            switch (size)
            {
                case CreatureSize.Gargantuan: return "grg";
                case CreatureSize.Huge: return "huge";
                case CreatureSize.Large: return "lg";
                case CreatureSize.Medium: return "med";
                case CreatureSize.Small: return "sm";
                case CreatureSize.Tiny: return "tiny";
                default: return "med";
            }
        }

        private static string removeComma(string input)
        {
            if (input.StartsWith(", "))
            {
                return input.Substring(2);
            }

            return input;
        }

        private static Skills ProcessSkills(ICreature input, List<string> errors)
        {
            var skillArray = Regex.Split(input.Skills, @"[\,\;]")
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();
            var skillAndBonus = skillArray.Select(x => Regex.Split(x, @"[\+ ]")).ToList();
            List<NameValue> skills = new List<NameValue>();
            foreach (var array in skillAndBonus)
            {
                if (array.Length < 2)
                {
                    errors.Add("Error converting skill [" +
                               string.Join(",", array.Select(x => "'" + x + "'").ToArray()) + "] from '" +
                               input.Skills + "'");
                }
                else
                {
                    try
                    {
                        if (array.Length == 2)
                        {
                            skills.Add(new NameValue(array[0].Trim(), Int32.Parse(array[1].Trim())));
                        }
                        else if (array.Length == 3)
                        {
                            skills.Add(new NameValue(array[0].Trim(), Int32.Parse(array[2].Trim())));
                        }
                        else
                        {
                            throw new Exception("unknown array size");
                        }
                    }
                    catch (Exception)
                    {
                        errors.Add("Error converting skill (error parsing output) [" +
                                   string.Join(",", array.Select(x => "'" + x + "'").ToArray()) + "] from '" +
                                   input.Skills + "'");
                    }
                }
            }

            var skillsHolder = new Skills();
            skills.ForEach(x =>
            {
                var bonus = 5;
                switch (x.Name.ToLowerInvariant())
                {
                    case "acrobatics":
                        skillsHolder.acr.value = bonus;
                        break;
                    case "arcana":
                        skillsHolder.arc.value = bonus;
                        break;
                    case "athletics":
                        skillsHolder.ath.value = bonus;
                        break;
                    case "bluff":
                        skillsHolder.blu.value = bonus;
                        break;
                    case "diplomacy":
                        skillsHolder.dip.value = bonus;
                        break;
                    case "dungeoneering":
                        skillsHolder.dun.value = bonus;
                        break;
                    case "endurance":
                        skillsHolder.end.value = bonus;
                        break;
                    case "heal":
                        skillsHolder.hea.value = bonus;
                        break;
                    case "history":
                        skillsHolder.his.value = bonus;
                        break;
                    case "insight":
                        skillsHolder.ins.value = bonus;
                        break;
                    case "intimidate":
                        skillsHolder.itm.value = bonus;
                        break;
                    case "nature":
                        skillsHolder.nat.value = bonus;
                        break;
                    case "perception":
                        skillsHolder.prc.value = bonus;
                        break;
                    case "religion":
                        skillsHolder.rel.value = bonus;
                        break;
                    case "stealth":
                        skillsHolder.stl.value = bonus;
                        break;
                    case "streetwise":
                        skillsHolder.stw.value = bonus;
                        break;
                    case "thievery":
                        skillsHolder.thi.value = bonus;
                        break;
                }
            });

            return skillsHolder;
        }

        private static List<NameDescValue> ProcessAuras(ICreature input, FoundryCreatureData output,
            List<String> errors)
        {
            if (input.Auras != null && input.Auras.Any())
            {
                var auras = new List<NameDescValue>();
                output.biography += "<h1>Auras</h1>\n";
                foreach (var aura in input.Auras)
                {
                    var removeAuraWord = aura.Details.ToLower().Trim().StartsWith("aura")
                        ? aura.Details.Trim().Substring(4)
                        : aura.Details;
                    var firstPart = Regex.Split(removeAuraWord.Trim(), @"[ :;]")[0];

                    if (Int32.TryParse(firstPart, out var dist))
                    {
                        auras.Add(new NameDescValue(aura.Name, aura.Details, dist));
                    }
                    else
                    {
                        auras.Add(new NameDescValue(aura.Name, aura.Details, 1));
                        errors.Add("Unable to parse distance for aura: " + aura.Name + ": '" + aura.Details + "'");
                    }

                    output.biography += "<h2>" + aura.Name + "</h2>\n";
                    output.biography += "<p>" + aura.Details + "</p>\n";
                }

                return auras;
            }

            return null;
        }
    }
}