using System;
using System.Collections.Generic;
using System.Linq;
using Masterplan.Data;

namespace EncounterExport
{
    public static class TrapHelper
    {
        public static TrapAndErrors CreateTrap(Trap input)
        {
            try
            {
                var errors = new List<string>();
                var result = new OutputTrap();
                result.Name = input.Name;
                result.Details = input.Details;
                result.Description = input.Description;
                result.Level = input.Level;
                result.Initiative = input.Initiative > 0 ? input.Initiative : 0;
                result.Role = input.Info;
                result.Countermeasures = input.Countermeasures.Select(x => x.Split( new[] { "\r\n", "\r", "\n" },
                    StringSplitOptions.None)).SelectMany(x => x).ToList();
                result.Trigger = input.Trigger;
                
                foreach (var inputAttack in input.Attacks)
                {
                    // tendency for empty attacks to happen
                    if ((!string.IsNullOrEmpty(inputAttack.Name) && inputAttack.Name != "Attack")
                        || inputAttack.Action != ActionType.Standard
                        || inputAttack.Range != ""
                        || inputAttack.Target != ""
                        || inputAttack.Attack.Bonus != 0
                        || inputAttack.OnHit != ""
                        || inputAttack.OnMiss != ""
                        || inputAttack.Effect != "")
                    {
                        var attackName = string.IsNullOrEmpty(inputAttack.Name) || inputAttack.Name == "Attack" ? "Trap Attack" : inputAttack.Name;
                        var attack = new OutputTrapAttack()
                        {
                            Attack = inputAttack.Copy(),
                            Damage = CommonHelpers.parseDamageString(inputAttack.OnHit, errors, input.Name)
                        };
                        attack.Attack.Name = attackName;
                        result.Attacks.Add(attack);
                        
                        var entireMatch = CommonHelpers.entireDamageStrRx.Match(inputAttack.OnHit);
                        if (entireMatch.Success)
                        {
                            attack.Damage.Raw = inputAttack.OnHit;
                            var match = entireMatch.Groups[0].Value; // get the entire match group
                            var newDamageString =
                                CommonHelpers.entireDamageStrRx.Replace(inputAttack.OnHit, "[[" + match + "]]");
                            attack.Attack.OnHit = newDamageString;
                        }
                        
                    }
                }

                result.Skills = input.Skills.Select(x => new NameDescValue(x.SkillName, x.Details, x.DC)).ToList();

                if (input.Description.StartsWith("HP="))
                {
                    var lines = input.Description.Split(new[] {"\r\n", "\r", "\n"}, StringSplitOptions.None);
                    foreach (var line in lines)
                    {
                        var parts = line.Split('=');
                        switch (parts[0].ToLower())
                        {
                            case "hp" :
                                if (Int32.TryParse(parts[1], out int hp))
                                {
                                    result.HP = hp;
                                }
                                else
                                {
                                    errors.Add(input.Name + " failed to parse HP number from " + line + " from description");
                                }

                                break;
                            case "other":
                                if (Int32.TryParse(parts[1], out int other))
                                {
                                    result.Fortitude = result.Fortitude ?? other;
                                    result.Reflex = result.Reflex ?? other;
                                    result.Will = result.Will ?? other;
                                }
                                else
                                {
                                    errors.Add(input.Name + " failed to parse defence number from " + line + " from description");
                                }
                                break;
                            case "ac":
                                if (Int32.TryParse(parts[1], out int ac))
                                {
                                    result.AC = ac;
                                }
                                else
                                {
                                    errors.Add(input.Name + " failed to parse ac number from " + line + " from description");
                                }
                                break;
                            case "fort":
                                if (Int32.TryParse(parts[1], out int fort))
                                {
                                    result.AC = fort;
                                }
                                else
                                {
                                    errors.Add(input.Name + " failed to parse fort number from " + line + " from description");
                                }
                                break;
                            case "ref":
                                if (Int32.TryParse(parts[1], out int reflex))
                                {
                                    result.AC = reflex;
                                }
                                else
                                {
                                    errors.Add(input.Name + " failed to parse reflex number from " + line + " from description");
                                }
                                break;
                            case "will":
                                if (Int32.TryParse(parts[1], out int will))
                                {
                                    result.AC = will;
                                }
                                else
                                {
                                    errors.Add(input.Name + " failed to parse will number from " + line + " from description");
                                }
                                break;
                            default: break;
                        }
                    }
                }
                
                return new TrapAndErrors { Errors = errors, Trap = result};
            }
            catch (Exception e)
            {
                throw new Exception("Error mapping trap " + input.Name, e);
            }
        }
    }
   
    public class TrapAndErrors
    {
        public OutputTrap Trap { get; set; }
        public List<String> Errors { get; set; }
        public bool HasError => Errors != null && Errors.Count > 0;
        public string Name => Trap.Name;
    }

    public class OutputTrap
    {
        public string Name { get; set; }
        public string Description { get; set; } = "";
        public string Details { get; set; } = "";

        public string Trigger { get; set; } = "";
        public List<NameDescValue> Skills { get; set; } = new List<NameDescValue>();
        public int Level { get; set; } = 1;
        public int Initiative { get; set; } 
        public string Role { get; set; } = "";
        public List<String> Countermeasures { get; set; } = new List<string>();
        public List<OutputTrapAttack> Attacks { get; set; } = new List<OutputTrapAttack>();
        
        public int? AC { get; set; }
        public int? Fortitude { get; set; }
        public int? Reflex { get; set; }
        public int? Will { get; set; }
        public int? HP { get; set; }
    }

    public class OutputTrapAttack
    {
        public ParsedDamage Damage { get; set; }
        public TrapAttack Attack { get; set; }
    }
}