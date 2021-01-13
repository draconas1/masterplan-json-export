using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Masterplan.Data;

namespace EncounterExport
{
    public static class CreatureHelper
    {
        private static readonly Regex entireDamageStrRx = new Regex(@"([1-9][0-9]*)d([12468][02]*)([ ]*\+[ ]*([1-9][0-9]*)*)*",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
        public static CreatureAndErrors createCreature(ICreature input)
        {
            try
            {
                List<String> errors = new List<string>();
                var result = new OutputCreature();
                result.Name = input.Name;
                result.Details = input.Details;
                result.Size = input.Size;
                result.Origin = input.Origin;
                result.Type = input.Type;
                result.Keywords = input.Keywords;
                result.Level = input.Level;
                result.FullTypeDesc = input.Phenotype;
                result.Role = input.Info;
                result.Senses = input.Senses;
                result.Movement = input.Movement;
                result.Alignment = input.Alignment;
                result.Languages = input.Languages;
                result.Equipment = input.Equipment;
                result.Category = input.Category;
                result.AC = input.AC;
                result.Fortitude = input.Fortitude;
                result.Reflex = input.Reflex;
                result.Will = input.Will;
                result.Resist = input.Resist;
                result.Vulnerable = input.Vulnerable;
                result.Immune = input.Immune;
                result.HP = input.HP;
                result.Initiative = input.Initiative;
                result.Strength = input.Strength;
                result.Constitution = input.Constitution;
                result.Dexterity = input.Dexterity;
                result.Intelligence = input.Intelligence;
                result.Wisdom = input.Wisdom;
                result.Charisma = input.Charisma;
                result.Tactics = input.Tactics;
                if (input.Regeneration != null)
                {
                    result.Regeneration = input.Regeneration.ToString();
                }
                
                if (input.Role is ComplexRole role)
                {
                    switch (role.Flag)
                    {
                        case RoleFlag.Elite: result.SavingThrowMod = 2;
                            break;
                        case RoleFlag.Solo: result.SavingThrowMod = 5; 
                            break;
                    }
                }
                
                // Skills
                result.Skills = ProcessSkills(input, errors);;
                
                //Movement Dist
                var movetest = input.Movement.Split(',').First();
                if (Int32.TryParse(movetest.Trim(), out var moveDist))
                {
                    result.MovementDist = moveDist;
                }
                else
                {
                    var movetest2 = input.Movement.Split(' ').First();
                    if (Int32.TryParse(movetest2.Trim(), out moveDist))
                    {
                        result.MovementDist = moveDist;
                    }
                    else
                    {
                        errors.Add("Unable to parse base movement distance from: " + input.Movement);
                    }
                }

                result.Auras = processAuras(input, errors);

                ProcessDamageModifiers(input, result);
                
                List<Power> powers = new List<Power>();
                List<Power> traits = new List<Power>();
                result.CreaturePowers = powers;
                result.CreatureTraits = traits;
                foreach (var power in input.CreaturePowers)
                {
                    var resultPower = new Power {PowerCard = power.Copy()};
                    if (power.Category == CreaturePowerCategory.Trait)
                    {
                        traits.Add(resultPower);
                        if (resultPower.PowerCard.Details == "")
                        {
                            resultPower.PowerCard.Details = resultPower.PowerCard.Range;
                        }
                    }
                    else
                    {
                        powers.Add(resultPower);
                    }

                    if (power.Attack != null)
                    {
                        ProcessAttack(resultPower, power, errors);
                    }
                }
             
                powers.Sort();
                
                var output = new CreatureAndErrors();
                output.Creature = result;
                output.Errors = errors;
                
                return output;
            }
            catch (Exception e)
            {
                throw new Exception("Error mapping creature " + input.Name, e);
            }
        }

        private static void ProcessAttack(Power resultPower, CreaturePower power, List<string> errors)
        {
            resultPower.Damage = CommonHelpers.parseDamageString(power.Damage, errors, power.Name);
            var entireMatch = entireDamageStrRx.Match(power.Details);
            if (entireMatch.Success)
            {
                resultPower.Damage.Raw = power.Damage;
                var match = entireMatch.Groups[0].Value; // get the entire match group
                var newDamageString =
                    entireDamageStrRx.Replace(power.Details, "[[" + match + "]]");
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
        
        private static string removeComma(string input)
        {
            if (input.StartsWith(", "))
            {
                return input.Substring(2);
            }

            return input;
        }

        private static List<NameValue> ProcessSkills(ICreature input, List<string> errors)
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

            return skills;
        }

        private static List<NameDescValue> processAuras(ICreature input, List<String> errors)
        {
            if (input.Auras != null)
            {
                var auras = new List<NameDescValue>();
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
                }

                return auras;
            }

            return null;
        }
    }
    
