﻿module FsLandsatApi.Models.User

open System

type User =
    { Id: Guid
      Email: string
      PasswordHash: string
      IsEmailEnabled: bool }
with
    override this.ToString() =
        $"[{this.Id}] {this.Email}"
        
type Target =
    { UserId: Guid  // Id/Guid of the user this target is attached to
      Id: Guid
      Path: int
      Row: int
      Latitude: double
      Longitude: double
      MinCloudCoverFilter: double
      MaxCloudCoverFilter: double
      NotificationOffset: TimeSpan }