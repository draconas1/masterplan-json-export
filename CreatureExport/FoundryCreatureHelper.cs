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
        public static FoundryCreatureAndErrors CreateCreature(EncounterCreature encounterCreature)
        {
            var input = encounterCreature.Creature;
            try
            {
                List<String> errors = new List<string>();
                var result = new FoundryCreature();
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

                var halflevel = input.Level / 2;
                
                result.defences.ac.value = input.AC - halflevel;
                result.defences.fort.value = input.Fortitude - halflevel;
                result.defences.refValue.value = input.Reflex - halflevel;
                result.defences.wil.value = input.Will - halflevel;

                var details = result.details;
                details.bloodied = input.HP / 2;
                details.surgeValue = input.HP / 4;
                details.surges.value = 1;
                details.origin = input.Origin.ToString().ToLowerInvariant();
                details.typeValue = input.Type.ToString().ToLowerInvariant();
                details.level = input.Level;
                details.size = SizeToString(input.Size);
                details.exp = encounterCreature.Card.XP;
               
                // result.Role = input.Info;
               
                switch (input.Role)
                {
                    case ComplexRole role:
                        details.role.primary = role.Type.ToString().ToLowerInvariant();
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
                    result.movement.baseValue.value  = moveDist;
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

                addValueToBio(input.Languages, "Languages", result);
                addValueToBio(input.Tactics, "Tactics", result);
              
                
                if  (input.Regeneration != null)
                {
                    addValueToBio(input.Regeneration.ToString(), "Regeneration", result);
                }

                result.senses = ProcessSenses(input.Senses);
          
                ProcessAuras(input, result, errors);
                
                // result.Details = input.Details;
                // result.Origin = input.Origin;
                // result.FullTypeDesc = input.Phenotype;
              
                // result.Equipment = input.Equipment;
                // result.Category = input.Category;

                // result.Resist = input.Resist;
                // result.Vulnerable = input.Vulnerable;
                // result.Immune = input.Immune;
                
               

      
                //
                // ProcessDamageModifiers(input, result);
                //
                // List<Power> powers = new List<Power>();
                // List<Power> traits = new List<Power>();
                // result.CreaturePowers = powers;
                // result.CreatureTraits = traits;
                // foreach (var power in input.CreaturePowers)
                // {
                //     var resultPower = new Power {PowerCard = power.Copy()};
                //     if (power.Category == CreaturePowerCategory.Trait)
                //     {
                //         traits.Add(resultPower);
                //         if (resultPower.PowerCard.Details == "")
                //         {
                //             resultPower.PowerCard.Details = resultPower.PowerCard.Range;
                //         }
                //     }
                //     else
                //     {
                //         powers.Add(resultPower);
                //     }
                //
                //     if (power.Attack != null)
                //     {
                //         ProcessAttack(resultPower, power, errors);
                //     }
                // }
                //
                // powers.Sort();
                
                var output = new FoundryCreatureAndErrors();
                output.Creature = result;
                output.Errors = errors;
                
                return output;
            }
            catch (Exception e)
            {
                throw new Exception("Error mapping creature " + input.Name, e);
            }
        }

        private static void addValueToBio(string inputValue, string header, FoundryCreature creature)
        {
            if (!string.IsNullOrEmpty(inputValue))
            {
                creature.biography += "<h1>" + header + "</h1>\n";
                creature.biography += "<p>" + inputValue + "</p>\n";
            }
        }

        private static readonly Regex blindsightRgx = new Regex(@"blindsight ([1-9][0-9]*)",RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex tremorsenseRgx = new Regex(@"tremorsense ([1-9][0-9]*)",RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
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
            
            var bsMatch = blindsightRgx.Match(inputSenseLower);
            if (bsMatch.Success)
            {
                var range = bsMatch.Groups[1].Value;
                var ll = new List<string>();
                ll.Add("bs");
                ll.Add(range);
                senses.special.value.Add(ll);
                inputSenseLower = inputSenseLower.Replace(bsMatch.Value, "");
            }

            var tsMatch = tremorsenseRgx.Match(inputSenseLower);
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

        private static void ProcessAttack(Power resultPower, CreaturePower power, List<string> errors)
        {
            resultPower.Damage = CommonHelpers.parseDamageString(power.Damage, errors, power.Name);
            var entireMatch = CommonHelpers.entireDamageStrRx.Match(power.Details);
            if (entireMatch.Success)
            {
                resultPower.Damage.Raw = power.Damage;
                var match = entireMatch.Groups[0].Value; // get the entire match group
                var newDamageString =
                    CommonHelpers.entireDamageStrRx.Replace(power.Details, "[[" + match + "]]");
                resultPower.PowerCard.Details = newDamageString;
            }
        }

        private static void ProcessDamageModifiers(ICreature input, OutputCreature result)
        {
            if (input.DamageModifiers != null)
            {
                var resistances = "";
                var vunerables = "";
                foreach (var mod in input.DamageModifiers)
                {
                    var str = ", " + mod.Value + " " + mod.Type;
                    if (mod.Value == 0)
                    {
                        continue;
                    }

                    if (mod.Value < 0)
                    {
                        resistances = resistances + str;
                    }
                    else
                    {
                        vunerables = vunerables + str;
                        ;
                    }
                }

                result.Resist += resistances;
                result.Vulnerable += vunerables;
                result.Resist = removeComma(result.Resist);
                result.Vulnerable = removeComma(result.Vulnerable);
            }
        }

        private static string SizeToString(CreatureSize size)
        {
            switch (size)
            {
                case CreatureSize.Gargantuan : return "grg";
                case CreatureSize.Huge : return "huge";
                case CreatureSize.Large : return "lg";
                case CreatureSize.Medium : return "med";
                case CreatureSize.Small : return "sm";
                case CreatureSize.Tiny : return "tiny";
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
                    errors.Add("Error converting skill [" + string.Join(",", array.Select(x => "'" + x + "'").ToArray()) + "] from '" + input.Skills + "'");
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
                        errors.Add("Error converting skill (error parsing output) [" + string.Join(",", array.Select(x => "'" + x + "'").ToArray()) + "] from '" + input.Skills + "'");
                    }
                }
            }

            var skillsHolder = new Skills();
            skills.ForEach(x =>
            {
                var bonus = 5;
                switch (x.Name.ToLowerInvariant())
                {
                    case "acrobatics" : skillsHolder.acr.value = bonus;
                        break;
                    case "arcana" : skillsHolder.arc.value = bonus;
                        break;
                    case "athletics" : skillsHolder.ath.value = bonus;
                        break;
                    case "bluff" : skillsHolder.blu.value = bonus;
                        break;
                    case "diplomacy" : skillsHolder.dip.value = bonus;
                        break;
                    case "dungeoneering" : skillsHolder.dun.value = bonus;
                        break;
                    case "endurance" : skillsHolder.end.value = bonus;
                        break;
                    case "heal" : skillsHolder.hea.value = bonus;
                        break;
                    case "history" : skillsHolder.his.value = bonus;
                        break;
                    case "insight" : skillsHolder.ins.value = bonus;
                        break;
                    case "intimidate" : skillsHolder.itm.value = bonus;
                        break;
                    case "nature" : skillsHolder.nat.value = bonus;
                        break;
                    case "perception" : skillsHolder.prc.value = bonus;
                        break;
                    case "religion" : skillsHolder.rel.value = bonus;
                        break;
                    case "stealth" : skillsHolder.stl.value = bonus;
                        break;
                    case "streetwise" : skillsHolder.stw.value = bonus;
                        break;
                    case "thievery" : skillsHolder.thi.value = bonus;
                        break;
                }
            });

            return skillsHolder;
        }

        private static List<NameDescValue> ProcessAuras(ICreature input, FoundryCreature output, List<String> errors)
        {
            if (input.Auras != null)
            {
                var auras = new List<NameDescValue>();
                output.biography += "<h1>Auras</h1>\n";
                foreach (var aura in input.Auras)
                {
                    var removeAuraWord = aura.Details.ToLower().Trim().StartsWith("aura") ? aura.Details.Trim().Substring(4) : aura.Details;
                    var firstPart = Regex.Split( removeAuraWord.Trim(), @"[ :;]")[0];
                 
                    if (Int32.TryParse(firstPart, out var dist))
                    {
                        auras.Add(new NameDescValue(aura.Name, aura.Details, dist));
                    }
                    else
                    {
                        auras.Add(new NameDescValue(aura.Name, aura.Details, 1));
                        errors.Add("Unable to parse distance for aura: " + aura.Name + ": '" + aura.Details + "'");
                    }
                    output.biography += "<p>" + aura + "</p>\n";
                }

                return auras;
            }

            return null;
        }
    }
    
    public class FoundryCreatureAndErrors
    {
        public FoundryCreature Creature { get; set; }
        public List<String> Errors { get; set; }
        public bool HasError => Errors != null && Errors.Count > 0;
        public string Name => Creature.name;
    }

    public class FoundryCreature
    {
        public string name { get; set; }
        public Abilities abilities { get; set; } = new Abilities();
        public Attributes attributes { get; set; } = new Attributes();
        public Defences defences { get; set; } = new Defences();

        public bool advancedCals { get; set; } = true;
        
        public Details details { get; set; } = new Details();


        public IntValueHolder actionpoints { get; set; } = new IntValueHolder();

        public Movement movement { get; set; } = new Movement();

        public Skills skills { get; set; } = new Skills();

        public string biography { get; set; } = "";

        public Senses senses { get; set; } = new Senses();

        public object original { get; set; }
    }

    public class Movement
    {
        [JsonProperty("base")]
        public IntValueHolderWithBase baseValue { get; set; } = new IntValueHolderWithBase();
        
        public string notes { get; set; }
    }
    public class Abilities
    {
        public IntValueHolder str { get; set; } = new IntValueHolder();
        public IntValueHolder con { get; set; } = new IntValueHolder();
        public IntValueHolder dex { get; set; } = new IntValueHolder();
        [JsonProperty("int")]
        public IntValueHolder intValue { get; set; } = new IntValueHolder();
        public IntValueHolder wis { get; set; } = new IntValueHolder();
        public IntValueHolder cha { get; set; } = new IntValueHolder();
    }

    public class Skills
    {
        public IntValueHolder acr { get; set; } = new IntValueHolder();
        public IntValueHolder arc { get; set; } = new IntValueHolder();
        public IntValueHolder ath { get; set; } = new IntValueHolder();
        public IntValueHolder blu { get; set; } = new IntValueHolder();
        public IntValueHolder dip { get; set; } = new IntValueHolder();
        public IntValueHolder dun { get; set; } = new IntValueHolder();
        public IntValueHolder end { get; set; } = new IntValueHolder();
        public IntValueHolder hea { get; set; } = new IntValueHolder();
        public IntValueHolder his { get; set; } = new IntValueHolder();
        public IntValueHolder ins { get; set; } = new IntValueHolder();
        public IntValueHolder itm { get; set; } = new IntValueHolder();
        public IntValueHolder nat { get; set; } = new IntValueHolder();
        public IntValueHolder prc { get; set; } = new IntValueHolder();
        public IntValueHolder rel { get; set; } = new IntValueHolder();
        public IntValueHolder stl { get; set; } = new IntValueHolder();
        public IntValueHolder stw { get; set; } = new IntValueHolder();
        public IntValueHolder thi { get; set; } = new IntValueHolder();
    }

    public class IntValueHolder
    {
        public int value { get; set; }
    }

    public class IntValueHolderWithMax : IntValueHolder
    {
        public int max => value;
    }

    public class IntValueHolderWithBase : IntValueHolder
    {
        [JsonProperty("base")]
        public int baseValue => value;
    }

    public class IntValueWithBonuses : IntValueHolder
    {
        public List<Bonus> bonus { get; set; } = new List<Bonus>();
    }

    public class Attributes
    {
        public IntValueHolderWithMax hp { get; set; } = new IntValueHolderWithMax();
        public IntValueHolder init { get; set; } = new IntValueHolder();
    }

    public class Defence : IntValueHolder
    {
        [JsonProperty("base")]
        public int baseValue => value;
    }

    public class Defences
    {
        public Defence ac { get; set; } = new Defence();
        public Defence fort { get; set; } = new Defence();
        [JsonProperty("ref")]
        public Defence refValue { get; set; } = new Defence();
        public Defence wil { get; set; } = new Defence();
    }

    public class Senses
    {
        public Sense vision { get; set; } = new Sense();
        public Sense special { get; set; } = new Sense();
    }

    public class Sense
    {
        public List<List<string>> value { get; set; } = new List<List<string>>();
        public string custom { get; set; }
    }

    public class Details
    {
        public string origin { get; set; }
        [JsonProperty("type")]
        public string typeValue { get; set; }
        public string other { get; set; }
        public int level { get; set; } = 1;
        public int exp { get; set; }
        public int bloodied { get; set; }
        public int surgeValue { get; set; }
        public string size { get; set; } = "med";
        public IntValueHolderWithMax surges { get; set; } = new IntValueHolderWithMax();
        public IntValueWithBonuses saves { get; set; } = new IntValueWithBonuses();
        public string alignment { get; set; }
        public Role role { get; set; } = new Role();
    }

    public class Role
    {
        public string primary { get; set; }
        public string secondary { get; set; }
    }

    public class Bonus : IntValueHolder
    {
        public string name { get; set; }
        public bool active { get; set; } = true;
        public string note { get; set; }
    }
    
    /*
     * 
     */
    public class FoundryPower
    {
        public string name { get; set; }
        public string type { get; set; } = "power";
        public string img { get; set; } = "icons/svg/item-bag.svg";
        public FoundryPowerData data { get; set; } = new FoundryPowerData();
    }

    public class FoundryPowerData
    {
        public FoundryPowerDescription description { get; set; } = new FoundryPowerDescription();
        public string source { get; set; }
        public FoundryPowerActivation activation = new FoundryPowerActivation();
        public FoundryPowerDuration duration = new FoundryPowerDuration();
        public string target { get; set; }
        public FoundryPowerRange range { get; set; } = new FoundryPowerRange();
        public string actionType { get; set; }
        public int attackBonus { get; set; } = 0;
        public string chatFlavor { get; set; } = "";
        public string level { get; set; } = "1";
        public string powersource { get; set; }
        public string subName { get; set; }
        public bool prepared { get; set; } = true;
        public string powerType { get; set; } = "class";
        public string useType { get; set; }
        public string requirements { get; set; } = "";
        public string weaponType { get; set; } = "none";
        public string weaponUse { get; set; } = "none";
        public string rangeType { get; set; } = "";
        public string rangeTextShort { get; set; } = "";
        public string rangeText { get; set; } = "";
        public object rangePower { get; set; }
        public int area { get; set; } = 0;
        public string rechargeRoll { get; set; } = "";
        public bool damageShare { get; set; } = false;
        public bool postEffect { get; set; } = true;
        public bool postSpecial { get; set; } = true;
        public bool autoGenChatPowerCard { get; set; } = true;
        
    }

    public class FoundryPowerDescription
    {
        public string value { get; set; } = "";
        public string chat { get; set; } = "";
        public string unidentified { get; set; } = "";
    }

    public class FoundryPowerActivation
    {
        public string type { get; set; } = "";
        public int cost { get; set; }
        public string condition { get; set; }
    }

    public class FoundryPowerDuration
    {
        public string value { get; set; }
        public string units { get; set; } = "";
    }
    
    public class FoundryPowerRange
    {
        public int? value { get; set; }
        [JsonProperty("long")]
        public int? longRange { get; set; }
        public string units { get; set; } = "";
    }
}