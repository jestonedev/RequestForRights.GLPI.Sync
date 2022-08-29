using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Text;

namespace RequestForRights.GLPI.Sync
{
    public class SmtpReporter
    {
        private readonly string _host;
        private readonly int _port;
        private readonly string _from;

        public SmtpReporter(string host, int port, string from)
        {
            _host = host;
            _port = port;
            _from = from;
        }

        public void SendMail(string subject, string body, List<string> to)
        {
            using (var smtp = new SmtpClient(_host, _port))
            {
                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_from),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };
                foreach (var address in to)
                {
                    mailMessage.To.Add(new MailAddress(address));
                }
                smtp.Send(mailMessage);
            }
        }

        public string BuildMailBodyForNewGlpiRequest(GlpiRequest request)
        {
            var body = "";

            body += string.Format("<div>GLPI URL: <a href=\"http://glpi.mcs.br/index.php?redirect=ticket_{0}\">http://glpi.mcs.br/index.php?redirect=ticket_{0}</a></div>", request.IdGlpiRequest);
            body += "<p class=\"description\"><strong>Заявка: Описание</strong></p>";
            body += string.Format("<p><span style=\"color: #8b8c8f; font-weight: bold; text-decoration: underline;\">Заголовок</span>: {0}<br />",
                request.Name);
            body += string.Format("<span style=\"color: #8b8c8f; font-weight: bold; text-decoration: underline;\">Инициатор запроса</span>: {0}<br />",
                request.Initiator);
            body += string.Format("<span style=\"color: #8b8c8f; font-weight: bold; text-decoration: underline;\">Дата открытия</span>: {0}<br />",
                request.OpenDate.ToString("yyyy-MM-dd HH:mm"));
            body += "<span style=\"color: #8b8c8f; font-weight: bold; text-decoration: underline;\">Источник запроса</span>: Другой</p>";
            body += "<p><span style=\"color: #8b8c8f; font-weight: bold; text-decoration: underline;\">Статус</span>: В работе (назначена)<br />";
            body += string.Format("<span style=\"color: #8b8c8f; font-weight: bold; text-decoration: underline;\">Назначено группам</span>: {0}</p>",
                request.ExecutorsGroups);
            body += string.Format("<p><span style=\"color: #8b8c8f; font-weight: bold; text-decoration: underline;\">Категория</span>: {0}<br />",
                request.Cateogry);
            body += string.Format("<span style=\"color: #8b8c8f; font-weight: bold; text-decoration: underline;\">Описание</span>:<br />{0}</p>",
                request.Content.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\""));
            body += "<br /><br />--<br /><br />Сгенерировано автоматически GLPI";
            return body;
        }

        internal void SendMailToSmevExecutor(List<RequestForRightsRequest> requests, List<string> smtpToEmails)
        {
            var body = "";
            var subject = "Подключение/отключение сотрудников к СМЭВ (smart-route)";
            foreach (var request in requests) {
                foreach(var user in request.RequestForRightsUsers)
                {
                    foreach(var right in user.RequestForRightsRights)
                    {
                        if (right.IdResource == 288)
                        {
                            body += string.Format("<br><span style=\"text-decoration: underline;\">{0}</span> {1} {2} ({3}).", right.RequestRightGrantType,
                                right.RequestRightGrantType == "Забрать право" ? "у сотрудника" : "сотруднику",  user.Snp, user.Department);

                            if (!string.IsNullOrWhiteSpace(right.ResourceRightDescription))
                                body += string.Format(" Примечание к праву: <span style=\"color: red\">{0}</span>.", right.ResourceRightDescription);
                        }
                    }
                }
            }
            if (!string.IsNullOrWhiteSpace(body))
            {
                body = "<b>Перечень запросов:</b>" + body;
                SendMail(subject, body, smtpToEmails);
            }
        }

        public string BuildMailTitleForNewGlpiRequest(GlpiRequest request)
        {
            return string.Format("[GLPI #{0}] Новая заявка {1}", request.IdGlpiRequest.ToString("D7"), request.Name);
        }
    }
}
