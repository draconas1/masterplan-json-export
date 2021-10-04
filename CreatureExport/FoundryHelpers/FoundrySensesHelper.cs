using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace EncounterExport.FoundryHelpers
{
    public static class FoundrySensesHelper
    {
        
        private static readonly Regex BlindsightRgx =
            new Regex(@"blindsight ([1-9][0-9]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex TremorsenseRgx =
            new Regex(@"tremorsense ([1-9][0-9]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static Senses ProcessSenses(string inputSenses)
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
    }
}