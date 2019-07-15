using System.Collections.Generic;

namespace RequestForRights.GLPI.Sync
{
    internal class GlpiRequest
    {
        public int IdGlpiRequest { get; set; }
        public int IdRequestForRightsRequest { get; set; }
        public List<GlpiRequestExecutor> Executors { get; set; }
        public List<GlpiRequestManager> Managers { get; set; }
    }
}