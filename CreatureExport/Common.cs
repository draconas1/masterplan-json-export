using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Masterplan.Data;

namespace EncounterExport
{

    public static class CommonHelpers
    {
        public static readonly Regex entireDamageStrRx = new Regex(@"([1-9][0-9]*)d([12468][02]*)([ ]*\+[ ]*([1-9][0-9]*)*)*",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
        private static readonly Regex damageDiceRx = new Regex(@"([1-9][0-9]*)d([12468][02]*)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex damagePlusRx = new Regex(@"\+[ ]*([1-9][0-9]*)*",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static ParsedDamage parseDamageString(string damageStr, List<string> errors, string context)
        {
            var parsedDamage = new ParsedDamage();
            // 3d10 + 9 damage
            // 4 damage
            if (damageStr.Trim().Length <= 0)
            {
                return parsedDamage;
            }
            var diceMatch = damageDiceRx.Match(damageStr);
            if (diceMatch.Success)
            {
                var numDice = diceMatch.Groups[1].Value;
                var diceSize = diceMatch.Groups[2].Value;
                if (!Int32.TryParse(numDice, out parsedDamage.NumDice))
                {
                    errors.Add("Unable to parse number of dice for damage for power " + context + ". " + damageStr +
                               " regex found " + numDice);
                }

                if (!Int32.TryParse(diceSize, out parsedDamage.DiceSize))
                {
                    errors.Add("Unable to parse dice size for damage for power " + context + ". " + damageStr +
                               " regex found " + diceSize);
                }

                var bonusMatch = damagePlusRx.Match(damageStr);
                if (bonusMatch.Success)
                {
                    var bonus = bonusMatch.Groups[1].Value;
                    if (!Int32.TryParse(bonus, out parsedDamage.Bonus))
                    {
                        errors.Add("Unable to parse bonus damage for power " + context + ". " + damageStr + " regex found " +
                                   bonus);
                    }
                }
            }
            else
            {
                // assume its fixed damage
                var hopefullyNumber = damageStr.Split(' ').First();
                if (!Int32.TryParse(hopefullyNumber, out parsedDamage.Bonus))
                {
                    errors.Add("Unable to parse bonus damage for a power with a damage entry that does not include a dice roll. " + context + ". '" + damageStr +
                               "'.  This is probably a text non damage attack power, but you may need to tweak the damage entry");
                }
            }

            return parsedDamage;
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
}