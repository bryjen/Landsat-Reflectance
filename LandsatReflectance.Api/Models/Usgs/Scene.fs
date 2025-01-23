module LandsatReflectance.Api.Models.Usgs.Scene

open System
open System.Text.Json


/// Represents some scene data returned by the 'scene-search' endpoint. Simplified, and does not contain all returned information.
type SceneData =
    { BrowseInfos: BrowseInfo array
      EntityId: string
      DisplayId: string
      Metadata: Metadata array
      CloudCoverInt: int
      PublishDate: DateTimeOffset }
    
and BrowseInfo =
    { BrowseName: string
      BrowsePath: string
      OverlayPath: string
      ThumbnailPath: string }
    
and Metadata =
    { Id: string
      FieldName: string
      DictionaryLink: string
      Value: JsonElement }
    

[<CLIMutable>]
type SimplifiedSceneData =
    { BrowseName: string option
      BrowsePath: string option
      OverlayPath: string option
      ThumbnailPath: string option
      Metadata: SimplifiedMetadata }
    
and SimplifiedMetadata =
    { EntityId: string
      DisplayId: string
      PublishDate: DateTimeOffset
      L1ProductId: string option
      L2ProductId: string option
      L1CloudCover: float option  // values returned from usgs api are to two decimal points, no need to worry abt precision
      CloudCoverInt: int option  // api provides a separate cloud cover value as an int. This is used to filter cloud cover values
      Satellite: int option  }
