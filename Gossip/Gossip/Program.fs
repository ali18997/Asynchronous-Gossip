//Imports
open Akka
open Akka.FSharp
open System
open System.Diagnostics
open Akka.Actor

type ChildMessage() = 

    //starting number of subproblem
    [<DefaultValue>] val mutable start: int

    //length of subproblem
    [<DefaultValue>] val mutable length: int

[<EntryPoint>]
let main argv =
    printfn "Hello World from F#!"
    0 // return an integer exit code
