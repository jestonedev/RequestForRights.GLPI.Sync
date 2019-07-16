using System.Collections.Generic;
using System.Linq;

namespace RequestForRights.GLPI.Sync
{
    public class RequestForRightsUser
    {
        public RequestForRightsUser()
        {
            RequestForRightsRights = new List<RequestForRightsRight>();
        }

        public int IdRequestUser { get; set; }
        public string Snp { get; set; }
        public string Post { get; set; }
        public string Phone { get; set; }
        public string Department { get; set; }
        public string Description { get; set; }
        public List<RequestForRightsRight> RequestForRightsRights { get; set; }

        public bool HasRight(RequestForRightsRight right)
        {
            return RequestForRightsRights.Any(r =>
                                r.ResourceName == right.ResourceName &&
                                r.ResourceRightName == right.ResourceRightName &&
                                r.ResourceRightDescription == right.ResourceRightDescription);
        }
    }
}