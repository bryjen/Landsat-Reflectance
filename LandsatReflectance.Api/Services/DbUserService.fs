﻿module LandsatReflectance.Api.Services.DbUserService

open System
open System.Threading.Tasks

open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options

open MySql.Data.MySqlClient

open LandsatReflectance.Api.Options
open LandsatReflectance.Api.Models.User
open LandsatReflectance.Api.Utils.PasswordHashing
open Polly



// assumes all fields of the user is fetched (SELECT * FROM ...)
let private parseUsersFromDataReader (reader: MySqlDataReader) (logger: ILogger) =
    let mutable parsedUsersList: User list = []
    while reader.Read() do
        try
            let user =
                { Id = reader.GetGuid(0)
                  FirstName = reader.GetString(1)
                  LastName = reader.GetString(2)
                  Email = reader.GetString(3)
                  PasswordHash = reader.GetString(4)
                  IsEmailEnabled = reader.GetBoolean(5)
                  IsAdmin = reader.GetBoolean(6) }
                
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
    
    let retryPolicy = 
        let policyBuilder = Policy.Handle<MySqlException>()
        let retryPolicy = policyBuilder.WaitAndRetry(
            [
                TimeSpan.FromSeconds(1.0)
                TimeSpan.FromSeconds(3.0)
                TimeSpan.FromSeconds(5.0)
            ])
        retryPolicy
    
    let connectionStringBuilder =
        let csb = MySqlConnectionStringBuilder(dbOptions.Value.DbConnectionString)
        csb.Pooling <- true
        csb.MinimumPoolSize <- uint 0
        csb.MaximumPoolSize <- uint 10
        
        
        csb.ConnectionTimeout <- uint 10
        
        csb
    
    
    member this.TryGetJwtGuid(userEmail: string) : Result<Guid option, string> =
        try
            use connection = new MySqlConnection(connectionStringBuilder.ToString())
            connection.Open()
            
            let queryStringRaw = "SELECT RefreshGuid FROM Users WHERE Email = @Email"
            
            use queryCommand = new MySqlCommand(queryStringRaw, connection)
            queryCommand.Parameters.AddWithValue("@Email", userEmail) |> ignore
            
            let queryStringToLog = queryStringRaw.Replace("@Email", $"'{userEmail}'")
            logger.LogInformation(queryStringToLog)
            
            use reader = queryCommand.ExecuteReader()
            
            match reader.Read() with
            | true ->
                match Guid.TryParse(reader.GetString(0)) with
                | true, guid -> Ok (Some guid)
                | false, _ -> Ok None 
            | false ->
                Ok None
        with
        | ex ->
            Error ex.Message
        
        
    member this.GenerateNewRefreshGuid(userEmail: string) : Result<Guid, string> =
        try
            use connection = new MySqlConnection(connectionStringBuilder.ToString())
            connection.Open()
            
            let queryStringRaw = "UPDATE Users SET RefreshGuid = @RefreshGuid WHERE Email = @Email"
            
            let refreshGuid = Guid.NewGuid()
            use command = new MySqlCommand(queryStringRaw, connection)
            command.Parameters.AddWithValue("@RefreshGuid", refreshGuid) |> ignore
            command.Parameters.AddWithValue("@Email", userEmail) |> ignore
            
            let queryStringToLog =
                queryStringRaw
                    .Replace("@RefreshGuid", $"'{refreshGuid}'")
                    .Replace("@Email", $"'{userEmail}'")
            logger.LogInformation(queryStringToLog)
            
            let rowsAffected = command.ExecuteNonQuery()
            match rowsAffected with
            | i when i >= 0 ->
                Ok refreshGuid
            | _ ->
                Error "No data changed."
        with
        | ex ->
            Error ex.Message
    
    
    // Matches without password, only use in handlers/endpoints where the user is already authenticated.
    member internal this.TryGetUserByEmail(email: string) = 
        try
            use connection = new MySqlConnection(connectionStringBuilder.ToString())
            connection.Open()
            
            let queryStringRaw = "SELECT * FROM Users as u WHERE u.Email = @email"
            
            use queryCommand = new MySqlCommand(queryStringRaw, connection)
            queryCommand.Parameters.AddWithValue("@email", email) |> ignore
            
            let queryStringToLog = queryStringRaw.Replace("@email", $"\"{email}\"")
            logger.LogInformation(queryStringToLog)
            
            use reader = queryCommand.ExecuteReader()
            parseUsersFromDataReader reader logger
            |> function
                | user :: _ -> Ok user
                | [] -> Error $"Could not find user with email \"{email}\""
        with
        | ex ->
            Error ex.Message
    
    
    member this.TryGetUserByCredentials(email: string, password: string) =
        try
            use connection = new MySqlConnection(connectionStringBuilder.ToString())
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
    
    
    member this.TryCreateUser(firstName: string,
                              lastName: string,
                              email: string,
                              password: string,
                              isEmailEnabled: bool) : Result<User, string> =
        let newUser =
            { Id = Guid.NewGuid()
              FirstName = firstName
              LastName = lastName
              Email = email
              PasswordHash = hashPassword email password
              IsEmailEnabled = isEmailEnabled
              IsAdmin = false }
        
        try
            use connection = new MySqlConnection(connectionStringBuilder.ToString())
            connection.Open()
            
            let queryStringRaw = "INSERT INTO Users (UserGuid, FirstName, LastName, Email, PasswordHash, EmailEnabled, IsAdmin, RefreshGuid) VALUES (@userGuid, @firstName, @lastName, @email, @passwordHash, @isEmailEnabled, @isAdmin, @refreshGuid)"
            
            let refreshGuid = Guid.NewGuid()
            use queryCommand = new MySqlCommand(queryStringRaw, connection)
            queryCommand.Parameters.AddWithValue("@userGuid", newUser.Id) |> ignore
            queryCommand.Parameters.AddWithValue("@firstName", newUser.FirstName) |> ignore
            queryCommand.Parameters.AddWithValue("@lastName", newUser.LastName) |> ignore
            queryCommand.Parameters.AddWithValue("@email", newUser.Email) |> ignore
            queryCommand.Parameters.AddWithValue("@passwordHash", newUser.PasswordHash) |> ignore
            queryCommand.Parameters.AddWithValue("@isEmailEnabled", newUser.IsEmailEnabled) |> ignore
            queryCommand.Parameters.AddWithValue("@isAdmin", newUser.IsAdmin) |> ignore
            queryCommand.Parameters.AddWithValue("@refreshGuid", refreshGuid) |> ignore
            
            let queryStringToLog = queryStringRaw
                                       .Replace("@userGuid", $"'{newUser.Id}'")
                                       .Replace("@firstName", $"'{newUser.FirstName}'")
                                       .Replace("@lastName", $"'{newUser.LastName};")
                                       .Replace("@email", $"'{newUser.Email}'")
                                       .Replace("@passwordHash", $"'{newUser.PasswordHash}'")
                                       .Replace("@emailEnabled", if newUser.IsEmailEnabled then "1" else "0")
                                       .Replace("@isAdmin", if newUser.IsAdmin then "1" else "0")
                                       .Replace("@refreshGuid", $"'{refreshGuid.ToString()}'")
            logger.LogInformation(queryStringToLog)
            
            let _ = queryCommand.ExecuteNonQuery()  // in case of a duplicate, will throw error instead
            Ok newUser
        with
        | ex ->
            Error ex.Message
        
        
    member this.TryDeleteUser(email: string) =
        try
            use connection = new MySqlConnection(connectionStringBuilder.ToString())
            connection.Open()
            
            let queryStringRaw = "DELETE FROM Users WHERE Email = @Email"
            
            use queryCommand = new MySqlCommand(queryStringRaw, connection)
            queryCommand.Parameters.AddWithValue("@Email", email) |> ignore
            
            let queryStringToLog = queryStringRaw.Replace("@Email", $"\"{email}\"")
            logger.LogInformation(queryStringToLog)
            
            match queryCommand.ExecuteNonQuery() with
            | i when i = 1 -> Ok ()
            | _ -> Error $"Could not find a user with email \"{email}\""
        with
        | ex ->
            Error ex.Message
        
    /// <remarks>
    /// A change in the user's ID will be ignored. The function will not error
    /// </remarks>
    member this.TryEditUser(transformUser: User -> User, userEmailToEdit: string) : Task<Result<User, string>> =
        
        // Helper method
        let editUserInfoInDb user =
            try
                use connection = new MySqlConnection(connectionStringBuilder.ToString())
                connection.Open()
                
                let queryStringRaw =
                    "UPDATE Users
                     SET FirstName = @FirstName, LastName = @LastName, Email = @Email, PasswordHash = @PasswordHash, EmailEnabled = @EmailEnabled
                     WHERE Email = @FilterEmail"
                
                use queryCommand = new MySqlCommand(queryStringRaw, connection)
                queryCommand.Parameters.AddWithValue("@FirstName", user.FirstName) |> ignore
                queryCommand.Parameters.AddWithValue("@LastName", user.LastName) |> ignore
                queryCommand.Parameters.AddWithValue("@Email", user.Email) |> ignore
                queryCommand.Parameters.AddWithValue("@PasswordHash", user.PasswordHash) |> ignore
                queryCommand.Parameters.AddWithValue("@EmailEnabled", user.IsEmailEnabled) |> ignore
                queryCommand.Parameters.AddWithValue("@FilterEmail", userEmailToEdit) |> ignore
                
                (*
                let queryStringToLog = queryStringRaw.Replace("@Email", $"\"{email}\"")
                logger.LogInformation(queryStringToLog)
                *)
                
                match queryCommand.ExecuteNonQuery() with
                | i when i = 1 -> Ok user
                | _ -> Error $"Could not find a user with email \"{userEmailToEdit}\""
            with
            | ex ->
                Error ex.Message
        
        
        // Main thingy
        task {
            return
                this.TryGetUserByEmail(userEmailToEdit)
                |> Result.map transformUser
                |> Result.bind editUserInfoInDb
        }
