using System;
using System.Collections.Generic;
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
            var glpiNotExistsRequests = glpiDb.FilterExistsRequests(rqrightsRequests);
            var glpiInsertedRequestsAssoc = glpiDb.InsertRequests(glpiNotExistsRequests);
            rqrightsDb.InsertRequestsAssoc(glpiInsertedRequestsAssoc);
            var glpiInsertedRequests = glpiDb.GetRequests(glpiInsertedRequestsAssoc.Select(r => r.Item2).ToList());
            var smtpReporter = new SmtpReporter(ConfigurationManager.AppSettings["smtpHost"],
                int.Parse(ConfigurationManager.AppSettings["smtpPort"]),
                ConfigurationManager.AppSettings["smtpFrom"]);
            foreach(var request in glpiInsertedRequests)
            {
                var mailBody = smtpReporter.BuildMailBodyForNewGlpiRequest(request);
                var mailTitle = smtpReporter.BuildMailTitleForNewGlpiRequest(request);
                smtpReporter.SendMail(mailTitle, mailBody, request.Managers.Select(r => r.Email).ToList());
            }
            smtpReporter.SendMailToSmevExecutor(glpiNotExistsRequests, ConfigurationManager.AppSettings["smtpSmevTo"].Split(",").ToList());
            if (ConfigurationManager.AppSettings["oneWaySync"] == "1")
            {
                return;
            }
            var glpiRequestsForCompleteChecking = rqrightsRequests.Except(glpiNotExistsRequests);
            var glpiCompletedRequest = glpiDb.GetRequests(null, 
                glpiRequestsForCompleteChecking.Select(r => (long)r.IdRequest).ToList(), new List<int> { 5, 6 });
            var glpiCanceledRequests = glpiDb.GetRequests(null,
                glpiRequestsForCompleteChecking.Select(r => (long)r.IdRequest).ToList(), new List<int> { 5, 6 }, null, true);
            rqrightsDb.UpdateRequestsState(
                glpiCompletedRequest.Where(r => !glpiCanceledRequests.Select(c => c.IdRequestForRightsRequest).Contains(r.IdRequestForRightsRequest))
                    .ToList(), 4);
            rqrightsDb.UpdateRequestsState(glpiCanceledRequests.ToList(), 5);
            var glpiRequests = glpiDb.GetRequests(createionDate: DateTime.Now.AddDays(-1));
            rqrightsDb.UpdateExecutors(glpiRequests);
        }
    }
}
