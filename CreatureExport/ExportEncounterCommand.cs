using System;
using System.Collections.Generic;
using Masterplan.Data;
using Masterplan.Extensibility;

namespace EncounterExport
{
    public class ExportEncounterCommand : ICommand,IDisposable
    {
        private readonly IApplication _MPApp;

        public ExportEncounterCommand(IApplication mpApp)
        {
            _MPApp = mpApp;
        }

        public bool Active => false;

        public bool Available => _MPApp.Project != null && _MPApp.SelectedPoint?.Element is Encounter;

        public string Description => "Export the creatures of the currently selected encounter project as json";

        public string Name => "Export Encounter to Json";

        /// <summary>
        /// Executes this instance.
        /// </summary>
        public void Execute()
        {
            var list = new List<PlotPoint>();
            list.Add(_MPApp.SelectedPoint);
            new JSONBuilder(_MPApp).CreateEncounterJson(list);
        }
        
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

       
    }
}

