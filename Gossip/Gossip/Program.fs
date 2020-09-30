//Imports
open Akka
open Akka.FSharp
open System
open System.Diagnostics
open Akka.Actor

//Create System reference
let system = System.create "system" <| Configuration.defaultConfig()

type PushSumMessage() = 
    [<DefaultValue>] val mutable s: int
    [<DefaultValue>] val mutable w: int

type GossipMessage() = 
    [<DefaultValue>] val mutable rumor: int

let mutable algo = ""
let mutable actorNumber = 20
let arrayActor : IActorRef array = Array.zeroCreate actorNumber

let getNeighbour currentNum = 
    if algo = "full" then
        let objrandom = new Random()
        let ran = objrandom.Next(0,actorNumber)
        ran
    else 
       0

//Actor
let gossipActor (actorMailbox:Actor<GossipMessage>) = 
    let mutable flag = false
    let mutable count = 0

    //Actor Loop that will process a message on each iteration
    let rec actorLoop() = actor {

        //Receive the message
        let! msg = actorMailbox.Receive()
        printfn "%A" msg.rumor
        count <- count + 1
        flag <- true
        if flag && count < 10 then
            let next = getNeighbour msg.rumor
            let sendMsg = new GossipMessage()
            sendMsg.rumor <- next
            arrayActor.[next] <! sendMsg
            

        return! actorLoop()
    }

    //Call to start the actor loop
    actorLoop()






let gossipFull = 
    algo <- "full"
    for i = 0 to actorNumber-1 do
        let name:string = "actor" + i.ToString() 
        arrayActor.[i] <- spawn system name gossipActor 

    let objrandom = new Random()
    let ran = objrandom.Next(0,actorNumber)

    let sendMsg = new GossipMessage()
    sendMsg.rumor <- ran
    arrayActor.[ran] <! sendMsg



[<EntryPoint>]
let main(args) =


    gossipFull

    //Keep the console open by making it wait for key press
    System.Console.ReadKey() |> ignore

    0 // return an integer exit code
