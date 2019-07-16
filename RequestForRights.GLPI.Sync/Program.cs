using System;
using System.Configuration;

namespace RequestForRights.GLPI.Sync
{
    class Program
    {
        static void Main(string[] args)
        {
            var rqrightsDb = new RequestForRightsDb(ConfigurationManager.AppSettings["rqrightsConnectionString"]);
            var rqrightsRequests = rqrightsDb.GetRequestsOnExecution();
            var glpiDb = new GlpiDb(ConfigurationManager.AppSettings["glpiConnectionString"]);
            var glpiInsertedRequestsIds = glpiDb.InsertRequests(glpiDb.FilterExistsRequests(rqrightsRequests));
            var glpiInsertedRequests = glpiDb.GetRequests(glpiInsertedRequestsIds);
        }
    }
}
