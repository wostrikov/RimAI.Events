using System.Collections.Generic;
using Verse;

namespace Ustas.RimAI.Events
{
    // Simple Def so we can maintain the blacklist via XML and patch it.
    public class QuestBlacklistDef : Def
    {
        // List of QuestScriptDef defNames to ignore in Event+.
        public List<string> blacklistedQuestRoots;
    }
}
