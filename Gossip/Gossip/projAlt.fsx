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
    | IntializeChild of int list*IActorRef
    | StartGossip of bool
    | ReferenceList of IActorRef list
    | Ping of int
    | Done of bool

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

let child (mailbox: Actor<_>) = 
    let mutable childNeighbours = List.empty
    let mutable msgCount = 0
    let mutable childRefs = List.empty
    let mutable bossRef = null
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | IntializeChild(neighbours,pRef) -> 
            childNeighbours <- childNeighbours @ neighbours
            //printfn "%A" childNeighbours 
            bossRef <- pRef

        | ReferenceList(childRefList) ->
            childRefs <- childRefs @ childRefList

        | StartGossip(b) ->
            msgCount <- msgCount + 1
            let randomNeighbour = getRandArrElement childNeighbours
            let randomNeighbourIndex = randomNeighbour-1
            childRefs.Item(randomNeighbourIndex)<!Ping(randomNeighbour)

        | Ping(childNumber) ->
            msgCount <- msgCount + 1
            //printfn "%i xxxx" childNumber

            //availableActors <- Set.remove childNumber availableActors

            if(Set.count availableActors=1) then
                bossRef<!Done(true)
            else 
                let randomNeighbour = getRandArrElement childNeighbours
                if(Set.contains randomNeighbour availableActors) then
                    let randomNeighbourIndex = randomNeighbour-1
                    childRefs.Item(randomNeighbourIndex)<!Ping(randomNeighbour)
                else
                    let mutable randomActor = getRandArrElement (Set.toList availableActors)
                    while(randomActor=childNumber) do
                        randomActor <- getRandArrElement (Set.toList availableActors)
                    let randomActorIndex = randomActor-1
                    childRefs.Item(randomActorIndex)<!Ping(randomActor)

                if(msgCount=10) then 
                    availableActors <- Set.remove childNumber availableActors
                    bossRef<!Done(true)
                else
                    availableActors <- Set.add childNumber availableActors

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
                //printfn "%i -- %A" i neighbours
                let name = i.ToString()
                let childref = spawn system name child
                childRefList <- childRefList @ [childref]
                childref<!IntializeChild(neighbours,parentRef)

            for i=0 to childRefList.Length-1 do
                childRefList.Item(i)<!ReferenceList(childRefList)
            
            childRefList.Item(0)<!StartGossip(true)

        | Done(x) ->
            completedActors<-completedActors+1
            //let i = Set.count availableActors
            printfn "%i" completedActors
           // printfn "%b" (Set.isEmpty availableActors)
            
        if(completedActors=nodes) then 
            printfn ""
            printfn "Press Any Key To Close"

            //Close all actors
            system.Terminate() |> ignore
        return! loop()
    }
    loop()



// let args : string array = fsi.CommandLineArgs |> Array.tail

// //Extract and convert to Int
// let first = args.[0]|> int
// let second = args.[1]|> string
// let third = args.[2]|> string

let parentActor = spawn system "parent" parent
parentActor <! IntializeParent(1000,"imp","Gossip",parentActor)

