﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using EncounterExport.FoundryHelpers;
using Masterplan.Data;
using Newtonsoft.Json;

namespace EncounterExport
{
    public static class FoundryCreatureHelper
    {
        private const bool DEBUG = false;
        private const string EPIC_DESTINY_POWER_TYPE = "destinyFeats";
        public static FoundryCreatureAndErrors CreateCreature(EncounterCreature encounterCreature)
        {
            /*
             Library Source
             * Creature c = creature as Creature;
        List<string> stringList2 = new List<string>();
        if (c != null)
        {
          Library library = Session.FindLibrary(c);
          if (library != null && library.Name != "" && (Session.Project == null || library != Session.Project.Library))
          {
            string str35 = HTML.Process(library.Name, true);
            stringList2.Add(str35);
          }
        }
             */
            var input = encounterCreature.Creature;
            var inputCard = encounterCreature.Card;
            try
            {
                var output = new FoundryCreatureAndErrors();
                List<string> errors = output.Errors;
                var result = output.Creature.Data;
                var monsterKnowledgeHardDescription = new FoundryPowerDescription();

                result.name = input.Name;
                result.abilities.str.value = input.Strength.Score;
                result.abilities.con.value = input.Constitution.Score;
                result.abilities.dex.value = input.Dexterity.Score;
                result.abilities.intValue.value = input.Intelligence.Score;
                result.abilities.wis.value = input.Wisdom.Score;
                result.abilities.cha.value = input.Charisma.Score;

                result.attributes.hp.value = inputCard.HP;
                result.attributes.init.value = inputCard.Initiative;

                var halfLevel = inputCard.Level / 2;

                result.defences.ac.value = inputCard.AC - halfLevel - Math.Max(input.Dexterity.Modifier, input.Intelligence.Modifier);
                result.defences.fort.value = inputCard.Fortitude - halfLevel - Math.Max(input.Strength.Modifier, input.Constitution.Modifier);
                result.defences.refValue.value = inputCard.Reflex - halfLevel - Math.Max(input.Dexterity.Modifier, input.Intelligence.Modifier);
                result.defences.wil.value = inputCard.Will - halfLevel - Math.Max(input.Wisdom.Modifier, input.Charisma.Modifier);

                var details = result.details;
                details.bloodied = input.HP / 2;
                details.surgeValue = input.HP / 4;
                details.surges.value = (Math.Max(inputCard.Level - 1, 1) / 10) + 1;
                details.origin = input.Origin.ToString().ToLowerInvariant();
                details.typeValue = input.Type.ToString().ToLowerInvariant();
                details.level = inputCard.Level;
                details.size = SizeToString(input.Size);
                details.exp = inputCard.XP;
                details.equipment = input.Equipment;
                
                switch (input.Role)
                {
                    case ComplexRole role:
                        details.role.primary = role.Type.ToString().ToLowerInvariant();
                        switch (role.Type)
                        {
                            case RoleType.Lurker:
                            {
                                var initBonus = new Bonus
                                {
                                    active = true,
                                    name = $"{role.Type} role",
                                    value = 4
                                };
                                result.attributes.init.bonus.Add(initBonus);
                                break;
                            }

                            case RoleType.Skirmisher:
                            case RoleType.Soldier:
                            {
                                var initBonus = new Bonus
                                {
                                    active = true,
                                    name = $"{role.Type} role",
                                    value = 2
                                };
                                result.attributes.init.bonus.Add(initBonus);
                                break;
                            }
                        }
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
                                details.role.secondary = "standard";
                                break;
                        }

                        break;
                    case Minion minion:
                        details.role.secondary = "minion";
                        details.role.primary = minion.Type.ToString().ToLowerInvariant();
                        break;
                    default:
                        details.role.primary = input.Info;
                        details.role.secondary = "standard";
                        break;
                }

                var movetest = inputCard.Movement.Split(',').First();
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
                result.movement.custom = input.Movement;
                details.other = input.Keywords;
                details.alignment = input.Alignment;
                
                var skills = ProcessSkills(inputCard, errors);
                result.skills = skills;

                AddValueToBio(input.Languages, "Languages", result);
                AddValueToBio(input.Tactics, "Tactics", result, monsterKnowledgeHardDescription);

                if (inputCard.Regeneration != null)
                {
                    AddValueToBio(input.Regeneration.ToString(), "Regeneration", result, monsterKnowledgeHardDescription);
                }

                result.senses = FoundrySensesHelper.ProcessSenses(inputCard.Senses);

                ProcessDamageModifiers(inputCard, result, monsterKnowledgeHardDescription);

                AddValueToBio(input.Equipment, "Equipment", result);

                List<FoundryPower> powers = output.Creature.Powers;
                List<FoundryTrait> traits = output.Creature.Traits;
                foreach (var power in inputCard.CreaturePowers)
                {
                    if (power.Category == CreaturePowerCategory.Trait)
                    {
                        traits.Add(ProcessTrait(power, errors));
                    }
                    else
                    {
                        if (power.Attack != null)
                        {
                            powers.Add(FoundryPowerHelper.ProcessAttack(power, errors));
                        }
                        else
                        {
                            powers.Add(FoundryPowerHelper.ProcessAction(power, errors, false));
                        }
                    }
                }
                
                powers.Sort(new PowerComparer());

                var auraTraitList = new List<FoundryTrait>();
                var auras = ProcessAuras(inputCard, result, errors, monsterKnowledgeHardDescription, auraTraitList);
                traits.AddRange(auraTraitList);
                if (auras != null)
                {
                    output.Creature.Token.flags["token-auras"] = auras;
                }
         
                if (DEBUG)
                {
                    output.Creature.creature = input;
                }

                GenerateMonsterKnowledgeBlocks(output, inputCard, monsterKnowledgeHardDescription, input.Size);
                
                var usefulStuff = new FoundryTrait
                {
                    name = "Misc NPC Info",
                    type = EPIC_DESTINY_POWER_TYPE,
                    img = "icons/svg/mystery-man.svg"
                    
                };
                usefulStuff.system.description.value = result.biography;
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
            EncounterCard creature,
            FoundryPowerDescription hardDescription, CreatureSize creatureSize)
        {
            var medKnowledge = new FoundryTrait
            {
                name = "Monster Knowledge (med)",
                type = EPIC_DESTINY_POWER_TYPE,
                img = "icons/svg/book.svg"
                    
            };
            
            
            
            var description = medKnowledge.system.description;
            description.value += $"<h1>{creatureAndErrors.Creature.Name}</h1>\n";
            var titleRemains = creature.Title.Replace(creatureAndErrors.Creature.Name, "");
            if (titleRemains.Trim() != "")
            {
                description.value += $"<h2>{titleRemains.Trim()}</h2>\n";
            }
            var data = creatureAndErrors.Creature.Data;
            var secondaryType = data.details.role.secondary == "standard" ? "" : data.details.role.secondary;
            description.value += $"<p><strong>Role: </strong>level {data.details.level} {creatureSize} {secondaryType} {data.details.role.primary}";
            if (data.details.role.leader)
            {
                description.value += " (leader)</p>\n";
            }
            else
            {
                description.value += "</p>\n";
            }
            description.value += $"<p><strong>Type: </strong>{data.details.origin} {data.details.typeValue}";
            if (string.IsNullOrEmpty(data.details.other))
            {
                description.value += $"</p>\n";
            }
            else
            {
                description.value += $" ({data.details.other})</p>\n";
            }

            description.value += $"<p><strong>Typical Alignment:</strong> {data.details.alignment}</p>\n";
            description.value += $"<p>Ask your DM for typical temperament</p>\n";
            
            var hardKnowledge = new FoundryTrait
            {
                name = "Monster Knowledge (hard)",
                type = EPIC_DESTINY_POWER_TYPE,
                img = "icons/svg/book.svg"
            };
            hardKnowledge.system.description = hardDescription;
            // prefix on the medium stuff
            hardDescription.value = medKnowledge.system.description.value + hardDescription.value;

            hardDescription.value += $"<h2>Powers</h2>\n";
            
            foreach (var power in creatureAndErrors.Creature.Powers)
            {
                hardDescription.value += $"<h3><strong>{power.name} \u2666 {power.system.useType.Replace("atwill","at will")}</strong></h3><p class=\"power-basics\">{power.system.description.chat}</p>\n";
                power.system.description.chat = "";
            }
            hardDescription.value += $"</table>\n";

            if (creatureAndErrors.Creature.Traits.Any())
            {
                hardDescription.value += $"<h2>Traits</h2>\n<table>";
                foreach (var trait in creatureAndErrors.Creature.Traits)
                {
                    hardDescription.value += $"<h3><strong>{trait.name}</strong></h3><p>{trait.system.description.value}</p>\n";
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
                creatureData.biography += $"<strong>{inputValue}</strong>\n";

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
        private static FoundryTrait ProcessTrait(CreaturePower power, List<string> errors)
        {
            var result = new FoundryTrait
            {
                name = power.Name
            };
            // sometimes details are in the range field for traits
            result.system.description.value = "<p>" + (string.IsNullOrEmpty(power.Details) ? power.Range : power.Details) + "</p>";
            return result;
        }

        private static void ProcessDamageModifiers(EncounterCard input, FoundryCreatureData result,
            FoundryPowerDescription monsterKnowledgeHardDescription)
        {
            var immunities = "";
            var resistances = "";
            var vunerables = "";
            if (input.DamageModifiers != null)
            {
                foreach (var mod in input.DamageModifiers)
                {
                    var str = ", " + Math.Abs(mod.Value) + " " + mod.Type;
                    var flippedVal = mod.Value * -1;

                    if (mod.Value > 0)
                    {
                        vunerables += str;
                    }
                    else if (mod.Value < 0)
                    {
                        resistances += str;
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
                            value = flippedVal,
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

                immunities = RemoveComma(immunities);
                resistances = RemoveComma(resistances);
                vunerables = RemoveComma(vunerables);
            }

            string[] splitter = { ", " };
            var immuneList = input.Immune.Split(splitter, StringSplitOptions.RemoveEmptyEntries).ToList();
            var resistList = input.Resist.Split(splitter, StringSplitOptions.RemoveEmptyEntries).ToList();
            var vulnerableList = input.Vulnerable.Split(splitter, StringSplitOptions.RemoveEmptyEntries).ToList();
            result.untypedResistances.resistances = resistList.Select(str => str.Trim()).ToList();
            result.untypedResistances.vulnerabilities = vulnerableList.Select(str => str.Trim()).ToList();
            result.untypedResistances.immunities = immuneList.Select(str => str.Trim()).ToList();
            result.untypedResistances.resistances.RemoveAll(string.IsNullOrEmpty);
            result.untypedResistances.vulnerabilities.RemoveAll(string.IsNullOrEmpty);
            result.untypedResistances.immunities.RemoveAll(string.IsNullOrEmpty);
          
            if (!string.IsNullOrEmpty(input.Immune))
            {
                var immuneStringLower = input.Immune.ToLowerInvariant();
                var damageTypes = Enum.GetValues(typeof(DamageType))
                    .Cast<DamageType>()
                    .Select(x => x.ToString().ToLowerInvariant());
                var damageTypeSet = new HashSet<string>();
                foreach (var damageType in damageTypes)
                {
                    damageTypeSet.Add(damageType);
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
                // try to remove immunities that are themselves damage types without a conditional modifer
                result.untypedResistances.immunities.RemoveAll(str => damageTypeSet.Contains(str.ToLower()));
            }
            
            var immuneStringList = new List<string>
            {
                immunities,
                input.Immune
            };
            AddValuesToBio(immuneStringList, "Immune", result, monsterKnowledgeHardDescription);
            
            var resistStringList = new List<string>
            {
                resistances,
                input.Resist
            };
            
            AddValuesToBio(resistStringList, "Resistances", result, monsterKnowledgeHardDescription);
            
            var vulnerablesStringList = new List<string>
            {
                vunerables,
                input.Vulnerable
            };
            AddValuesToBio(vulnerablesStringList, "Vulnerabilities", result, monsterKnowledgeHardDescription);
            
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

        private static string RemoveComma(string input)
        {
            return input.StartsWith(", ") ? input.Substring(2) : input;
        }

        private static Skills ProcessSkills(EncounterCard input, List<string> errors)
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
            const int bonus = 5;
            skills.ForEach(x =>
            {
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

        private static Dictionary<string, object> ProcessAuras(EncounterCard input, FoundryCreatureData output,
            List<string> errors, FoundryPowerDescription hardDescription, List<FoundryTrait> auraTraits)
        {
            if (input.Auras != null && input.Auras.Any())
            {
                // auras module format is aura1 & 2 are named flags (those are the UI ones), whilst 3+ are programatic only and stored in the auras array.  
                var auras = new Dictionary<string, object>();
                var extraAurasList = new List<FoundryTokenAuraData>();
                auras["auras"] = extraAurasList;
                output.biography += "<h1>Auras</h1>\n";
                hardDescription.value += $"<h2>Auras</h2>\n<table>";
                int index = 0;
                
                foreach (var aura in input.Auras)
                {
                    var removeAuraWord = aura.Details.ToLower().Trim().StartsWith("aura")
                        ? aura.Details.Trim().Substring(4)
                        : aura.Details;
                    var firstPart = Regex.Split(removeAuraWord.Trim(), @"[ :;]")[0];
                    index++;
                    string colour = "#ffffff";
                    switch (index)
                    {
                        case 1: colour = "#FFFF99";
                            break;
                        case 2: colour = "#59E594";
                            break;
                        case 3: colour = "#C27BA0";
                            break;
                    }
                    var foundryAura = new FoundryTokenAuraData
                    {
                        colour = colour
                    };
                    if (Int32.TryParse(firstPart, out var dist))
                    {
                        foundryAura.distance = dist;
                    }
                    else
                    {
                        errors.Add("Unable to parse distance for aura: " + aura.Name + ": '" + aura.Details + "'");
                    }

                    if (index <= 2)
                    {
                        auras[$"aura{index}"] = foundryAura;
                    }
                    else
                    {
                        extraAurasList.Add(foundryAura);
                    }

                    output.biography += $"<h2>{aura.Name}</h2>\n";
                    output.biography += $"<p>{aura.Details}</p>\n";
                    hardDescription.value += $"<tr><td><strong>{aura.Name}</strong></td><td>{aura.Details}</td></tr>\n";

                    var trait = new FoundryTrait()
                    {
                        name = "Aura: " + aura.Name
                    };
                    trait.system.description.value = "<p>" + aura.Details + "</p>";
                    auraTraits.Add(trait);

                }
                hardDescription.value += $"</table>\n";
                return auras;
            }

            return null;
        }
    }
}