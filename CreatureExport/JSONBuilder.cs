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

    public class EncounterCreature
    {
        public EncounterCard Card { get; set; }
        public ICreature Creature { get; set; }
    }
    public class JSONBuilder
    { 
        private readonly IApplication _mpApp;
        public JSONBuilder(IApplication mpApp)
        {
            _mpApp = mpApp;
        }
        public void CreateEncounterJson(List<PlotPoint> plotPoints)
        {
            try
            {
                object listToBeSerialised = null;
                List<string> errors = null;
                if (CommandState.SelectedSystem == OutputSystem.Roll20)
                {
                    var encounterList = new List<Roll20EncounterOutput>();

                    foreach (var plotPoint in plotPoints)
                    {
                        if (plotPoint.Element is Encounter encounter)
                        {
                            var creatures = encounter.Slots.Select(y => y.Card)
                                .Select(z => FindCreature(z.CreatureID))
                                .Select(Roll20CreatureHelper.createCreature)
                                .ToList();
                            var traps = encounter.Traps.Select(Roll20TrapHelper.CreateTrap).ToList();

                            var output = new Roll20EncounterOutput
                            {
                                Name = plotPoint.Name,
                                Creatures = creatures.Select(x => x.Creature).ToList(),
                                Errors = creatures.Select(x => x.Errors.Select(y => x.Name + ": " + y)).SelectMany(x => x)
                                    .ToList().Concat(traps.Select(x => x.Errors.Select(y => x.Name + ": " + y))
                                        .SelectMany(x => x)).ToList(),
                                Traps = traps.Select(x => x.Trap).ToList()
                            };

                            encounterList.Add(output);
                        }

                        if (plotPoint.Element is TrapElement trap)
                        {
                            var trapsOutput = Roll20TrapHelper.CreateTrap(trap.Trap);
                            var output = new Roll20EncounterOutput
                            {
                                Name = plotPoint.Name,
                                Errors = trapsOutput.Errors
                            };
                            output.Traps.Add(trapsOutput.Trap);
                            encounterList.Add(output);
                        }
                    }

                    listToBeSerialised = encounterList;
                    errors = encounterList.Select(x => x.Errors).SelectMany(x => x).ToList();
                }
                
                if (CommandState.SelectedSystem == OutputSystem.Foundry)
                {
                    var encounterList = new List<FoundryEncounterOutput>();
                    foreach (var plotPoint in plotPoints)
                    {
                        if (plotPoint.Element is Encounter encounter)
                        {
                            var creatures = encounter.Slots.Select(y => y.Card)
                                .Select(z => new EncounterCreature
                                {
                                    Card = z,
                                    Creature = FindCreature(z.CreatureID)
                                })
                                .Select(FoundryCreatureHelper.CreateCreature)
                                .ToList();
                            var traps = encounter.Traps.Select(FoundryTrapHelper.CreateTrap).ToList();

                            var output = new FoundryEncounterOutput
                            {
                                Name = plotPoint.Name,
                                Creatures = creatures.Select(x => x.Creature).ToList(),
                                Errors = creatures.Select(x => x.Errors.Select(y => x.Name + ": " + y)).SelectMany(x => x)
                                    .ToList().Concat(traps.Select(x => x.Errors.Select(y => x.Name + ": " + y))
                                        .SelectMany(x => x)).ToList(),
                                Traps = traps.Select(x => x.Trap).ToList()
                            };

                            encounterList.Add(output);
                        }

                        if (plotPoint.Element is TrapElement trap)
                        {
                            var trapsOutput = FoundryTrapHelper.CreateTrap(trap.Trap);
                            var output = new FoundryEncounterOutput 
                            {
                                Name = plotPoint.Name,
                                Errors = trapsOutput.Errors
                            };
                            output.Traps.Add(trapsOutput.Trap);
                            encounterList.Add(output);
                        }
                    }
                    listToBeSerialised = encounterList;
                    errors = encounterList.Select(x => x.Errors).SelectMany(x => x).ToList();
                }
               

                var list = JsonConvert.SerializeObject(listToBeSerialised, Formatting.Indented, new StringEnumConverter());
                
                var form = new ResultForm();
                form.Open(list, errors);
            }
            catch (Exception e)
            {
                var form = new ResultForm();
                form.Open("Fatal error: " + e, new List<string>());
                throw e;
            }
        }

        private ICreature FindCreature(Guid creature_id)
        {
            foreach (Library library in  _mpApp.Libraries)
            {
                Creature creature = library.FindCreature(creature_id);
                if (creature != null)
                    return (ICreature) creature;
            }
            
            Creature creature2 = _mpApp.Project.Library.FindCreature(creature_id);
            if (creature2 != null)
                return (ICreature) creature2;
            CustomCreature customCreature = _mpApp.Project.FindCustomCreature(creature_id);
            if (customCreature != null)
                return (ICreature) customCreature;
            NPC npc = _mpApp.Project.FindNPC(creature_id);
            if (npc != null)
                return (ICreature) npc;
            return (ICreature) null;
        }

        public class Roll20EncounterOutput
        {
            public String Name { get; set; }
            public List<OutputCreature> Creatures { get; set; } = new List<OutputCreature>();

            public List<OutputTrap> Traps { get; set; } = new List<OutputTrap>();

            public List<string> Errors { get; set; } = new List<string>();
        }
        
        public class FoundryEncounterOutput
        {
            public String Name { get; set; }
            public List<FoundryCreature> Creatures { get; set; } = new List<FoundryCreature>();

            public List<FoundryCreature> Traps { get; set; } = new List<FoundryCreature>();

            public List<string> Errors { get; set; } = new List<string>();
        }
    }
}