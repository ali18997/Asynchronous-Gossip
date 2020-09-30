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

let mutable topology = ""
let mutable actorNumber = 20
let arrayActor : IActorRef array = Array.zeroCreate actorNumber

let sendGossip num = 
    let sendMsg = new GossipMessage()
    sendMsg.rumor <- num
    arrayActor.[num] <! sendMsg


let getNeighbour currentNum = 
    if topology = "full" then
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
            sendGossip next
            

        return! actorLoop()
    }

    //Call to start the actor loop
    actorLoop()


let makeActors =     
    for i = 0 to actorNumber-1 do
        let name:string = "actor" + i.ToString() 
        arrayActor.[i] <- spawn system name gossipActor 





[<EntryPoint>]
let main(args) =
    topology <- "full"

    makeActors

    sendGossip 0

    //Keep the console open by making it wait for key press
    System.Console.ReadKey() |> ignore

    0 // return an integer exit code
