module FsLandsatApi.Services.Hosted.PredictionCheckingService

open System
open System.Globalization
open System.IO
open System.Threading
open System.Threading.Tasks

open CsvHelper
open LandsatReflectance.Api.Services.UsgsTokenService
open LandsatReflectance.Api.Utils
open LandsatReflectance.Api.Utils.UsgsHttpClient
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

open FsToolkit.ErrorHandling

open LandsatReflectance.Api.Services.PredictionService



[<AutoOpen>]
module private PredictionCheckingServiceHelpers =
    let rec filterPredictionsState (predictionStateEntries: Map<int * int, PredictionStateEntry>) =
        let toUpdate = predictionStateEntries |> Map.filter (fun _ -> predictionsStateFilter)
        let rest = predictionStateEntries |> Map.filter (fun _ ->  (predictionsStateFilter >> not))
        toUpdate, rest
        
    and private predictionsStateFilter (predictionStateEntry: PredictionStateEntry) =
        option {
            let! _, stdDev =
                match predictionStateEntry.PredictedSatellite with
                | 8 -> Some (predictionStateEntry.Landsat8Mean, predictionStateEntry.Landsat8StdDev)
                | 9 -> Some (predictionStateEntry.Landsat9Mean, predictionStateEntry.Landsat9StdDev)
                | _ -> None
                
            let dateTime = predictionStateEntry.PredictedDateTimeUtc - (0.5 * stdDev)
            return dateTime.ToUniversalTime() < DateTime.UtcNow
        }
        |> function
            | Some value -> value
            | None -> false
            
            
    let logFilteredPredictionStateEntries (logger: ILogger) (filteredPredictionStateEntries: Map<int * int, PredictionStateEntry>) =
        logger.LogInformation($"Found {filteredPredictionStateEntries.Count} entries to update.")
            
            
    let rec getUpdatedPredictions (logger: ILogger) (usgsTokenService: UsgsTokenService) (usgsHttpClient: UsgsHttpClient) (toUpdate: Map<int * int, PredictionStateEntry>) =
        task {
            let mutable psesMap: Map<int * int, Result<PredictionStateEntry, PredictionStateEntry>> = Map.ofArray [||]
            for kv in toUpdate do
                let pathRow, pse = kv.Key, kv.Value
                let! result = getNewPredictionStateEntry logger usgsTokenService usgsHttpClient pathRow pse
                psesMap <- psesMap.Add(pathRow, result)
                ()
            
            (* // this executes everything all at the same time, will get you rate limited
            let psesMap = toUpdate |> Map.map (getNewPredictionStateEntry logger predictionService)
            let! _ = Task.WhenAll psesMap.Values  // ignore values, since it would be better to change them in the map.
            let psesMap = psesMap |> Map.map (fun _ task -> task.Result)
            *)
            
            let successfullyUpdated =
                psesMap
                |> Map.filter (fun _ result -> result.IsOk)
                |> Map.map (fun _ result -> match result with | Ok pse -> pse | Error _ -> failwith "FATAL: Failed to access OK value")
            let failedToUpdate =
                psesMap
                |> Map.filter (fun _ result -> result.IsError)
                |> Map.map (fun _ result -> match result with | Ok _ -> failwith "FATAL: Failed to access ERROR value" | Error pse -> pse)
            
            return successfullyUpdated, failedToUpdate
        }
        
    and private getNewPredictionStateEntry
        (logger: ILogger)
        (usgsTokenService: UsgsTokenService)
        (usgsHttpClient: UsgsHttpClient)
        (pathRow: int * int)
        (previousPredictionStateEntry: PredictionStateEntry)
        : TaskResult<PredictionStateEntry, PredictionStateEntry> =
            
        task {
            let path, row = pathRow
            let! results = PredictionService.GetPredictionAndNormalDistributionParameters(usgsTokenService, usgsHttpClient, path, row, 10)
            
            // print formatted error message, if applicable
            // solely for side effects
            match results with
            | Error errorMsg -> logger.LogWarning($"An error occurred while trying to update path/row {path}/{row} with message \"{errorMsg}\"")
            | _ -> () 
            
            return
                results
                |> Result.map (fun (prediction, normalDistributionParameters) -> toPredictionStateEntry path row prediction normalDistributionParameters)
                |> Result.mapError (fun _ -> previousPredictionStateEntry)
        }
        
    and private toPredictionStateEntry path row (prediction: Prediction) (normalDistributionParameters: NormalDistributionParameters) =
        { Path = path
          Row = row
          PredictedSatellite = prediction.PredictedSatellite
          PredictedDateTimeUtc = prediction.PredictedTimeUtc
          Landsat8Mean = normalDistributionParameters.Landsat8Mean
          Landsat8StdDev = normalDistributionParameters.Landsat8StdDev
          Landsat9Mean = normalDistributionParameters.Landsat9Mean
          Landsat9StdDev = normalDistributionParameters.Landsat9StdDev }
        
        
        
    let logUpdatedResults (logger: ILogger) (updated: Map<int * int, PredictionStateEntry>) (failedToUpdate: Map<int * int, PredictionStateEntry>) =
        logger.LogInformation($"Successfully updated {updated.Count} entries.")
        logger.LogError($"Failed to update {failedToUpdate.Count} entries.")
        


type PredictionCheckingService(serviceScopeFactory: IServiceScopeFactory, logger: ILogger<PredictionCheckingService>) =
    inherit BackgroundService()
    
    let checkInterval: TimeSpan = TimeSpan.FromSeconds(30.0)
    
    let scope = serviceScopeFactory.CreateScope()
    let predictionsState = scope.ServiceProvider.GetRequiredService<PredictionsState>()
    // let predictionService = scope.ServiceProvider.GetRequiredService<PredictionService>()
    let usgsTokenService = scope.ServiceProvider.GetRequiredService<UsgsTokenService>()
    let usgsHttpClient = scope.ServiceProvider.GetRequiredService<UsgsHttpClient>()
    
    override this.ExecuteAsync (stoppingToken: CancellationToken): Task =
        task {
            while not predictionsState.IsInitialized do
                do! Task.Delay(TimeSpan.FromSeconds(10.0))
            
            logger.LogInformation("Started checking for predictions")
            
            // Introduce some delay before starting to update predictions
            // Usually allows the main server to start without problems
            do! Task.Delay(TimeSpan.FromSeconds(10.0))
            
            while (not stoppingToken.IsCancellationRequested) do
                let toUpdate, rest = filterPredictionsState predictionsState.PredictionStateEntries
                logFilteredPredictionStateEntries logger toUpdate
                
                let! updated, failedToUpdate = getUpdatedPredictions logger usgsTokenService usgsHttpClient toUpdate
                logUpdatedResults logger updated failedToUpdate
                
                let something = Map.fold (fun acc key value -> Map.add key value acc)
                let newEntries = something updated failedToUpdate
                let newPredictionStateEntries = something newEntries rest
                
                predictionsState.PredictionStateEntries <- newPredictionStateEntries
                
                let values = Seq.toArray newPredictionStateEntries.Values
                use writer = new StreamWriter(predictionDataFilePath)
                use csv = new CsvWriter(writer, CultureInfo.InvariantCulture)
                csv.WriteRecords(values)
                logger.LogInformation($"[{DateTime.Now}]\tSaved {values.Length} entries.")
                
                do! Task.Delay(checkInterval)
                
            return ()
        }
