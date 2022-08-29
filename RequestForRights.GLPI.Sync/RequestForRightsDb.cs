using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;

namespace RequestForRights.GLPI.Sync
{
    class RequestForRightsDb
    {
        private string _connectionString = null;

        public RequestForRightsDb(string connectionString)
        {
            _connectionString = connectionString;
        }

        private static readonly string BaseInfoQuery = @"SELECT r.IdRequest, r.IdRequestType, acl.IdUser, acl.Snp, acl.Login, acl.Email,
              COALESCE(r.Description, '') AS Description, 
              COALESCE(d1.Name, d.Name) AS Department
            FROM Requests r 
              INNER JOIN AclUsers acl ON r.IdUser = acl.IdUser
              LEFT JOIN Departments d ON acl.IdDepartment = d.IdDepartment
              LEFT JOIN Departments d1 ON d.IdParentDepartment = d1.IdDepartment
            WHERE r.Deleted <> 1 AND r.IdCurrentRequestStateType = 3
            ORDER BY r.IdRequest";

        private static readonly string RightsInfoQuery = @"
                    SELECT r.IdRequest, rs.Name AS ResourceName, rr.Name AS ResourceRightName, 
  ru.IdRequestUser, ru.Snp, COALESCE(ru.Post, '') AS Post, COALESCE(ru.Phone, '') AS Phone, ru.Department, 
    'Делегировать права сотруднику ' + rud.Snp + 
    COALESCE(', '+LOWER(rud.Post), '') +
    COALESCE(', тел. ' + rud.Phone + ', ', '') + 
    ' на период с '+CONVERT(VARCHAR, druei.DelegateFromDate, 104) +
    COALESCE(' по '+CONVERT(VARCHAR, CASE WHEN druei.DelegateToDate = '9999-31-12 23:59:59' THEN NULL ELSE druei.DelegateToDate END, 104), ' бессрочно') +
    COALESCE('.&lt;br/&gt;'+rua.Description, '') AS RequestUserDescription,    
    COALESCE(rura.Descirption, '') AS ResourceRightDescription, 
    COALESCE(rs.IdResourceResponsibleDepartment, 0) AS IdResourceResponsibleDepartment, 
    COALESCE(rrd.Name, '') AS ResourceResponsibleDepartment, COALESCE(ru.Unit, '') AS Unit,
    rrgt.Name AS RequestRightGrantType,
    COALESCE(ru.Office, '') AS Office,
    rs.IdResource
    FROM Requests r 
      INNER JOIN RequestUserAssocs rua ON r.IdRequest = rua.IdRequest
      LEFT JOIN DelegationRequestUsersExtInfo druei ON rua.IdRequestUserAssoc = druei.IdRequestUserAssoc
      LEFT JOIN RequestUsers rud ON druei.IdDelegateToUser = rud.IdRequestUser
      INNER JOIN RequestUsers ru ON rua.IdRequestUser = ru.IdRequestUser
      INNER JOIN RequestUserRightAssocs rura ON rua.IdRequestUserAssoc = rura.IdRequestUserAssoc
      INNER JOIN RequestRightGrantTypes rrgt ON rura.IdRequestRightGrantType = rrgt.IdRequestRightGrantType
      INNER JOIN ResourceRights rr ON rura.IdResourceRight = rr.IdResourceRight
      INNER JOIN Resources rs ON rr.IdResource = rs.IdResource
      LEFT JOIN ResourceResponsibleDepartments rrd ON rs.IdResourceResponsibleDepartment = rrd.IdResourceResponsibleDepartment
    WHERE r.Deleted <> 1 AND r.IdCurrentRequestStateType = 3 AND r.IdRequestType = 4 AND
      rua.Deleted <> 1 AND rura.Deleted <> 1
    UNION ALL
    SELECT r.IdRequest, rs.Name AS ResourceName, rr.Name AS ResourceRightName, 
  ru.IdRequestUser, ru.Snp, COALESCE(ru.Post, '') AS Post, COALESCE(ru.Phone, '') AS Phone, ru.Department,
  COALESCE(rua.Description, '') AS RequestUserDescription,    
    COALESCE(rura.Descirption, '') AS ResourceRightDescription, 
    COALESCE(rs.IdResourceResponsibleDepartment, 0) AS IdResourceResponsibleDepartment, 
    COALESCE(rrd.Name, '') AS ResourceResponsibleDepartment, COALESCE(ru.Unit, '') AS Unit,
    rrgt.Name AS RequestRightGrantType,
    COALESCE(ru.Office, '') AS Office,
      rs.IdResource
    FROM Requests r 
      INNER JOIN RequestUserAssocs rua ON r.IdRequest = rua.IdRequest
      INNER JOIN RequestUsers ru ON rua.IdRequestUser = ru.IdRequestUser
      INNER JOIN RequestUserRightAssocs rura ON rua.IdRequestUserAssoc = rura.IdRequestUserAssoc
      INNER JOIN RequestRightGrantTypes rrgt ON rura.IdRequestRightGrantType = rrgt.IdRequestRightGrantType
      INNER JOIN ResourceRights rr ON rura.IdResourceRight = rr.IdResourceRight
      INNER JOIN Resources rs ON rr.IdResource = rs.IdResource
      LEFT JOIN ResourceResponsibleDepartments rrd ON rs.IdResourceResponsibleDepartment = rrd.IdResourceResponsibleDepartment
    WHERE r.Deleted <> 1 AND r.IdCurrentRequestStateType = 3 AND r.IdRequestType IN (1, 2) AND
      rua.Deleted <> 1 AND rura.Deleted <> 1
    UNION ALL
    SELECT r.IdRequest, v.ResourceName, v.ResourceRightName, 
       ru.IdRequestUser, ru.Snp, COALESCE(ru.Post, '') AS Post, COALESCE(ru.Phone, '') AS Phone, ru.Department,
        COALESCE(rua.Description, '') AS RequestUserDescription, COALESCE(v.ResourceRightDescription, '') AS ResourceRightDescription, 
        COALESCE(v.IdResourceResponsibleDepartment, 0) AS IdResourceResponsibleDepartment, 
        COALESCE(v.ResourceResponsibleDepartment, '') AS ResourceResponsibleDepartment, COALESCE(ru.Unit, '') AS Unit,
    'Забрать право' AS RequestRightGrantType,
    COALESCE(ru.Office, '') AS Office,
    v.IdResource
    FROM (
    SELECT rs.IdRequest, MIN(rs.Date) AS CreateDate
    FROM RequestStates rs
      INNER JOIN Requests r ON rs.IdRequest = r.IdRequest
    GROUP BY rs.IdRequest) rcd 
      INNER JOIN Requests r ON rcd.IdRequest = r.IdRequest
      INNER JOIN RequestUserAssocs rua ON r.IdRequest = rua.IdRequest
      INNER JOIN RequestUsers ru ON rua.IdRequestUser = ru.IdRequestUser
      CROSS APPLY (
      SELECT rr.Name AS  ResourceRightName, rs.Name AS ResourceName, rura2.Descirption AS ResourceRightDescription, 
        rrd.IdResourceResponsibleDepartment, rrd.Name  AS ResourceResponsibleDepartment, rs.IdResource
      FROM RequestUserAssocs rua2 
      INNER JOIN RequestUserRightAssocs rura2 ON rua2.IdRequestUserAssoc = rura2.IdRequestUserAssoc AND 
        (rura2.GrantedTo IS NULL OR rura2.GrantedTo >= rcd.CreateDate) AND rura2.GrantedFrom <= rcd.CreateDate AND rua.IdRequestUser = rua2.IdRequestUser
      INNER JOIN ResourceRights rr ON rura2.IdResourceRight = rr.IdResourceRight
      INNER JOIN Resources rs ON rr.IdResource = rs.IdResource
      LEFT JOIN ResourceResponsibleDepartments rrd ON rs.IdResourceResponsibleDepartment = rrd.IdResourceResponsibleDepartment
      WHERE rua2.Deleted <> 1 AND rura2.Deleted <> 1 AND rs.Deleted <> 1 AND rr.Deleted <> 1
      GROUP BY rr.Name,  rs.Name, rura2.Descirption, rrd.IdResourceResponsibleDepartment, rrd.Name, rs.IdResource) v
    WHERE r.Deleted <> 1 AND rua.Deleted <> 1 AND r.IdRequestType = 3 AND r.IdCurrentRequestStateType = 3
    ORDER BY r.IdRequest";

