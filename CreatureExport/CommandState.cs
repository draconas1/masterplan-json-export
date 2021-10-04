namespace EncounterExport
{
    public static class CommandState
    {
        public static OutputSystem SelectedSystem { get; set; } = OutputSystem.Roll20;

        public static string GetSystemString
        {
            get
            {
                switch (SelectedSystem)
                {
                    case OutputSystem.Foundry : return " for Foundry";
                    case OutputSystem.Roll20 : return " for Roll20";
                    default: return "";
                }
            }
        }
    }

    public enum OutputSystem
    {
        Roll20,
        Foundry
    }
}