using System;
using System.Collections.Generic;
using System.Text;
using MySql.Data;
using MySql.Data.MySqlClient;
using System.Linq;

namespace RequestForRights.GLPI.Sync
{
    class GlpiDb
    {
        private string _connectionString = null;
        private static readonly string CheckExistsRequestsQueryTemplate = @"SELECT ugrra.id_rqrights_request FROM udt_glpi_rqrights_request_assoc ugrra
            WHERE ugrra.id_rqrights_request IN (0{0})";

        private static readonly string InsertTicketQueryTemplate = @"INSERT INTO glpi_tickets (
                  entities_id, name, date, users_id_lastupdater, status, users_id_recipient, 
                  requesttypes_id, content, urgency, impact, priority, itilcategories_id, type, 
                  global_validation, slas_id_ttr, slas_id_tto, slalevels_id_ttr, 
                  sla_waiting_duration, ola_waiting_duration, olas_id_tto, olas_id_ttr, olalevels_id_ttr, 
                  waiting_duration, close_delay_stat, solve_delay_stat, takeintoaccount_delay_stat, actiontime, 
                  is_deleted, locations_id, validation_percent, date_creation)
                  SELECT IFNULL((SELECT ge.id FROM glpi_entities ge WHERE ge.name = @department LIMIT 1), 0), @title, NOW(), 0, 2, 0, 
                  6, @description, 3, 3, 3, @ticket_category, 2, 
                  1, 0, 0, 0, 
                  0, 0, 0, 0, 0, 
                  0, 0, 0, 0, 0, 
                  0, 0, 0, NOW()";

