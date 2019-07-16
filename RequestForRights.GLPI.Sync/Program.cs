using System;
using System.Configuration;
using System.Linq;

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
            var smtpReporter = new SmtpReporter(ConfigurationManager.AppSettings["smtpHost"],
                int.Parse(ConfigurationManager.AppSettings["smtpPort"]),
                ConfigurationManager.AppSettings["smtpFrom"]);
            foreach(var request in glpiInsertedRequests)
            {
                var mailBody = smtpReporter.BuildMailBodyForNewGlpiRequest(request);
                var mailTitle = smtpReporter.BuildMailTitleForNewGlpiRequest(request);
                smtpReporter.SendMail(mailTitle, mailBody, request.Managers.Select(r => r.Email).ToList());
            }
        }
    }
}
