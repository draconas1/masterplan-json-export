using System;
using Masterplan.Extensibility;

namespace EncounterExport
{
    public class ExportProjectCommand : ICommand,IDisposable
    {
        private readonly IApplication _MPApp;

        public ExportProjectCommand(IApplication mpApp)
        {
            _MPApp = mpApp;
        }

        public bool Active => false;

        public bool Available => _MPApp.Project != null;

        public string Description => "Export the creatures and traps of the current project as json";

        public string Name => "Export Project to Json" + CommandState.GetSystemString;

        /// <summary>
        /// Executes this instance.
        /// </summary>
        public void Execute()
        {
            new JSONBuilder(_MPApp).CreateEncounterJson(_MPApp.Project.AllPlotPoints);
        }
        
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}

