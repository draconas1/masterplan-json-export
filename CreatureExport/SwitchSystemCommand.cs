using System;
using Masterplan.Extensibility;

namespace EncounterExport
{
    public class SwitchSystemCommand : ICommand, IDisposable
    {
        public SwitchSystemCommand(IApplication mpApp)
        {
        }

        public bool Active => false;

        public bool Available => true;

        public string Description => "Export the creatures and traps of the current project as json";

        public string Name => getName();

        /// <summary>
        /// Executes this instance.
        /// </summary>
        public void Execute()
        {
            switch (CommandState.SelectedSystem)
            {
                case OutputSystem.Foundry:
                    CommandState.SelectedSystem = OutputSystem.Roll20;
                    break;

                case OutputSystem.Roll20:
                    CommandState.SelectedSystem = OutputSystem.Foundry;
                    break;
            }
        }

        private string getName()
        {
            switch (CommandState.SelectedSystem)
            {
                case OutputSystem.Foundry:
                    return "Switch Output to Roll20";
                case OutputSystem.Roll20:
                    return "Switch Output to Foundry";
                default: return "ERROR";
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}