using System;
using System.Collections.Generic;

namespace RequestForRights.GLPI.Sync
{
    public class GlpiRequest
    {
        public int IdGlpiRequest { get; set; }
        public int IdRequestForRightsRequest { get; set; }
        public string Name { get; set; }
        public string Content { get; set; }
        public DateTime OpenDate { get; set; }
        public string Cateogry { get; set; }
        public string Initiator { get; set; }
        public string ExecutorsGroups { get; set; }
        public string CompleteDescription { get; set; }
        public string CompleteUserSnp { get; set; }
        public string CompleteUserLogin { get; set; }
        public List<GlpiRequestExecutor> Executors { get; set; }
        public List<GlpiRequestManager> Managers { get; set; }
    }
}