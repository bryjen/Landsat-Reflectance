namespace LandsatReflectance.Api

open System.Runtime.CompilerServices

// Testing type to validate internals are visible to test project.
type internal TestInternalType =
    { Property: string }

[<assembly: InternalsVisibleTo("LandsatReflectance.Api.Tests")>]
do ()