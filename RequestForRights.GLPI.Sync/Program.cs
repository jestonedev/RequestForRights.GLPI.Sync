using System;
using System.Configuration;

namespace RequestForRights.GLPI.Sync
{
    class Program
    {
        static void Main(string[] args)
        {
            var rqrightsDb = new RequestForRightsDb(ConfigurationManager.AppSettings["rqrightsConnectionString"]);
            var rqrightsRequest = rqrightsDb.GetRequestsOnExecution();
        }
    }
}
