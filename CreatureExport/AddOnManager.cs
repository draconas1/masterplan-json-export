using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Masterplan.Extensibility;

namespace EncounterExport
{
    public class AddOnManager : IAddIn
    {
        private List<ICommand> _commands;

        private IApplication _MPApp = null;

        /// <summary>
        /// Gets the list of combat commands supplied by the add-in.
        /// </summary>
        public List<ICommand> CombatCommands => new List<ICommand>();

        /// <summary>
        /// Gets the list of commands supplied by the add-in.
        /// </summary>
        public List<ICommand> Commands => _commands;

        public string Description => "Add-in used to export data to json for VTT use";

        public bool Initialise(IApplication app)
        {
            //Set bool to return whether this Add-In has initialized correctly
            bool initializeSuccessful = true;

            try
            {
                this._MPApp = app;
                _commands = new List<ICommand>();
                _commands.Add(new ExportProjectCommand(app));
                _commands.Add(new ExportEncounterCommand(app));
                _commands.Add(new ExportEncounterAndSubsCommand(app));
                _commands.Add(new SwitchSystemCommand(app));
                AppDomain currentDomain = AppDomain.CurrentDomain;
                currentDomain.AssemblyResolve += new ResolveEventHandler(LoadFromSameFolder);
            }
            catch (Exception ex)
            {
                initializeSuccessful = false;
                Utils.LogSystem.Trace(ex);
            }

            return initializeSuccessful;
        }
        
        //http://stackoverflow.com/questions/1373100/how-to-add-folder-to-assembly-search-path-at-runtime-in-net
        internal static Assembly LoadFromSameFolder(object sender, ResolveEventArgs args)
        {
            string folderPath = CurrentDLLPath;
            string assemblyPath = Path.Combine(folderPath, new AssemblyName(args.Name).Name + ".dll");
            if (File.Exists(assemblyPath) == false) return null;
            Assembly assembly = Assembly.LoadFrom(assemblyPath);
            return assembly;
        }

        internal static string CurrentDLLPath
        {
            get
            {
                return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            }
        }


        public string Name => "Creature Exporter";

        /// <summary>
        /// Gets the list of tab pages supplied by the add-in.
        /// </summary>
        public List<IPage> Pages
        {
            get { return new List<IPage>(); }
        }

        /// <summary>
        /// Gets the list of quick reference tab pages supplied by the add-in.
        /// </summary>
        public List<IPage> QuickReferencePages
        {
            get { return new List<IPage>(); }
        }

        public Version Version
        {
            get { return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version; }
        }
    }
}
