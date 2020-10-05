using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CompendiumImport.UI;
using Masterplan.Data;
using Masterplan.Extensibility;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace EncounterExport
{
    public class JSONBuilder
    { 
        private readonly IApplication _MPApp;
        public JSONBuilder(IApplication mpApp)
        {
            _MPApp = mpApp;
        }
        public void CreateEncounterJson(List<PlotPoint> plotPoints)
        {
            var encounterList = new List<EncounterOutput>();
            
            foreach (var plotPoint in plotPoints)
            {
                var encounter = plotPoint.Element as Encounter;
                if (encounter == null)
                {
                    continue;
                }
                
                var creatures = encounter.Slots.Select(y => y.Card)
                    .Select(z => z.CreatureID)
                    .Select(FindCreature)
                    .Select(createCreate)
                    .ToList();

                var output = new EncounterOutput();
                output.Name = plotPoint.Name;
                output.Creatures = creatures.Select(x => x.Creature).ToList();
                output.Errors = creatures.Select(x => x.Errors.Select(y => x.Name + ": " + y)).SelectMany(x => x).ToList();
                encounterList.Add(output);
            }

            var list = JsonConvert.SerializeObject(encounterList, Formatting.Indented, new StringEnumConverter());

            var errors = encounterList.Select(x => x.Errors).SelectMany(x => x).ToList();
            var form = new ResultForm();
            form.Open(list, errors);
        }
        
        private Output createCreate(ICreature input)
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
                var skillArray = input.Skills.Split(',')
                    .Select(x => x.Trim())
                    .Where(x => x.Length > 0)
                    .ToList();
                var skillAndBonus = skillArray.Select(x => x.Split('+')).ToList();
                List<NameValue> skills = new List<NameValue>();
                foreach (var array in skillAndBonus)
                {
                    if (array.Length < 2)
                    {
                        errors.Add("Error converting skill " + string.Join(",", array) + " from " + input.Skills);
                    }

                    skills.Add(new NameValue(array[0].Trim(), Int32.Parse(array[1].Trim())));
                }

                result.Skills = skills;
                
                //Movement Dist
                var movetest = input.Movement.Split(',').First();
                int moveDist = 0;
                if (Int32.TryParse(movetest.Trim(), out moveDist))
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

                if (input.Auras != null)
                {
                    var auras = new List<NameDescValue>();
                    foreach (var aura in input.Auras)
                    {
                        var firstPart = aura.Details.Split(' ')[0];
                        int dist = 0;
                        if (Int32.TryParse(firstPart, out dist))
                        {
                            auras.Add(new NameDescValue(aura.Name, aura.Details, dist));
                        }
                        else
                        {
                            errors.Add("Unable to parse distance for aura: " + aura.Name + ": " + aura.Details);
                        }
                    }

                    result.Auras = auras;
                }

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
                            vunerables = vunerables + str;;
                        }
                    }

                    result.Resist += resistances;
                    result.Vulnerable += vunerables;
                    result.Resist = removeComma(result.Resist);
                    result.Vulnerable = removeComma(result.Vulnerable);
                }
                Regex damageDiceRx = new Regex(@"([1-9][0-9]*)d([12468][02]*)",
                    RegexOptions.Compiled | RegexOptions.IgnoreCase);
                Regex damagePlusRx = new Regex(@"\+[ ]*([1-9][0-9]*)*",
                    RegexOptions.Compiled | RegexOptions.IgnoreCase);
                Regex entireDamageStrRx = new Regex(@"([1-9][0-9]*)d([12468][02]*)([ ]*\+[ ]*([1-9][0-9]*)*)*",
                    RegexOptions.Compiled | RegexOptions.IgnoreCase);
                
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
                    }
                    else
                    {
                        powers.Add(resultPower);
                    }

                    if (power.Attack != null)
                    {
                        var dam = new ParsedDamage();
                        resultPower.Damage = dam;
                        var damageStr = power.Damage;
                        // 3d10 + 9 damage
                        // 4 damage
                        var diceMatch = damageDiceRx.Match(damageStr);
                        if (diceMatch.Success)
                        {
                            var numDice = diceMatch.Groups[1].Value;
                            var diceSize = diceMatch.Groups[2].Value;
                            if (!Int32.TryParse(numDice, out dam.NumDice))
                            {
                                errors.Add("Unable to parse number of dice for damage for power " + power.Name + ". " + damageStr + " regex found " + numDice);
                            }
                            if (!Int32.TryParse(diceSize, out dam.DiceSize))
                            {
                                errors.Add("Unable to parse dice size for damage for power " + power.Name + ". " + damageStr + " regex found " + diceSize);
                            }

                            var bonusMatch = damagePlusRx.Match(damageStr);
                            if (bonusMatch.Success)
                            {
                                var bonus = bonusMatch.Groups[1].Value;
                                if (!Int32.TryParse(bonus, out dam.Bonus))
                                {
                                    errors.Add("Unable to parse bonus damage for power " + power.Name + ". " + damageStr + " regex found " + bonus);
                                }
                            }
                        }
                        else
                        {
                            // assume its fixed damage
                            var hopefullyNumber = damageStr.Split(' ').First();
                            if (!Int32.TryParse(hopefullyNumber, out dam.Bonus))
                            {
                                errors.Add("Unable to parse bonus damage for non dice damage power " + power.Name + ". " + damageStr + " regex found " + hopefullyNumber);
                            }
                        }

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
                }
             
                powers.Sort();
                
                var output = new Output();
                output.Creature = result;
                output.Errors = errors;
                
                return output;
            }
            catch (Exception e)
            {
                throw new Exception("Error mapping creature " + input.Name, e);
            }
        }

        private string removeComma(string input)
        {
            if (input.StartsWith(", "))
            {
                return input.Substring(2);
            }

            return input;
        }
        
        private ICreature FindCreature(Guid creature_id)
        {
            foreach (Library library in  _MPApp.Libraries)
            {
                Creature creature = library.FindCreature(creature_id);
                if (creature != null)
                    return (ICreature) creature;
            }
            
            Creature creature2 = _MPApp.Project.Library.FindCreature(creature_id);
            if (creature2 != null)
                return (ICreature) creature2;
            CustomCreature customCreature = _MPApp.Project.FindCustomCreature(creature_id);
            if (customCreature != null)
                return (ICreature) customCreature;
            NPC npc = _MPApp.Project.FindNPC(creature_id);
            if (npc != null)
                return (ICreature) npc;
            return (ICreature) null;
        }

        public class EncounterOutput
        {
            public String Name { get; set; }
            public List<OutputCreature> Creatures { get; set; }
            
            public List<string> Errors { get; set; }
        }


        public class Output
        {
            public OutputCreature Creature { get; set; }
            public List<String> Errors { get; set; }
            public bool HasError => Errors != null && Errors.Count > 0;
            public string Name => Creature.Name;
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

        public class ParsedDamage
        {
            public int NumDice;
            public int DiceSize;
            public int Bonus;
            public String Raw;
        }

        public class NameValue
        {
            public NameValue(string name, int bonus)
            {
                Name = name;
                Bonus = bonus;

            }
            public string Name { get; }
            public int Bonus { get; }
        }
        
        public class NameDescValue : NameValue
        {
            public NameDescValue(string name, string desc, int bonus) : base(name, bonus)
            {
                Desc = desc;
            }
            
            public string Desc { get; }
        }

        public class OutputCreature
        {
            public string Name = "";
            public string Details = "";
            public CreatureSize Size = CreatureSize.Medium;
            public CreatureOrigin Origin = CreatureOrigin.Natural;
            public CreatureType Type = CreatureType.MagicalBeast;
            public int SavingThrowMod = 0; 
            public string FullTypeDesc;
            public string Keywords = "";
            public int Level = 1;
            public String Role;
            public string Senses = "";
            public string Movement = "6";
            public int MovementDist;
            public string Alignment = "";
            public string Languages = "";
            public string Equipment = "";
            public string Category = "";
            public Ability Strength;
            public Ability Constitution;
            public Ability Dexterity;
            public Ability Intelligence;
            public Ability Wisdom;
            public Ability Charisma;
            public List<NameValue> Skills;
            public int AC = 10;
            public int Fortitude = 10;
            public int Reflex = 10;
            public int Will = 10;
            public List<NameDescValue> Auras;
            public List<Power> CreaturePowers = new List<Power>();
            public List<Power> CreatureTraits = new List<Power>();
            public string Resist = "";
            public string Vulnerable = "";
            public string Immune = "";
            public string Tactics = "";
            public int HP;
            public int Initiative;
            public String Regeneration;
        }
    }
}