        private static readonly string InsertGlpiRqrightsIdsAssocQueryTemplate = @"INSERT INTO udt_glpi_rqrights_request_assoc (
                id_glpi_ticket, id_rqrights_request)
                VALUES (@id_glpi_ticket, @id_rqrights_request)";

        private static readonly string InsertTicketInitiatorQueryTemplate = @"INSERT INTO glpi_tickets_users (
                tickets_id, users_id, type, use_notification, alternative_email)
                  VALUES (@ticket_id, 
                  (SELECT gu.id FROM glpi_users gu WHERE LOWER(gu.name) = @login), 1, 0, 
                  (SELECT gue.email
                    FROM glpi_users gu JOIN glpi_useremails gue ON gu.id = gue.users_id AND gue.is_default = 1 
                    WHERE LOWER(gu.name) = @login))";

        private static readonly string InsertTicketExecutorGroupsQueryTemplate = @"INSERT INTO glpi_groups_tickets (tickets_id, groups_id, type)
                VALUES (@ticket_id, @group_id, 2);";

        private static readonly string GetTicketsQueryTemplate = @"SELECT gt.id AS id_glpi_ticket, ugrra.id_rqrights_request,
                      IFNULL(gt.name, '') AS name, IFNULL(gt.content, '') AS content, 
                      IFNULL(gt.date, gt.date_creation) AS date, gi.completename AS category, TRIM(CONCAT(IFNULL(gu.realname, ''), ' ', IFNULL(gu.firstname, ''))) AS inititator,
                      IFNULL(GROUP_CONCAT(gg.completename SEPARATOR ', '), '') AS executors_groups,
                      IFNULL(gis.content, '') AS solution, 
                      IFNULL(gis.snp,'') AS complete_user_snp, 
                      IFNULL(gis.name, '') AS complete_user_login
                    FROM glpi_tickets gt
                    INNER JOIN udt_glpi_rqrights_request_assoc ugrra ON gt.id = ugrra.id_glpi_ticket
                      LEFT JOIN glpi_tickets_users gtu ON gt.id = gtu.tickets_id
                      LEFT JOIN glpi_users gu ON gtu.users_id = gu.id
                      LEFT JOIN glpi_groups_tickets ggt ON gt.id = ggt.tickets_id
                      LEFT JOIN glpi_groups gg ON ggt.groups_id = gg.id
                      INNER JOIN glpi_itilcategories gi ON gt.itilcategories_id = gi.id
                      LEFT JOIN (SELECT gi.items_id, gi.content,
                                 TRIM(CONCAT(IFNULL(gu.realname, ''), ' ', IFNULL(gu.firstname, ''))) AS snp,
                                 gu.name
                                 FROM glpi_itilsolutions gi
                                 JOIN glpi_users gu ON gi.users_id = gu.id
                    WHERE gi.status IN(2,3) AND gi.itemtype = 'Ticket'
                    GROUP BY gi.items_id) gis ON gt.id = gis.items_id
                    WHERE gtu.type = 1 AND ggt.type = 2 {0}
                    GROUP BY gt.id";

        private static readonly string GetTicketManagersQueryTemplate = @"SELECT ggt.tickets_id, IFNULL(CONCAT('PWR\\', gu.name), '') AS login, 
              TRIM(CONCAT(IFNULL(gu.realname, ''), ' ', IFNULL(gu.firstname, ''))) AS snp, IFNULL(gum.email, '') AS email
            FROM glpi_groups_tickets ggt
              INNER JOIN glpi_groups_users ggu ON ggt.groups_id = ggu.groups_id
              INNER JOIN glpi_users gu ON ggu.users_id = gu.id
              INNER JOIN glpi_useremails gum ON gu.id = gum.users_id
            WHERE ggt.tickets_id IN ({0}) AND ggt.type = 2 AND ggu.is_manager = 1 AND 
                  gum.is_default = 1 AND gu.is_deleted = 0 AND gu.is_active = 1";

        private static readonly string GetTicketExecutorsQueryTemplate = @"SELECT gt.tickets_id, IFNULL(gt.content, '') AS content, 
             IFNULL(CONCAT('PWR\\',gu.name), '') AS login, 
              TRIM(CONCAT(IFNULL(gu.realname, ''), ' ', IFNULL(gu.firstname, ''))) AS name, 1 AS executor_type
            FROM glpi_tickettasks gt INNER JOIN glpi_users gu ON gt.users_id_tech = gu.id
            WHERE gt.tickets_id IN ({0})
            UNION ALL
            SELECT gt.tickets_id, gt.content, '', IFNULL(gg.completename, '') AS completename, 2 AS executor_type
            FROM glpi_tickettasks gt INNER JOIN glpi_groups gg ON gt.groups_id_tech = gg.id
            WHERE gt.tickets_id IN ({0})";

        public GlpiDb(string connectionString)
        {
            _connectionString = connectionString;
        }

        private static string PrepareCheckExistsRequestsQuery(List<RequestForRightsRequest> requests)
        {
            return string.Format(CheckExistsRequestsQueryTemplate,
                        requests.Count > 0 ? requests.Select(r => r.IdRequest.ToString()).Aggregate((v, acc) => v + ',' + acc) : "");
        }

        public List<RequestForRightsRequest> FilterExistsRequests(List<RequestForRightsRequest> requests)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();
                var checkExistsRequestsCommand = new MySqlCommand(
                    PrepareCheckExistsRequestsQuery(requests), connection);
                var reader = checkExistsRequestsCommand.ExecuteReader();
                var existsRequestsIds = new List<int>();
                while (reader.Read())
                {
                    existsRequestsIds.Add(reader.GetInt32(0));
                }
                return requests.Where(r => !existsRequestsIds.Any(id => id == r.IdRequest)).ToList();
            }
        }

        public List<long> InsertRequests(List<RequestForRightsRequest> requests)
        {
            var insertedTicketsIds = new List<long>();
            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        foreach (var request in requests)
                        {
                            var insertTicketCommand = new MySqlCommand(InsertTicketQueryTemplate, connection, transaction);
                            insertTicketCommand.Parameters.AddWithValue("@title", request.Description.Replace("\r\n", " "));
                            insertTicketCommand.Parameters.AddWithValue("@department", request.Requester.Department);
                            insertTicketCommand.Parameters.AddWithValue("@description", request.GetFullDescription());
                            insertTicketCommand.Parameters.AddWithValue("@ticket_category", request.IdRequestType + 114 /*В glpi id с 115 до 118, в rqrights id с 1 до 4*/);
                            insertTicketCommand.ExecuteNonQuery();
                            var lastInsertedId = insertTicketCommand.LastInsertedId;

                            var insertGlpiRqrightsIdsAssocCommand = new MySqlCommand(InsertGlpiRqrightsIdsAssocQueryTemplate, connection, transaction);
                            insertGlpiRqrightsIdsAssocCommand.Parameters.AddWithValue("@id_glpi_ticket", lastInsertedId);
                            insertGlpiRqrightsIdsAssocCommand.Parameters.AddWithValue("@id_rqrights_request", request.IdRequest);
                            insertGlpiRqrightsIdsAssocCommand.ExecuteNonQuery();

                            var insertTicketInitiatorCommand = new MySqlCommand(InsertTicketInitiatorQueryTemplate, connection, transaction);
                            insertTicketInitiatorCommand.Parameters.AddWithValue("@ticket_id", lastInsertedId);
                            insertTicketInitiatorCommand.Parameters.AddWithValue("@login", 
                                request.Requester.Login.Split('\\')?[1]?.ToLowerInvariant() ?? 
                                request.Requester.Login?.ToLowerInvariant());
                            insertTicketInitiatorCommand.ExecuteNonQuery();

                            foreach(var dep in request.ResourceResponsibleDepartments)
                            {
                                var insertTicketExecutorGroups = new MySqlCommand(InsertTicketExecutorGroupsQueryTemplate, connection, transaction);
                                insertTicketExecutorGroups.Parameters.AddWithValue("@ticket_id", lastInsertedId);
                                insertTicketExecutorGroups.Parameters.AddWithValue("@group_id",
                                    ConvertRqrightsRespDepartmentToExecutorGroup(dep.IdResourceResponsibleDepartment));
                                insertTicketExecutorGroups.ExecuteNonQuery();
                            }

                            insertedTicketsIds.Add(lastInsertedId);
                        }
                    } catch(MySqlException)
                    {
                        transaction.Rollback();
                        return null;
                    }
                    transaction.Commit();
                }
            }
            return insertedTicketsIds;
        }

        private int ConvertRqrightsRespDepartmentToExecutorGroup(int idResourceResponsibleDepartment)
        {
            switch (idResourceResponsibleDepartment)
            {
                case 1: return 12;
                case 2: return 46;
                case 3: return 52;
                case 4: return 48;
                case 5: return 37;
                case 6: return 47;
                default: throw new ApplicationException("GlpiDb.ConvertRqrightsRespDepartmentToExecutorGroup: Неизвестный отдел сопровождения");
            }
        }

        public List<GlpiRequest> GetRequests(List<long> glpiIds = null, List<long> rqrightsIds = null, List<int> statusIds = null, DateTime? createionDate = null, bool onlyCanceled = false)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();
                var where = GetRequestsBuildWhere(glpiIds, rqrightsIds, statusIds, createionDate, onlyCanceled);
                var getTicketCommand = new MySqlCommand(string.Format(GetTicketsQueryTemplate, where), connection);
                var reader = getTicketCommand.ExecuteReader();
                var requests = new List<GlpiRequest>();
                while (reader.Read())
                {
                    var request = ReadCurrentRequestBaseInfoFromMySqlDataReader(reader);
                    requests.Add(request);
                }
                reader.Close();
                glpiIds = requests.Select(r => (long)r.IdGlpiRequest).ToList();

                if (glpiIds.Count == 0) return requests;
                var idsString = glpiIds.Select(r => r.ToString()).Aggregate((v, acc) => v + ", " + acc);


                var getTicketManagersCommand = new MySqlCommand(string.Format(GetTicketManagersQueryTemplate, idsString), connection);
                reader = getTicketManagersCommand.ExecuteReader();
                GlpiRequest currentTicket = null;
                while (reader.Read())
                {
                    var idRequest = reader.GetInt32(0);
                    if (currentTicket == null || currentTicket.IdGlpiRequest != idRequest)
                    {
                        currentTicket = requests.FirstOrDefault(r => r.IdGlpiRequest == idRequest);
                    }
                    if (currentTicket == null)
                    {
                        throw new ApplicationException("GlpiDb.GetRequests: Несогласованность данных в запросах");
                    }
                    currentTicket.Managers.Add(ReadCurrentManagerFromMySqlDataReader(reader));
                }
                reader.Close();
                currentTicket.Managers = currentTicket.Managers.Distinct().ToList();

                var getTicketExecutorsCommand = new MySqlCommand(string.Format(GetTicketExecutorsQueryTemplate, idsString), connection);
                reader = getTicketExecutorsCommand.ExecuteReader();
                currentTicket = null;
                while (reader.Read())
                {
                    var idRequest = reader.GetInt32(0);
                    if (currentTicket == null || currentTicket.IdGlpiRequest != idRequest)
                    {
                        currentTicket = requests.FirstOrDefault(r => r.IdGlpiRequest == idRequest);
                    }
                    if (currentTicket == null)
                    {
                        throw new ApplicationException("GlpiDb.GetRequests: Несогласованность данных в запросах");
                    }
                    currentTicket.Executors.Add(ReadCurrentExecutorFromMySqlDataReader(reader));
                }
                reader.Close();

                return requests;
            }
        }

        private GlpiRequestManager ReadCurrentManagerFromMySqlDataReader(MySqlDataReader reader)
        {
            return new GlpiRequestManager
            {
                Login = reader.GetString(1),
                Snp = reader.GetString(2),
                Email = reader.GetString(3)
            };
        }

        private GlpiRequestExecutor ReadCurrentExecutorFromMySqlDataReader(MySqlDataReader reader)
        {
            return new GlpiRequestExecutor
            {
                Content = reader.GetString(1),
                Login = reader.GetString(2),
                Name = reader.GetString(3),
                Type = reader.GetInt32(4)
            };
        }

        private GlpiRequest ReadCurrentRequestBaseInfoFromMySqlDataReader(MySqlDataReader reader)
        {
            return new GlpiRequest
            {
                IdGlpiRequest = reader.GetInt32(0),
                IdRequestForRightsRequest = reader.GetInt32(1),
                Name = reader.GetString(2),
                Content = reader.GetString(3),
                OpenDate = reader.GetDateTime(4),
                Cateogry = reader.GetString(5),
                Initiator = reader.GetString(6),
                ExecutorsGroups = reader.GetString(7),
                CompleteDescription = reader.GetString(8),
                CompleteUserSnp = reader.GetString(9),
                CompleteUserLogin = reader.GetString(10),
                Executors = new List<GlpiRequestExecutor>(),
                Managers = new List<GlpiRequestManager>()
            };
        }

        private string GetRequestsBuildWhere(List<long> glpiIds = null, List<long> rqRightsIds = null, List<int> statusIds = null, DateTime? createionDate = null, bool onlyCanceled = false)
        {
            var where = "";
            if (glpiIds != null)
            {
                where += " AND gt.id IN (" + (glpiIds.Count > 0 ? glpiIds.Select(r => r.ToString()).Aggregate((v, acc) => v + "," + acc) : "0") + ")";
            }
            if (rqRightsIds != null)
            {
                where += " AND ugrra.id_rqrights_request IN (" + 
                    (rqRightsIds.Count > 0 ? rqRightsIds.Select(r => r.ToString()).Aggregate((v, acc) => v + "," + acc) : "0") + ")";
            }
            if (statusIds != null)
            {
                where += " AND status IN (" + (statusIds.Count > 0 ? statusIds.Select(r => r.ToString()).Aggregate((v, acc) => v + "," + acc) : "0") + ")";
            }
            if (createionDate != null)
            {
                where += " AND gt.date_creation > STR_TO_DATE('" + createionDate.Value.ToString("dd.MM.yyyy hh:mm:ss") + "', '%d.%m.%Y %H:%i:%s')";
            }
            if (onlyCanceled)
            {
                where += @" AND  EXISTS(
                    SELECT *
                    FROM glpi_itilsolutions gi1
                    WHERE gi1.itemtype = 'Ticket' AND gi1.items_id = gt.id AND gi1.status IN(2, 3) AND gi1.solutiontypes_id = 2)";
            }
            if (string.IsNullOrEmpty(where)) return "AND 1=0";
            return where;
        }
    }
}
