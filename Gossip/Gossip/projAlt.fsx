#I @"packages"
// #r "nuget: Akka.FSharp" 
// #r "nuget: Akka" 
#r "Akka.FSharp.dll"
#r "Akka.dll"
// #r "System.Configuration.ConfigurationManager.dll"
#r "Newtonsoft.Json.dll"
#r "FsPickler.dll"
#r "FSharp.Core.dll"
#r "Extreme.Numerics.dll"
#r "Extreme.Numerics.FSharp.dll"
// #time "on"

open Akka
open Akka.FSharp
open Akka.Actor
open System
open System.Diagnostics

let system = System.create "system" <| Configuration.load ()

type ProcessorMessage = 
    | IntializeParent of int*string*string*IActorRef
    | IntializeChild of int*(int list)*IActorRef
    | StartGossip of bool
    | ReferenceList of IActorRef list
    | Ping of int
    | Done of int
    | StartPushSum of bool
    | Pong of int*double*double

let timer = new Stopwatch()

let stopTime num = 
    let realTime = timer.ElapsedMilliseconds
    printfn "TIME: %dms" realTime

let getRandArrElement =
  let rnd = Random()
  fun (arr : int list) -> arr.[rnd.Next(arr.Length)]

let getNeighbours topology i size= 
    if(topology="Full") then
        List.filter (fun elem -> elem<>i) [ 1 .. size]
    elif(topology="2D") then
        let mutable row = 0
        let rowf = Math.Sqrt(float(size))
        let rowi = int (Math.Sqrt(float(size)))
        if(float (rowi) = rowf) then 
            row <- rowi
        else 
            row <- rowi+1
        let x = (i-1)/row
        let y = (i-1)%row
        List.filter (fun elem -> 
                let x0 = (elem-1)/row
                let y0 = (elem-1)%row
                (x0 = x && y0 = y - 1) || (x0 = x && y0 = y + 1) || (x0 = x - 1 && y0 = y) || (x0 = x + 1 && y0 = y)) [ 1 .. size]
    elif(topology="Line") then
        List.filter (fun elem -> elem=i-1 || elem=i+1) [ 1 .. size]
    else
        let mutable row = 0
        let rowf = Math.Sqrt(float(size))
        let rowi = int (Math.Sqrt(float(size)))
        if(float (rowi) = rowf) then 
            row <- rowi
        else 
            row <- rowi+1
        //let row = int (Math.Sqrt(1.0+float(size))) + 1
        let x = (i-1)/row
        let y = (i-1)%row
        let mutable neighBourSet = Set.filter (fun elem -> 
                let x0 = (elem-1)/row
                let y0 = (elem-1)%row
                (x0 = x && y0 = y - 1) || (x0 = x && y0 = y + 1) || (x0 = x - 1 && y0 = y) || (x0 = x + 1 && y0 = y)) (Set.ofSeq [ 1 .. size])
        
        let s = List.filter (fun elem -> elem<>i && not (Set.contains elem neighBourSet)) [ 1 .. size]
        let num = getRandArrElement s
        Set.toList neighBourSet @ [num]




let mutable availableActors = Set.empty

let checkAllNeighborsExhausted (neighboursList: int list) = 
    let mutable count = 0
    let mutable randomEle = getRandArrElement neighboursList
    let mutable result = false
    while(not result) do
        if(not (Set.contains randomEle availableActors)) then
            count <- count + 1
        else 
            result <- true
        randomEle <- getRandArrElement neighboursList
        if(count=500) then result<-true
    result

    // while(not result) do

    
    // while(not (Set.contains randomEle availableActors)) do
    //     count <- count + 1
    //     randomEle <- getRandArrElement neighboursList
    //     if(count=50) then true
    // false    
    // for i=0 to 50 do
    //     if(not (Set.contains (neighboursList.Item(i)) availableActors)) then
    //         count <- count + 1
    //     else
    //         count <- count
    // count=neighboursList.Length

