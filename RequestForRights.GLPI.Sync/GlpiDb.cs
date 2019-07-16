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
                  VALUES (0, @title, NOW(), 0, 2, 0, 
                  6, @description, 3, 3, 3, @ticket_category, 2, 
                  1, 0, 0, 0, 
                  0, 0, 0, 0, 0, 
                  0, 0, 0, 0, 0, 
                  0, 0, 0, NOW())";

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

        public GlpiDb(string connectionString)
        {
            _connectionString = connectionString;
        }

        private static string PrepareCheckExistsRequestsQuery(List<RequestForRightsRequest> requests)
        {
            return string.Format(CheckExistsRequestsQueryTemplate,
                        requests.Select(r => r.IdRequest.ToString()).Aggregate((v, acc) => v + ',' + acc));
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
                            var insertTicketCommand = new MySqlCommand(InsertTicketQueryTemplate, connection);
                            insertTicketCommand.Parameters.AddWithValue("@title", request.Description.Replace("\r\n", " "));
                            insertTicketCommand.Parameters.AddWithValue("@description", request.GetFullDescription());
                            insertTicketCommand.Parameters.AddWithValue("@ticket_category", request.IdRequestType + 114 /*В glpi id с 115 до 118, в rqrights id с 1 до 4*/);
                            insertTicketCommand.ExecuteNonQuery();
                            var lastInsertedId = insertTicketCommand.LastInsertedId;

                            var insertGlpiRqrightsIdsAssocCommand = new MySqlCommand(InsertGlpiRqrightsIdsAssocQueryTemplate, connection);
                            insertGlpiRqrightsIdsAssocCommand.Parameters.AddWithValue("@id_glpi_ticket", lastInsertedId);
                            insertGlpiRqrightsIdsAssocCommand.Parameters.AddWithValue("@id_rqrights_request", request.IdRequest);
                            insertGlpiRqrightsIdsAssocCommand.ExecuteNonQuery();

                            var insertTicketInitiatorCommand = new MySqlCommand(InsertTicketInitiatorQueryTemplate, connection);
                            insertTicketInitiatorCommand.Parameters.AddWithValue("@ticket_id", lastInsertedId);
                            insertTicketInitiatorCommand.Parameters.AddWithValue("@login", 
                                request.Requester.Login.Split('\\')?[1]?.ToLowerInvariant() ?? 
                                request.Requester.Login?.ToLowerInvariant());
                            insertTicketInitiatorCommand.ExecuteNonQuery();

                            foreach(var dep in request.ResourceResponsibleDepartments)
                            {
                                var insertTicketExecutorGroups = new MySqlCommand(InsertTicketExecutorGroupsQueryTemplate, connection);
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
                case 3: return 34;
                case 4: return 48;
                case 5: return 37;
                case 6: return 47;
                default: throw new ApplicationException("ConvertRqrightsRespDepartmentToExecutorGroup: Неизвестный отдел сопровождения");
            }
        }

        public List<GlpiRequest> GetRequests(List<long> ids = null, List<long> requesttypesIds = null, DateTime? createionDate = null)
        {
            return null;
        }
    }
}
