module FsLandsatApi.Services.DbUserTargetService

open System
open FsLandsatApi.Models.User
open FsLandsatApi.Options
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open MySql.Data.MySqlClient

let tryCreateTarget
    (log: string -> unit)
    (connection: MySqlConnection)
    (target: Target) : Result<Target, string> =
    try
        let queryStringRaw = "INSERT INTO Targets (TargetGuid, ScenePath, SceneRow, Latitude, Longitude, MinCloudCover, MaxCloudCover, NotificationOffset) VALUES (@TargetGuid, @ScenePath, @SceneRow, @Latitude, @Longitude, @MinCloudCover, @MaxCloudCover, @NotificationOffset)"
        
        use queryCommand = new MySqlCommand(queryStringRaw, connection)
        queryCommand.Parameters.AddWithValue("@TargetGuid", target.Id) |> ignore
        queryCommand.Parameters.AddWithValue("@ScenePath", target.Path) |> ignore
        queryCommand.Parameters.AddWithValue("@SceneRow", target.Row) |> ignore
        queryCommand.Parameters.AddWithValue("@Latitude", target.Latitude) |> ignore
        queryCommand.Parameters.AddWithValue("@Longitude", target.Longitude) |> ignore
        queryCommand.Parameters.AddWithValue("@MinCloudCover", target.MinCloudCoverFilter) |> ignore
        queryCommand.Parameters.AddWithValue("@MaxCloudCover", target.MaxCloudCoverFilter) |> ignore
        queryCommand.Parameters.AddWithValue("@NotificationOffset", DateTime.MinValue.Add(target.NotificationOffset)) |> ignore
        
        let queryStringToLog = 
            queryStringRaw
                .Replace("@TargetGuid", $"\"{target.Id}\"")
                .Replace("@ScenePath", $"\"{target.Path}\"")
                .Replace("@SceneRow", $"\"{target.Row}\"")
                .Replace("@Latitude", $"\"{target.Latitude}\"")
                .Replace("@Longitude", $"\"{target.Longitude}\"")
                .Replace("@MinCloudCover", $"\"{target.MinCloudCoverFilter}\"")
                .Replace("@MaxCloudCover", $"\"{target.MaxCloudCoverFilter}\"")
                .Replace("@NotificationOffset", $"\"{DateTime.MinValue.Add(target.NotificationOffset)}\"")
        log(queryStringToLog)
        
        let _ = queryCommand.ExecuteNonQuery()  // in case of a duplicate, will throw error instead
        Ok target
    with
    | ex ->
        Error ex.Message
        
// Creates an entry in the join table that links a user and a target
let tryCreateJoinEntry
    (log: string -> unit)
    (connection: MySqlConnection)
    (target: Target) : Result<Target, string> =
    try
        let queryStringRaw =
            "INSERT INTO UsersTargets (UserGuid, TargetGuid) VALUES (@UserGuid, @TargetGuid)"
        
        use queryCommand = new MySqlCommand(queryStringRaw, connection)
        queryCommand.Parameters.AddWithValue("@UserGuid", target.UserId) |> ignore
        queryCommand.Parameters.AddWithValue("@TargetGuid", target.Id) |> ignore
        
        let queryStringToLog = 
            queryStringRaw
                .Replace("@UserGuid", $"{target.UserId}")
                .Replace("@TargetGuid", $"{target.Id}")
        log(queryStringToLog)
        
        let _ = queryCommand.ExecuteNonQuery()  // in case of a duplicate, will throw error instead
        Ok target
    with
    | ex ->
        Error ex.Message
        
let parseTargetsFromDataReader (reader: MySqlDataReader) (logger: ILogger) =
    let mutable parsedTargetsList: Target list = []
    while reader.Read() do
        try
            let target =
                { UserId = reader.GetGuid(0)
                  Id = reader.GetGuid(1)
                  Path = reader.GetInt32(2)
                  Row = reader.GetInt32(3)
                  Latitude = reader.GetDouble(4)
                  Longitude = reader.GetDouble(5)
                  MinCloudCoverFilter = reader.GetDouble(6)
                  MaxCloudCoverFilter = reader.GetDouble(7)
                  NotificationOffset = reader.GetDateTime(8) - DateTime.MinValue }
                
            parsedTargetsList <- target :: parsedTargetsList
        with
        | ex ->
            logger.LogError("Failed to parse a target from a returned sql data.")
            
    parsedTargetsList


type DbUserTargetService(
    logger: ILogger<DbUserTargetService>,
    dbOptions: IOptions<DbOptions>) =
    
    member this.TryGetTargets(userEmail: string, ?requestId: Guid) =
        try
            use connection = new MySqlConnection(dbOptions.Value.DbConnectionString)
            connection.Open()
            
            let queryStringRaw = "
SELECT u.UserGuid, t.TargetGuid, t.ScenePath, t.SceneRow, t.Latitude, t.Longitude, t.MinCloudCover, t.MaxCloudCover, t.NotificationOffset
FROM Targets as t
INNER JOIN UsersTargets as ut ON t.TargetGuid = ut.TargetGuid 
INNER JOIN Users as u ON u.UserGuid = ut.UserGuid
WHERE u.Email = @Email"

            use queryCommand = new MySqlCommand(queryStringRaw, connection)
            queryCommand.Parameters.AddWithValue("@email", userEmail) |> ignore

            let queryStringToLog = queryStringRaw.Replace("@email", $"\"{userEmail}\"")
            logger.LogInformation($"[{requestId}] {queryStringToLog}")
            
            
            use reader = queryCommand.ExecuteReader()
            parseTargetsFromDataReader reader logger
            |> Ok
        with
        | ex ->
            Error ex.Message
    
    
    member this.TryAddTarget(target: Target, requestId: Guid) =
        try
            use connection = new MySqlConnection(dbOptions.Value.DbConnectionString)
            connection.Open()
            
            let log str = logger.LogInformation($"[{requestId}] {str}")
            
            tryCreateTarget log connection target
            |> Result.bind (tryCreateJoinEntry log connection)
        with
        | ex ->
            Error ex.Message