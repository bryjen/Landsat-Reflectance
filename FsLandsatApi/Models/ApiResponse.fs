module FsLandsatApi.Models.ApiResponse

open System

type ApiResponse<'a> =
    { RequestGuid: Guid
      ErrorMessage: string option
      Data: 'a option }