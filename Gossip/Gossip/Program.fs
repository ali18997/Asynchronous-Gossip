//Imports
open Akka
open Akka.FSharp
open System
open System.Diagnostics
open Akka.Actor

//Create System reference
let system = System.create "system" <| Configuration.defaultConfig()

type Message() = 
    [<DefaultValue>] val mutable s: int
    [<DefaultValue>] val mutable w: int


//Actor
let actor (actorMailbox:Actor<Message>) = 

    //Actor Loop that will process a message on each iteration
    let rec actorLoop() = actor {

        //Receive the message
        let! msg = actorMailbox.Receive()
        printf "%A" msg.s
        printf "%A" msg.w

        return! actorLoop()
    }

    //Call to start the actor loop
    actorLoop()


[<EntryPoint>]
let main(args) =

    //Create the starting message
    let startMessage = new Message()

    //Add values to the message
    startMessage.s <- 0
    startMessage.w <- 0

    //Create the start actor
    let startActor = spawn system "start" actor
   
    //And pass it the start message
    startActor <! startMessage

    //Keep the console open by making it wait for key press
    System.Console.ReadKey() |> ignore

    0 // return an integer exit code
