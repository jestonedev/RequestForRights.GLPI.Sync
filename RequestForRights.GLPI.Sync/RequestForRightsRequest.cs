using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

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

        public string GetFullDescription()
        {
            var description = "&lt;p&gt;";
            foreach(var user in RequestForRightsUsers)
            {
                description += "&lt;b&gt;Сотрудник:&lt;/b&gt; " + user.Snp + "&lt;br/&gt;";
                if (!string.IsNullOrEmpty(user.Post))
                {
                    description += "&lt;b&gt;Должность:&lt;/b&gt; " + user.Post + "&lt;br/&gt;";
                }
                if (!string.IsNullOrEmpty(user.Phone))
                {
                    description += "&lt;b&gt;Телефон:&lt;/b&gt; " + user.Phone + "&lt;br/&gt;";
                }
                if (!string.IsNullOrEmpty(user.Description))
                {
                    description += "&lt;b&gt;Примечание:&lt;/b&gt; " + user.Description + "&lt;br/&gt;";
                }

                description += "&lt;br/&gt;";
                foreach (var right in user.RequestForRightsRights)
                {
                    description += "&lt;b&gt;Ресурс:&lt;/b&gt; " + right.ResourceName + "&lt;br/&gt;";
                    description += "&lt;b&gt;Право:&lt;/b&gt; " + right.ResourceRightName + "&lt;br/&gt;";
                    if (!string.IsNullOrEmpty(right.ResourceRightDescription))
                    {
                        description += "&lt;b&gt;Примечание:&lt;/b&gt; " + right.ResourceRightDescription + "&lt;br/&gt;";
                    }
                    description += "&lt;br/&gt;";
                }
            }
            description += "&lt;a href=&quot;http://rqrights/Request/Detail/"+IdRequest.ToString()+ "&quot;&gt;Заявка в АИС &quot;Реестр информационных систем и прав доступа к ним&quot;&lt;/a&gt;";

            description += "&lt;/p&gt;";
            return description.Replace("\r\n", " ");
        }

        public bool HasResourceResponsibleDepartment(RequestForRightsResourceResponsibleDepartment dep)
        {
            return ResourceResponsibleDepartments.Any(r => r.IdResourceResponsibleDepartment == dep.IdResourceResponsibleDepartment);
        }
    }
}