let child (mailbox: Actor<_>) = 
    let mutable childNeighbours = List.empty
    let mutable msgCount = 0
    let mutable childRefs = List.empty
    let mutable bossRef = null

    //push sum

    let mutable s:double = 0.0
    let mutable w:double = 0.0
    let mutable count = 0

    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | IntializeChild(childNo,neighbours,pRef) -> 
            childNeighbours <- childNeighbours @ neighbours
            //printfn "childno %i list %A" childNo childNeighbours
            bossRef <- pRef
            s <- double childNo
            w <- 1.0  

        | ReferenceList(childRefList) ->
            childRefs <- childRefs @ childRefList

        | StartGossip(b) ->
            msgCount <- msgCount + 1
            let randomNeighbour = getRandArrElement childNeighbours
            let randomNeighbourIndex = randomNeighbour-1
            childRefs.Item(randomNeighbourIndex)<!Ping(randomNeighbour)

        | Ping(childNumber) ->
           // printfn "childNumber=%i msgCount=%i" childNumber msgCount
            if(msgCount<10) then
                msgCount <- msgCount + 1
                //printfn "msgCount=%i" msgCount

                if(Set.count availableActors=1) then
                    availableActors <- Set.remove childNumber availableActors
                    bossRef<!Done(childNumber)
                else 
                    let checker = checkAllNeighborsExhausted childNeighbours
                    
                    if (checker) then
                        let mutable randomActor = getRandArrElement (Set.toList availableActors)
                        while(randomActor=childNumber) do
                            randomActor <- getRandArrElement (Set.toList availableActors)
                        let randomActorIndex = randomActor-1
                        childRefs.Item(randomActorIndex)<!Ping(randomActor)
                    else
                        let mutable randomNeighbour = getRandArrElement childNeighbours
                        while(not (Set.contains randomNeighbour availableActors)) do
                            randomNeighbour <- getRandArrElement childNeighbours
                        let randomNeighbourIndex = randomNeighbour-1
                        childRefs.Item(randomNeighbourIndex)<!Ping(randomNeighbour)
            else
                availableActors <- Set.remove childNumber availableActors
                //printfn "Available set %A" availableActors

                if(Set.isEmpty availableActors) then
                    bossRef<!Done(childNumber)
                else
                    let checker = checkAllNeighborsExhausted childNeighbours
                    
                    if (checker) then
                        let mutable randomActor = getRandArrElement (Set.toList availableActors)
                        while(randomActor=childNumber) do
                            randomActor <- getRandArrElement (Set.toList availableActors)
                        let randomActorIndex = randomActor-1
                        childRefs.Item(randomActorIndex)<!Ping(randomActor)
                    else
                        let mutable randomNeighbour = getRandArrElement childNeighbours
                        while(not (Set.contains randomNeighbour availableActors)) do
                            randomNeighbour <- getRandArrElement childNeighbours
                        let randomNeighbourIndex = randomNeighbour-1
                        childRefs.Item(randomNeighbourIndex)<!Ping(randomNeighbour)
                    bossRef<!Done(childNumber)

                // if(Set.contains childNumber availableActors) then
                //     availableActors <- Set.remove childNumber availableActors
                //     bossRef<!Done(true)
                //bossRef<!Done(true)

        | StartPushSum(b) ->
            let randomNeighbour = getRandArrElement childNeighbours
            let randomNeighbourIndex = randomNeighbour-1
            s <- s/2.0
            w <- w/2.0
            childRefs.Item(randomNeighbourIndex)<!Pong(randomNeighbour,s,w)

        | Pong(childNumber,recS,recW) ->

            if(count<3) then
                let newS = s + recS
                let newW = w + recW

                if(Math.Abs ((newS/newW)-(s/w))<0.0000000001) then 
                    count <- count + 1
                else
                    count <- 0
                // printfn "Old ratio = %e" (s/w)
                // printfn "New ratio = %e" (newS/newW)
                // printfn "Difference = %e" (Math.Abs ((newS/newW)-(s/w)))
                s <- newS
                w <- newW

                if(Set.count availableActors=1) then
                    availableActors <- Set.remove childNumber availableActors
                   // printfn "xxxx %i" childNumber
                    bossRef<!Done(childNumber)
                else
                    let checker = checkAllNeighborsExhausted childNeighbours
                    
                    if (checker) then
                        let mutable randomActor = getRandArrElement (Set.toList availableActors)
                        while(randomActor=childNumber) do
                            randomActor <- getRandArrElement (Set.toList availableActors)
                        let randomActorIndex = randomActor-1
                        s<-s/2.0
                        w<-w/2.0
                        childRefs.Item(randomActorIndex)<!Pong(randomActor,s,w)
                    else
                        let mutable randomNeighbour = getRandArrElement childNeighbours
                        while(not (Set.contains randomNeighbour availableActors)) do
                            randomNeighbour <- getRandArrElement childNeighbours
                        let randomNeighbourIndex = randomNeighbour-1
                        s<-s/2.0
                        w<-w/2.0
                        childRefs.Item(randomNeighbourIndex)<!Pong(randomNeighbour,s,w)


                    // let mutable randomActor = getRandArrElement (Set.toList availableActors)
                    // while(randomActor=childNumber) do
                    //     randomActor <- getRandArrElement (Set.toList availableActors)
                    // let randomActorIndex = randomActor-1
                    // s<-s/2.0
                    // w<-w/2.0
                    // childRefs.Item(randomActorIndex)<!Pong(randomActor,s,w)

                // if(count=3) then 
                //     //printfn "ratio is %f" (s/w)  
                //     availableActors <- Set.remove childNumber availableActors
                //     bossRef<!Done(true)
            else 
                printfn "childNo %i : Ratio %f" childNumber (s/w)
                availableActors <- Set.remove childNumber availableActors
                if(Set.isEmpty availableActors) then
                    bossRef<!Done(childNumber)
                else
                    let checker = checkAllNeighborsExhausted childNeighbours
                    
                    if (checker) then
                        let mutable randomActor = getRandArrElement (Set.toList availableActors)
                        while(randomActor=childNumber) do
                            randomActor <- getRandArrElement (Set.toList availableActors)
                        let randomActorIndex = randomActor-1
                        s<-s/2.0
                        w<-w/2.0
                        childRefs.Item(randomActorIndex)<!Pong(randomActor,s,w)
                    else
                        let mutable randomNeighbour = getRandArrElement childNeighbours
                        while(not (Set.contains randomNeighbour availableActors)) do
                            randomNeighbour <- getRandArrElement childNeighbours
                        let randomNeighbourIndex = randomNeighbour-1
                        s<-s/2.0
                        w<-w/2.0
                        childRefs.Item(randomNeighbourIndex)<!Pong(randomNeighbour,s,w)
                    bossRef<!Done(childNumber)

        return! loop()
    }
    loop()

