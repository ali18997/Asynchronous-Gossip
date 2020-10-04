//Imports
open Akka
open Akka.FSharp
open System
open System.Diagnostics
open Akka.Actor


//Create System reference
let system = System.create "system" <| Configuration.defaultConfig()

type Message() = 
    [<DefaultValue>] val mutable num: int
    [<DefaultValue>] val mutable s: BigRational
    [<DefaultValue>] val mutable w: BigRational

//CONTROL VARIABLES

//CHANGE FROM ARGS
let mutable topology = ""
let mutable algorithm = ""
let mutable numNodes = 0

//CHANGE HERE DIRECTLY
let printFlag = true
let thresholdGossip = 10
let thresholdPushSum = BigRational.FromInt(1)/BigRational.FromBigInt(bigint 10**10) // 10 ^ -10


let mutable arrayActor : IActorRef array = null


let timer = new Stopwatch()

let stopTime num = 
    let realTime = timer.ElapsedMilliseconds
    //printfn "ACTOR %A WILL NO LONGER SEND" num   
    printfn "TIME: %dms" realTime

let perfectSquare n =
    let h = n &&& 0xF
    if (h > 9) then false
    else
        if ( h <> 2 && h <> 3 && h <> 5 && h <> 6 && h <> 7 && h <> 8 ) then
            let t = ((n |> double |> sqrt) + 0.5) |> floor|> int
            t*t = n
        else false

let neighbour2D currentNum side ran = 
    if ran = 0 && (currentNum % side) <> 0 then
        currentNum - 1
    elif ran = 0 && (currentNum % side) = 0 then
        currentNum + 1
    elif ran = 1 && ((currentNum + 1) % side) <> 0 then
        currentNum + 1
    elif ran = 1 && ((currentNum + 1) % side) = 0 then
        currentNum - 1
    elif ran = 2 && currentNum + side < side*side then
        currentNum + side
    elif ran = 2 && currentNum + side >= side*side then
        currentNum - side
    elif ran = 3 && currentNum - side >= 0 then
        currentNum - side
    elif ran = 3 && currentNum - side < 0 then
        currentNum + side
    else
        0

let sendMessage num s w = 
    let sendMsg = new Message()
    sendMsg.num <- num
    sendMsg.s <- s
    sendMsg.w <- w
    arrayActor.[int num] <! sendMsg

let killActor num = 
    arrayActor.[int num] <! PoisonPill.Instance


let getNeighbour currentNum = 
    let objrandom = new Random()
    let side = (int (sqrt (float numNodes)))
    if topology = "full" then
        let ran = objrandom.Next(0,numNodes)
        ran
     
    elif topology = "2D" then
        let ran = objrandom.Next(0,4)
        neighbour2D currentNum side ran
    
    elif topology = "imp2D" then
        let ran = objrandom.Next(0,5)
        if ran = 4 then
            objrandom.Next(0,numNodes)
        else
            neighbour2D currentNum side ran

    elif topology = "line" then
        if currentNum = 0 then
            1
        elif currentNum = numNodes-1 then
            numNodes-2
        else
            let ran = objrandom.Next(0,2)
            if ran = 0 then
                currentNum + 1
            else
                currentNum - 1
    else
       0

let getNeighbourUnique num = 
    let mutable next = getNeighbour num
    while next = num do
        next <- getNeighbour num
    next

//Actor
let actor (actorMailbox:Actor<Message>) = 
    let mutable count = 0
    let mutable s = BigRational.FromInt(-1)
    let mutable w = BigRational.FromInt(1)
    let mutable ratio1 = BigRational.FromInt(0)
    let mutable ratio2 = BigRational.FromInt(0)
    let mutable ratio3 = BigRational.FromInt(0)

    //GOSSIP ALGORITHM
    let gossip num next =
        if count < thresholdGossip then
            sendMessage next (BigRational.FromInt(0)) (BigRational.FromInt(0))
        else
            stopTime num
            killActor num

    //PUSH-SUM ALGORITHM
    let pushSum next num ms mw =
        if s = BigRational.FromInt(-1) then
            s <- BigRational.FromInt(num)

        s <- s + ms
        w <- w + mw
   
        ratio1 <- ratio2
        ratio2 <- ratio3
        ratio3 <- s/w

        if abs(ratio3 - ratio1) < thresholdPushSum && count > 3 then
            stopTime num
            killActor num

        else 
            sendMessage next (s/ BigRational.FromInt(2)) (w/ BigRational.FromInt(2))

    //RUN ALGORITHM
    let runAlgo msg =
        let m:Message = msg

        //printfn "ACTOR %A RECEIVED MSG" msg.num

        count <- count + 1
        
        let next = getNeighbour msg.num

        if algorithm = "gossip" then
            gossip msg.num next 

        elif algorithm = "push-sum" then  
            pushSum next msg.num msg.s msg.w


    //Actor Loop that will process a message on each iteration
    let rec actorLoop() = actor {

        //Receive the message
        let! msg = actorMailbox.Receive()
        
        runAlgo msg
            
        return! actorLoop()
    }

    //Call to start the actor loop
    actorLoop()


let makeActors start =

    if topology = "2D" || topology = "imp2D" then
        while perfectSquare numNodes = false do
            numNodes <- numNodes + 1

    arrayActor <- Array.zeroCreate numNodes

    for i = 0 to numNodes-1 do
        let name:string = "actor" + i.ToString() 
        arrayActor.[i] <- spawn system name actor 


[<EntryPoint>]
let main(args) =

    numNodes <- args.[0] |> int

    topology <- args.[1] |> string

    algorithm <- args.[2] |> string

    makeActors true
    
    timer.Start()

    sendMessage 0 (BigRational.FromInt(0)) (BigRational.FromInt(0))

    //Keep the console open by making it wait for key press
    System.Console.ReadKey() |> ignore

    0 // return an integer exit code
