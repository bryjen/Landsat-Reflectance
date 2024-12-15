module FsLandsatApi.Services.DbUserService

open System
open FsLandsatApi.Options
open Microsoft.AspNetCore.Identity
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open System.Data
open Microsoft.Net.Http.Headers
open MySql.Data.MySqlClient
open FsLandsatApi.Models.User

// TODO: Maybe find a better, 'more secure' form of hashing them passwords
[<AutoOpen>]
module private Hashing = 
    let hashPassword (email: string) (password: string) =
        let passwordHasher = PasswordHasher<string>()
        passwordHasher.HashPassword(email, password)
        
    let isHashValid (email: string) (password: string) (hashedPassword: string) =
        let passwordHasher = PasswordHasher<string>()
        match passwordHasher.VerifyHashedPassword(email, hashedPassword, password) with
        | PasswordVerificationResult.Success | PasswordVerificationResult.SuccessRehashNeeded -> true
        | _ -> false
    


// assumes all fields of the user is fetched (SELECT * FROM ...)
let private parseUsersFromDataReader (reader: MySqlDataReader) (logger: ILogger) =
    let mutable parsedUsersList: User list = []
    while reader.Read() do
        try
            let user =
                { Id = reader.GetGuid(0)
                  Email = reader.GetString(1)
                  PasswordHash = reader.GetString(2)
                  IsEmailEnabled = reader.GetBoolean(3) }
                
            parsedUsersList <- user :: parsedUsersList
        with
        | ex ->
            logger.LogError("Failed to parse a user from a returned sql data.")
            
    parsedUsersList
    
let rec private filterUsers (email: string) (password: string) (users: User list) =
    match users with
    | user :: rest ->
        match isHashValid email password user.PasswordHash with
        | true -> Some user
        | false -> filterUsers email password rest
    | [] ->
        None
    


type DbUserService(
    logger: ILogger<DbUserService>,
    dbOptions: IOptions<DbOptions>) =
    
    member this.TryGetUserByCredentials(email: string, password: string) =
        try
            use connection = new MySqlConnection(dbOptions.Value.DbConnectionString)
            connection.Open()
            
            let queryStringRaw = "SELECT * FROM Users as u WHERE u.Email = @email"
            
            use queryCommand = new MySqlCommand(queryStringRaw, connection)
            queryCommand.Parameters.AddWithValue("@email", email) |> ignore
            
            let queryStringToLog = queryStringRaw.Replace("@email", $"\"{email}\"")
            logger.LogInformation(queryStringToLog)
            
            use reader = queryCommand.ExecuteReader()
            parseUsersFromDataReader reader logger
            |> filterUsers email password
            |> function
                | Some user -> Ok user
                | None -> Error $"Could not find user with email \"{email}\" and password \"{password}\""
        with
        | ex ->
            Error ex.Message
    
    
    member this.TryCreateUser(email: string, password: string, isEmailEnabled: bool) : Result<User, string> =
        let newUser =
            { Id = Guid.NewGuid()
              Email = email
              PasswordHash = hashPassword email password
              IsEmailEnabled = isEmailEnabled }
        
        try
            use connection = new MySqlConnection(dbOptions.Value.DbConnectionString)
            connection.Open()
            
            let queryStringRaw = "INSERT INTO Users (UserGuid, Email, PasswordHash, EmailEnabled) VALUES (@userGuid, @email, @passwordHash, @emailEnabled)"
            
            use queryCommand = new MySqlCommand(queryStringRaw, connection)
            queryCommand.Parameters.AddWithValue("@userGuid", newUser.Id) |> ignore
            queryCommand.Parameters.AddWithValue("@email", newUser.Email) |> ignore
            queryCommand.Parameters.AddWithValue("@passwordHash", newUser.PasswordHash) |> ignore
            queryCommand.Parameters.AddWithValue("@emailEnabled", newUser.IsEmailEnabled) |> ignore
            
            let queryStringToLog = queryStringRaw
                                       .Replace("@userGuid", $"\"{newUser.Id}\"")
                                       .Replace("@email", $"\"{newUser.Email}\"")
                                       .Replace("@passwordHash", $"\"{newUser.PasswordHash}\"")
                                       .Replace("@emailEnabled", if newUser.IsEmailEnabled then "1" else "0")
            logger.LogInformation(queryStringToLog)
            
            let _ = queryCommand.ExecuteNonQuery()  // in case of a duplicate, will throw error instead
            Ok newUser
        with
        | ex ->
            Error ex.Message
        
    member this.TryDeleteUser() =
        failwith "todo"
        
    member this.TryEditUser() =
        failwith "todo"
