using System;
using System.Collections.Generic;
using Masterplan.Data;
using Masterplan.Extensibility;

namespace EncounterExport
{
    public class ExportEncounterAndSubsCommand : ICommand,IDisposable
    {
        private readonly IApplication _MPApp;

        public ExportEncounterAndSubsCommand(IApplication mpApp)
        {
            _MPApp = mpApp;
        }

        public bool Active => false;

        public bool Available => _MPApp.Project != null && (_MPApp.SelectedPoint?.Element is Encounter || _MPApp.SelectedPoint?.Element is TrapElement);

        public string Description => "Export the creatures and traps of the currently selected encounter and all sub-encounters as json";

        public string Name => "Export Encounter and All Sub Plots to Json" + CommandState.GetSystemString;

        /// <summary>
        /// Executes this instance.
        /// </summary>
        public void Execute()
        {
            var list = new List<PlotPoint>();
            list.AddRange(_MPApp.SelectedPoint.Subtree); // includes current point
            new JSONBuilder(_MPApp).CreateEncounterJson(list);
        }
        
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}

