using System;
using System.Collections.Generic;
using System.Linq;
using EncounterExport.FoundryHelpers;
using Masterplan.Data;

namespace EncounterExport
{
    public static class FoundryTrapHelper
    {
        public static FoundryTrapAndErrors CreateTrap(Trap input)
        {
            try
            {
                var errors = new List<string>();
                var output = new FoundryCreature();
                var result = output.Data;
                var details = result.details;
                output.Token.hidden = true;
                
                result.name = input.Name;
                result.biography = input.Description;
                
                details.level = input.Level;
                int halfLevel = input.Level / 2;
                var initBonus = new Bonus
                {
                    active = true,
                    name = $"Trap",
                    value = 0
                };
              

                if (input.Initiative > 0)
                {
                    result.attributes.init.bonus.Add(initBonus);
                    initBonus.value = input.Initiative - halfLevel;
                }
                if (input.Initiative < 0 && input.Attack != null && input.Attack.HasInitiative)
                {
                    result.attributes.init.bonus.Add(initBonus);
                    initBonus.value = input.Attack.Initiative - halfLevel;
                }

                result.attributes.init.ability = "";
                result.details.exp = input.XP;
                
                switch (input.Role)
                {
                    case ComplexRole role:
                        details.role.primary = role.Type.ToString().ToLowerInvariant();
                        details.other = role.Type.ToString().ToLowerInvariant();
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
               

                List<FoundryPower> powers = output.Powers;
                List<FoundryTrait> traits = output.Traits;

                if (!String.IsNullOrEmpty(input.ReadAloud))
                {
                    var trait = new FoundryTrait
                    {
                        name = "Description"
                    };
                    trait.system.description.value = input.ReadAloud;
                    traits.Add(trait);
                }
                if (!String.IsNullOrEmpty(input.Trigger))
                {
                    var trigger = new FoundryTrait
                    {
                        name = "Trigger"
                    };
                    trigger.system.description.value = input.Trigger;
                    traits.Add(trigger);
                }
                var inputCountermeasures = input.Countermeasures.Select(x => x.Split( new[] { "\r\n", "\r", "\n" },
                    StringSplitOptions.None)).SelectMany(x => x).ToList();
                if (inputCountermeasures.Any())
                {
                    var counters = new FoundryTrait
                    {
                        name = "Countermeasures"
                    };
                    counters.system.description.value = String.Join("\n", inputCountermeasures.Select(x => "<p>" + x + "</p>").ToArray());
                    traits.Add(counters);
                }

                if (input.Skills.Any())
                {
                    var skills = new FoundryTrait
                    {
                        name = "Skills"
                    };
                    
                    skills.system.description.value = String.Join("\n", input.Skills.Select(x => $"<p><b>{x.SkillName} DC {x.DC}: </b> {x.Details}</p>").ToArray());
                    traits.Add(skills);
                }
                
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
                        var power = new FoundryPower
                        {
                            name = attackName,
                            _id = FoundryPowerHelper.newId(),
                            img = "icons/svg/trap.svg"
                        };
                        powers.Add(power);
                        var powerData = power.system;
                        powerData.keywords = inputAttack.Keywords.Split(CommonHelpers.separator, StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => x.Trim()).ToList();
                        FoundryPowerHelper.ProcessAttackDamageAndRange(power, errors,
                            inputAttack.OnHit,
                            inputAttack.OnHit,
                            inputAttack.Attack.Defence,
                            inputAttack.Attack.Bonus,
                            inputAttack.Range);

                        powerData.miss.detail = inputAttack.OnMiss;
                        powerData.target = inputAttack.Target;
                        powerData.effect.detail = inputAttack.Effect;
                    }
                }
                
                if (input.Description.StartsWith("HP="))
                {
                    var lines = input.Description.Split(new[] {"\r\n", "\r", "\n"}, StringSplitOptions.None);
                    bool acSet = false;
                    bool fortSet = false;
                    bool refSet = false;
                    bool willSet = false;
                    foreach (var line in lines)
                    {
                        var parts = line.Split('=');
                        switch (parts[0].ToLower())
                        {
                            case "hp" :
                                if (Int32.TryParse(parts[1], out int hp))
                                {
                                    result.attributes.hp.value = hp;
                                }
                                else
                                {
                                    errors.Add(input.Name + " failed to parse HP number from " + line + " from description");
                                }

                                break;
                            case "other":
                                if (Int32.TryParse(parts[1], out int other))
                                {
                                    if (!acSet) result.defences.ac.value = other;
                                    if (!fortSet) result.defences.fort.value = other;
                                    if (!refSet) result.defences.refValue.value = other;
                                    if (!willSet) result.defences.wil.value = other;
                                }
                                else
                                {
                                    errors.Add(input.Name + " failed to parse defence number from " + line + " from description");
                                }
                                break;
                            case "ac":
                                if (Int32.TryParse(parts[1], out int ac))
                                {
                                    result.defences.ac.value = ac;
                                    acSet = true;
                                }
                                else
                                {
                                    errors.Add(input.Name + " failed to parse ac number from " + line + " from description");
                                }
                                break;
                            case "fort":
                                if (Int32.TryParse(parts[1], out int fort))
                                {
                                    result.defences.fort.value = fort;
                                    fortSet = true;
                                }
                                else
                                {
                                    errors.Add(input.Name + " failed to parse fort number from " + line + " from description");
                                }
                                break;
                            case "ref":
                                if (Int32.TryParse(parts[1], out int reflex))
                                {
                                    result.defences.refValue.value = reflex;
                                    refSet = true;
                                }
                                else
                                {
                                    errors.Add(input.Name + " failed to parse reflex number from " + line + " from description");
                                }
                                break;
                            case "will":
                                if (Int32.TryParse(parts[1], out int will))
                                {
                                    result.defences.wil.value = will;
                                    willSet = true;
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
                
                return new FoundryTrapAndErrors { Errors = errors, Trap = output };
            }
            catch (Exception e)
            {
                throw new Exception("Error mapping trap " + input.Name, e);
            }
        }
    }
}