let parent (mailbox: Actor<_>) =
    let mutable completedActors = 0
    let mutable algorithm = null
    let mutable nodes = 0
    let mutable topology = null
    let mutable childRefList = List.empty
    let mutable parentRef = null
    let rec loop () = actor {

        let! message = mailbox.Receive ()
        match message with
        | IntializeParent(x,y,z,p) -> 
            nodes <- x
            topology <- y
            algorithm <- z
            parentRef <- p
            availableActors <- Set.ofSeq [1..nodes]
            for i=1 to nodes do
                let neighbours = getNeighbours topology i nodes
                let name = i.ToString()
                let childref = spawn system name child
                childRefList <- childRefList @ [childref]
                childref<!IntializeChild(i,neighbours,parentRef)

            for i=0 to childRefList.Length-1 do
                childRefList.Item(i)<!ReferenceList(childRefList)
            
            timer.Start()
            if(algorithm="Gossip") then
                childRefList.Item(0)<!StartGossip(true)
            else
                childRefList.Item(0)<!StartPushSum(true)
        | Done(x) ->
            completedActors<-completedActors+1
            //let i = Set.count availableActors
            //printfn "Completed %i" x
           // printfn "%b" (Set.isEmpty availableActors)
            
            if(completedActors=nodes) then 
                //printfn ""
                
                printfn "All Nodes Converged"
                stopTime 1
                printfn "Press Any Key To Close"
                //Close all actors
                system.Terminate() |> ignore
        return! loop()
    }
    loop()



let args : string array = fsi.CommandLineArgs |> Array.tail

//Extract and convert to Int
let first = args.[0]|> int
let second = args.[1]|> string
let third = args.[2]|> string


let parentActor = spawn system "parent" parent
parentActor <! IntializeParent(first,second,third,parentActor)

System.Console.ReadKey() |> ignore