    public class CreatureAndErrors
    {
        public OutputCreature Creature { get; set; }
        public List<String> Errors { get; set; }
        public bool HasError => Errors != null && Errors.Count > 0;
        public string Name => Creature.Name;
    }

    public class OutputCreature
    {
        public string Name { get; set; } = "";
        public string Details { get; set; } = "";
        public CreatureSize Size { get; set; } = CreatureSize.Medium;
        public CreatureOrigin Origin { get; set; } = CreatureOrigin.Natural;
        public CreatureType Type { get; set; } = CreatureType.MagicalBeast;
        public int SavingThrowMod { get; set; } = 0;
        public string FullTypeDesc { get; set; }
        public string Keywords { get; set; } = "";
        public int Level { get; set; } = 1;
        public String Role { get; set; }
        public string Senses { get; set; } = "";
        public string Movement { get; set; } = "6";
        public int MovementDist { get; set; }
        public string Alignment { get; set; } = "";
        public string Languages { get; set; } = "";
        public string Equipment { get; set; } = "";
        public string Category { get; set; } = "";
        public Ability Strength { get; set; }
        public Ability Constitution { get; set; }
        public Ability Dexterity { get; set; }
        public Ability Intelligence { get; set; }
        public Ability Wisdom { get; set; }
        public Ability Charisma { get; set; }
        public List<NameValue> Skills { get; set; }
        public int AC { get; set; } = 10;
        public int Fortitude { get; set; } = 10;
        public int Reflex { get; set; } = 10;
        public int Will { get; set; } = 10;
        public List<NameDescValue> Auras { get; set; }
        public List<Power> CreaturePowers { get; set; } = new List<Power>();
        public List<Power> CreatureTraits { get; set; } = new List<Power>();
        public string Resist { get; set; } = "";
        public string Vulnerable { get; set; } = "";
        public string Immune { get; set; } = "";
        public string Tactics { get; set; } = "";
        public int HP { get; set; }
        public int Initiative { get; set; }
        public String Regeneration { get; set; }
    }
    
    public class Power : IComparable<Power>
    {
        public CreaturePower PowerCard { get; set; }
        public ParsedDamage Damage { get; set; }
        
        public int CompareTo(Power otherp)
        {
            var power = PowerCard;
            var other = otherp.PowerCard;
            if (power.Action == null || other.Action == null)
            {
                return 0;
            }

            var useValue = powerUseScore(power) - powerUseScore(other);
            if (useValue != 0)
            {
                return useValue;
            }

            // they must both be basic to compare like this.
            if (power.Action.Use == PowerUseType.Basic)
            {
                var thisRange = power.Range.ToLower();
                var otherRange = other.Range.ToLower();
                if (thisRange.StartsWith("melee") && otherRange.StartsWith("melee"))
                {
                    return 0;
                }
                if (thisRange.StartsWith("melee") && !otherRange.StartsWith("melee"))
                {
                    return -1;
                }
                if (!thisRange.StartsWith("melee") && otherRange.StartsWith("melee"))
                {
                    return 1;
                }
            }

            return 0;
        }

        private int powerUseScore(CreaturePower put)
        {
            switch (put.Action.Use)
            {
                case PowerUseType.Basic : return 0;
                case PowerUseType.AtWill: return 1;
                case PowerUseType.Encounter: return 2;
                case PowerUseType.Daily: return 3;
                default: return 999;
            }
        }
    }
}