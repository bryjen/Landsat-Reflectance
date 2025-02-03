module FsLandsatApi.Models.PredictionsState

open System
open System.IO
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging


type public NormalDistributionParameters =
    { Landsat8Mean: TimeSpan
      Landsat8StdDev: TimeSpan
      Landsat9Mean: TimeSpan
      Landsat9StdDev: TimeSpan }

type public PredictionStateEntry =
    { Path: int
      Row: int
      PredictedSatellite: int option
      PredictedDateUtc: DateTime option
      NormalDistributionParameters: NormalDistributionParameters option }
    
    
    
[<AutoOpen>]
module private PredictionsStateHelpers =
    
    module Csv =
        let parseCsvLine (line: string) =
            let tokens = line.Split(',') |> Seq.map _.Trim() |> Seq.toArray
            let pathStr = Array.tryItem 0 tokens
            let rowStr = Array.tryItem 1 tokens
            failwith "todo"
            


type public PredictionsState() =
    
    static member Init(serviceProvider: IServiceProvider) =
        let logger = serviceProvider.GetRequiredService<ILogger<PredictionsState>>()
        
        let bootstrapFilePath = "./Data/bootstrapPathRowData.csv"
        let predictionDataFilePath = "./Data/predictionData.csv"
        
        match File.Exists(bootstrapFilePath), File.Exists(predictionDataFilePath) with
        | true, true ->
            logger.LogInformation($"Found path/row data file \"{predictionDataFilePath}\".")
            // load prediction data file 
            failwith "todo"
        | true, false ->
            logger.LogInformation($"Found bootstrap file \"{bootstrapFilePath}\", path/row data file missing at \"{predictionDataFilePath}\".")
            logger.LogInformation("Initializing path/row data file.")
            // init data file from bootstrap
            failwith "todo"
        | false, true ->
            logger.LogInformation($"Found path/row data file \"{predictionDataFilePath}\".")
            logger.LogWarning($"Could not find the bootstrap file at \"{bootstrapFilePath}\".")
            // log warning
            // load prediction data file 
            failwith "todo"
        | false, false ->
            // TODO: Add a way to fix this; link back somewhere to the repo
            failwith $"Missing path/row data file, and no bootstrap file was found at \"{bootstrapFilePath}\"."
        ()