        private static readonly string UpdateRequestStateQueryTemplate = @"INSERT INTO RequestStates(IdRequestStateType, IdRequest, Date, Deleted)
            VALUES(@idRequestStateType, @idRequest, CURRENT_TIMESTAMP, 0);";

        private static readonly string DeleteRequestExecutorsQueryTemplate = @"DELETE FROM RequestExecutors WHERE IdRequest = @IdRequest";
        private static readonly string InsertRequestExecutorQueryTemplate = @"INSERT INTO RequestExecutors(IdRequest, Login) VALUES(@IdRequest, @Login)";

        public void UpdateExecutors(List<GlpiRequest> glpiRequests)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                var transaction = connection.BeginTransaction();
                foreach (var request in glpiRequests)
                {
                    var idRequest = request.IdRequestForRightsRequest;
                    var executors = request.Executors.Where(r => r.Type == 1);
                    var deleteRequestStateQuery = new SqlCommand(DeleteRequestExecutorsQueryTemplate, connection, transaction);
                    deleteRequestStateQuery.Parameters.AddWithValue("@IdRequest", idRequest);
                    deleteRequestStateQuery.ExecuteNonQuery();
                    foreach(var executor in executors)
                    {
                        var insertRequestStateQuery = new SqlCommand(InsertRequestExecutorQueryTemplate, connection, transaction);
                        insertRequestStateQuery.Parameters.AddWithValue("@IdRequest", idRequest);
                        insertRequestStateQuery.Parameters.AddWithValue("@Login", executor.Login);
                        insertRequestStateQuery.ExecuteNonQuery();
                    }
                }
                transaction.Commit();
            }
        }

        private RequestForRightsRequest ReadCurrentRequestBaseInfoFromSqlDataReader(SqlDataReader reader)
        {
            return new RequestForRightsRequest
            {
                IdRequest = reader.GetInt32(0),
                IdRequestType = reader.GetInt32(1),
                Requester = new RequestForRightsRequester
                {
                    IdUser = reader.GetInt32(2),
                    Snp = reader.GetString(3),
                    Login = reader.GetString(4),
                    Email = reader.GetString(5),
                    Department = reader.GetString(7)
                },
                Description = reader.GetString(6),
                RequestForRightsUsers = new List<RequestForRightsUser>(),
                ResourceResponsibleDepartments = new List<RequestForRightsResourceResponsibleDepartment>()
            };
        }

        private RequestForRightsRight ReadCurrentRightFromSqlDataReader(SqlDataReader reader)
        {
            return new RequestForRightsRight
            {
                ResourceName = reader.GetString(1),
                ResourceRightName = reader.GetString(2),
                ResourceRightDescription = reader.GetString(9),
                RequestRightGrantType = reader.GetString(13),
                IdResource = reader.GetInt32(15)
            };
        }

        private RequestForRightsUser ReadCurrentUserFromSqlDataReader(SqlDataReader reader)
        {
            return new RequestForRightsUser
            {
                IdRequestUser = reader.GetInt32(3),
                Snp = reader.GetString(4),
                Post = reader.GetString(5),
                Phone = reader.GetString(6),
                Department = reader.GetString(7),
                Unit = reader.GetString(12),
                Description = reader.GetString(8),
                Office = reader.GetString(14)
            };
        }

        private RequestForRightsResourceResponsibleDepartment ReadResourceResponsibleDepartmentFromSqlDataReader(SqlDataReader reader)
        {
            return new RequestForRightsResourceResponsibleDepartment
            {
                IdResourceResponsibleDepartment = reader.GetInt32(10),
                Name = reader.GetString(11)
            };
        }

        public List<RequestForRightsRequest> GetRequestsOnExecution()
        {
            var requests = new List<RequestForRightsRequest>();
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                var baseInfoCommand = new SqlCommand(BaseInfoQuery, connection);
                using (var reader = baseInfoCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var request = ReadCurrentRequestBaseInfoFromSqlDataReader(reader);
                        requests.Add(request);
                    }
                }
                var rightsInfoCommand = new SqlCommand(RightsInfoQuery, connection);
                using (var reader = rightsInfoCommand.ExecuteReader())
                {
                    var currentRequest = requests.FirstOrDefault();
                    while (reader.Read())
                    {
                        var IdRequest = reader.GetInt32(0);
                        if (currentRequest == null || currentRequest.IdRequest != IdRequest)
                        {
                            currentRequest = requests.FirstOrDefault(r => r.IdRequest == IdRequest);
                        }
                        if (currentRequest == null)
                        {
                            throw new ApplicationException("RequestForRightsDb.GetRequestsOnExecution: Несогласованность данных в запросах");
                        }
                        var idRequestUser = reader.GetInt32(3);
                        var requestUserDescription = reader.GetString(8);
                        var currentUser = currentRequest.RequestForRightsUsers.FirstOrDefault(r => r.IdRequestUser == idRequestUser && r.Description == requestUserDescription);
                        var currentRight = ReadCurrentRightFromSqlDataReader(reader);
                        if (currentUser == null)
                        {
                            var requestUser = ReadCurrentUserFromSqlDataReader(reader);
                            requestUser.RequestForRightsRights = new List<RequestForRightsRight> { currentRight };
                            currentRequest.RequestForRightsUsers.Add(requestUser);
                        } else
                        {
                            if (!currentUser.HasRight(currentRight))
                            {
                                currentUser.RequestForRightsRights.Add(currentRight);
                            }
                        }
                        var dep = ReadResourceResponsibleDepartmentFromSqlDataReader(reader);

                        if (!currentRequest.HasResourceResponsibleDepartment(dep))
                            currentRequest.ResourceResponsibleDepartments.Add(dep);
                        var cmtDep = new RequestForRightsResourceResponsibleDepartment
                            {
                            IdResourceResponsibleDepartment = 3
                            };
                        if ((currentRight.ResourceName == "Учетная запись пользователя" || currentRequest.IdRequestType == 3) &&
                            !currentRequest.HasResourceResponsibleDepartment(cmtDep))
                        {
                            currentRequest.ResourceResponsibleDepartments.Add(cmtDep);
                        }
                    }
                    if (currentRequest.IdRequestType == 3 && !currentRequest.ResourceResponsibleDepartments.Any())
                    {
                        currentRequest.ResourceResponsibleDepartments.AddRange(new[] {
                            new RequestForRightsResourceResponsibleDepartment { IdResourceResponsibleDepartment = 3 },
                            new RequestForRightsResourceResponsibleDepartment { IdResourceResponsibleDepartment = 2 }
                        });
                    }
                }
            }

            return requests;
        }

        public void UpdateRequestsState(List<GlpiRequest> requests, int idRequestStateType)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                var transaction = connection.BeginTransaction();
                foreach (var request in requests)
                {
                    if (idRequestStateType == 5)
                    {
                        request.CompleteDescription = Regex.Replace(request.CompleteDescription, "&lt;.+?&gt;", "");

                        var userIdCommand = new SqlCommand("SELECT au.IdUser FROM AclUsers au WHERE au.Deleted <> 1 AND au.Login = @login", connection, transaction);
                        userIdCommand.Parameters.AddWithValue("login", "pwr\\" + request.CompleteUserLogin.ToLowerInvariant());
                        var userIdObj = userIdCommand.ExecuteScalar();
                        var userId = -1;
                        if (userIdObj == null)
                        {
                            var addUserCommand = new SqlCommand("INSERT INTO AclUsers(Login, IdDepartment, Snp) VALUES(@login, 24, @snp)", connection, transaction);
                            addUserCommand.Parameters.AddWithValue("login", "pwr\\" + request.CompleteUserLogin.ToLowerInvariant());
                            addUserCommand.Parameters.AddWithValue("snp", "pwr\\" + request.CompleteUserSnp.ToLowerInvariant());
                            addUserCommand.ExecuteNonQuery();

                            userId = (int)userIdCommand.ExecuteScalar();

                            var addUserRightsCommand = new SqlCommand("INSERT INTO AclUserAclRoles(IdUser, IdRole) VALUES(@idUser, @idRole)", connection, transaction);
                            addUserRightsCommand.Parameters.AddWithValue("idUser", userId);
                            addUserRightsCommand.Parameters.AddWithValue("idRole", 6);
                            addUserRightsCommand.ExecuteNonQuery();
                        }
                        else
                            userId = (int)userIdObj;


                        var addAgreementCommand = new SqlCommand(
                            "INSERT INTO RequestAgreements(AgreementDescription, AgreementDate, IdUser, IdRequest, IdAgreementState, IdAgreementType) VALUES(@description, @date, @idUser, @idReqeust, 3, 2)",
                                connection, transaction);
                        addAgreementCommand.Parameters.AddWithValue("description", request.CompleteDescription);
                        addAgreementCommand.Parameters.AddWithValue("date", DateTime.Now);
                        addAgreementCommand.Parameters.AddWithValue("idUser", userId);
                        addAgreementCommand.Parameters.AddWithValue("idReqeust", request.IdRequestForRightsRequest);
                        addAgreementCommand.ExecuteNonQuery();
                    }

                    var command = new SqlCommand(UpdateRequestStateQueryTemplate, connection, transaction);
                    command.Parameters.AddWithValue("idRequestStateType", idRequestStateType);
                    command.Parameters.AddWithValue("idRequest", request.IdRequestForRightsRequest);
                    command.ExecuteNonQuery();
                }
                transaction.Commit();
            }
        }
    }
}
