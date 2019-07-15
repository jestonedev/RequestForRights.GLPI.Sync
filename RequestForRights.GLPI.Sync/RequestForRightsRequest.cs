using System;
using System.Collections.Generic;
using System.Text;

namespace RequestForRights.GLPI.Sync
{
    class RequestForRightsRequest
    {
        public int IdRequest { get; set; }
        public int IdRequestType { get; set; }
        public RequestForRightsRequester Requester { get; set; }
        public string Description { get; set; }
        public List<RequestForRightsResourceResponsibleDepartment> ResourceResponsibleDepartments { get; set; }
        public List<RequestForRightsUser> RequestForRightsUsers { get; set; }
    }
}
