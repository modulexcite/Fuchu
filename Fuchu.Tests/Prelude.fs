﻿namespace Fuchu

open System
open FSharpx

[<AutoOpen>]
module Assert = 
    let nullBool2 f a b =
        if a = null && a = null then
            true
        elif a = null || b = null then
            false
        else
            f b a
        
    let addNewLine s = 
        if String.IsNullOrEmpty s
            then ""
            else s + "\n"

    let inline assertNone preface =
        let preface = addNewLine preface
        function
        | Some x -> failtestf "%sExpected None, actual Some (%A)" preface x
        | _ -> ()

    let inline assertEqual preface =
        let preface = addNewLine preface
        fun expected actual ->
            if expected <> actual then
                failtestf "%sExpected: %A\nActual: %A" preface expected actual

    let inline (==?) actual expected =
        assertEqual null expected actual

    let inline assertStringContains preface =
        let run = nullBool2 (fun (actual: string) (expected: string) -> actual.Contains expected)
        let preface = addNewLine preface
        fun expected actual ->
            if not (run expected actual)
                then failtestf "%sExpected string to contain '%s'\nActual: %s" preface expected actual

    let inline assertRaise preface (ex: Type) f =
        let preface = addNewLine preface
        try
            f()
            failtestf "%sExpected exception '%s' but no exception was raised" preface ex.FullName
        with e ->
            if e.GetType() <> ex
                then failtestf "%sExpected exception '%s' but raised:\n%A" preface ex.FullName e
                
module Seq = 
    let (|Empty|Cons|) l = 
        if Seq.isEmpty l
            then Empty
            else Cons(Seq.head l, Seq.skip 1 l)

    let (|One|_|) l = 
        match Seq.toList l with
        | [x] -> Some x
        | _ -> None

    let (|Two|_|) l = 
        match Seq.toList l with
        | [x;y] -> Some(x,y)
        | _ -> None

module String =
    let internal nullOption2 f a b =
        nullBool2 f a b |> Option.fromBool

    let (|StartsWith|_|) =
        nullOption2 (fun (s: string) -> s.StartsWith)

    let (|Contains|_|) =
        nullOption2 (fun (s: string) -> s.Contains)

[<AutoOpen>]
module TestHelpers = 
    open Fuchu
    open Fuchu.Impl

    let evalSilent = eval TestPrinters.Default Seq.map

    let inline assertTestFails test =
        let test = TestCase test
        match evalSilent test with
        | [{ TestRunResult.Result = TestResult.Failed _ }] -> ()
        | x -> failtestf "Should have failed, but was %A" x

    open FsCheck

    let genLimitedTimeSpan = 
        lazy (
            Arb.generate<TimeSpan> 
            |> Gen.suchThat (fun t -> t.Days = 0)
        )

    let genTestResultCounts = 
        lazy (
            gen {
                let! passed = Arb.generate<int>
                let! ignored = Arb.generate<int>
                let! failed = Arb.generate<int>
                let! errored = Arb.generate<int>
                let! time = genLimitedTimeSpan.Value
                return {
                    TestResultCounts.Passed = passed
                    Ignored = ignored
                    Failed = failed
                    Errored = errored
                    Time = time
                }
            }
        )

    let shrinkTestResultCounts (c: TestResultCounts) : TestResultCounts seq = 
        seq {
            for passed in Arb.shrink c.Passed do
            for ignored in Arb.shrink c.Ignored do
            for failed in Arb.shrink c.Failed do
            for errored in Arb.shrink c.Errored do
            for time in Arb.shrink c.Time ->
            {
                TestResultCounts.Passed = passed
                Ignored = ignored
                Failed = failed
                Errored = errored
                Time = time
            }
        }

    let arbTestResultCounts = 
        lazy (
            Arb.fromGenShrink(genTestResultCounts.Value, shrinkTestResultCounts)
        )

    let twoTestResultCounts = 
        lazy (
            Gen.two arbTestResultCounts.Value.Generator |> Arb.fromGen
        )
