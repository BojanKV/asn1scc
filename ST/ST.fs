﻿module ST

open FsUtils
open System
open System.Numerics
open System.Globalization
open Antlr.StringTemplate
open System.IO

type dummy = 
    | Dummy1 

let mutable lang:Ast.ProgrammingLanguage = Ast.ProgrammingLanguage.Unknown
type StrHelper =
    StrHelper of string
    with 
    override this.ToString () = 
        match this with  StrHelper(actStr) -> actStr
    member this.U1 =
        match this with  StrHelper(actStr) -> actStr.U1
    member this.L1 =
        match this with  StrHelper(actStr) -> actStr.L1
    member this.RDA =
        match this with  StrHelper(actStr) -> actStr.RDA
    member this.JSEsc =
        match this with  StrHelper(actStr) -> actStr.JSEsc
    member this.ISQ =
        match this with  StrHelper(actStr) -> actStr.ISQ

        

type BasicFormatRenderer() =
    interface IAttributeRenderer with
        member this.ToString o = o.ToString()
        member this.ToString(o,format) =
            let frmStr = "{0:" + format + "}"
            String.Format(frmStr, o)


type BasicFormatRenderer2<'T when 'T :equality>() =
    interface IAttributeRenderer with
        member this.ToString o = (o :?> PrimitiveWithLocation<'T>).Value.ToString()
        member this.ToString(o,format) =    
            let frmStr = "{0:" + format + "}"
            String.Format(frmStr, (o :?> PrimitiveWithLocation<'T>).Value)


type StringFormatRenderer() =
    static member TS o format =
        let frmStr = "{0," + format + "}";
        String.Format(frmStr, o.ToString())
    interface IAttributeRenderer with
        member this.ToString o = o.ToString()
        member this.ToString(o,format) = StringFormatRenderer.TS o format

type StringFormatRenderer2() =
    interface IAttributeRenderer with
        member this.ToString o = (o :?> StringLoc).Value.ToString()
        member this.ToString(o,format) = StringFormatRenderer.TS (o :?> StringLoc).Value format


type RealFormatRenderer() =
    interface IAttributeRenderer with
        member this.ToString (o:Object) = 
            let v = o :?> double
            let result = 
                match lang with
                | Ast.Html  -> v.ToString()
                | _         -> v.ToString("E", NumberFormatInfo.InvariantInfo)
//            String.Format(NumberFormatInfo.InvariantInfo, "{0}", o)
//            if ((v%1.0) = 0.0) && not(result.Contains("E")) then
//                result + ".0"
//            else
//                result
            result
        member this.ToString(o,format) = 
            String.Format(NumberFormatInfo.InvariantInfo, "{0:" + format + "}", o)

type RealFormatRenderer2() =
    interface IAttributeRenderer with
        member this.ToString o =
            String.Format(NumberFormatInfo.InvariantInfo, "{0}", (o :?> DoubleLoc).Value)
        member this.ToString(o,format) = 
            String.Format(NumberFormatInfo.InvariantInfo, "{0:" + format + "}", (o :?> DoubleLoc).Value)

            
type BigIntegerFormatRenderer() =
    static member TS1 (obj:BigInteger) =
        if (BigInteger Int32.MinValue < obj && obj < BigInteger Int32.MaxValue) then
            obj.ToString();
        else
            match lang with
            | Ast.ProgrammingLanguage.C -> 
                if obj = (BigInteger System.Int64.MinValue) then
                    "LLONG_MIN"
                else
                    obj.ToString() + "LL"
            | _                         -> obj.ToString()
    static member TS2(o:Object, format) =
        let frmStr = "{0:" + format + "}";
        String.Format(frmStr, o);
    interface IAttributeRenderer with
        member this.ToString o = BigIntegerFormatRenderer.TS1 (o :?> BigInteger)
        member this.ToString(o,format) = BigIntegerFormatRenderer.TS2(o, format)


type BasicFormatRendererBigInt() =
    interface IAttributeRenderer with
        member this.ToString o = BigIntegerFormatRenderer.TS1 (o :?> IntLoc).Value
        member this.ToString(o,format) = BigIntegerFormatRenderer.TS2((o :?> IntLoc).Value, format)



let cache = System.Collections.Generic.Dictionary<string, StringTemplateGroup>()

let runsUnderMono() = 
    match System.Type.GetType("Mono.Runtime") with
    | null  -> false
    | _     -> true

let DollarDelimeterFiles = ["xml"; "icd_uper"]

let get_group  fileName =
    if cache.ContainsKey fileName then
        cache.[fileName]
    else
        let applicationFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetAssembly(typedefof<dummy>).Location);
        let devFolder = match runsUnderMono() with
                        | true  -> @"/mnt/camelot/prj/DataModeling/ASN1_FSHARP/Backend2.ST"
                        | false -> @"C:\prj\DataModeling\ASN1_FSHARP\Backend2.ST\"
        let custFolder =
            match Path.GetDirectoryName fileName with
            | ""    -> []
            | _     -> [Path.GetFullPath(Path.Combine(System.IO.Directory.GetCurrentDirectory(), Path.GetDirectoryName fileName))] 
        let stgFoldres = match Directory.Exists devFolder with
                         | true  -> custFolder @ [ applicationFolder; devFolder; (System.IO.Directory.GetCurrentDirectory ()) ]   |> Seq.toArray
                         | false -> custFolder @ [ applicationFolder; (System.IO.Directory.GetCurrentDirectory ()) ]  |> Seq.toArray
        let grpLoader = CommonGroupLoader(StringTemplateGroup.DEFAULT_ERROR_LISTENER, stgFoldres)
                        
        StringTemplateGroup.RegisterGroupLoader(grpLoader);
        let fileNameNotExt = Path.GetFileNameWithoutExtension(fileName)
        let HasDollarDelimeter () =
            let ContainsMagicLine (folder:string) =
                let absPath = System.IO.Path.Combine(folder, fileNameNotExt+".stg")
                match System.IO.File.Exists absPath with
                | false -> false
                | true  ->
                    let lines = System.IO.File.ReadAllLines absPath
                    lines |> Array.exists (fun (l:string) -> l.Contains("delimiters \"$\", \"$\""))
            let a1 = stgFoldres |> Array.exists  ContainsMagicLine
            let a2 = DollarDelimeterFiles |> Seq.exists ((=) fileNameNotExt)
            a1 || a2
        let group = match HasDollarDelimeter () with
                    |true   -> grpLoader.LoadGroup(fileNameNotExt, null, typedefof<Antlr.StringTemplate.Language.DefaultTemplateLexer>);
                    |false  -> grpLoader.LoadGroup(fileNameNotExt);

        group.RegisterAttributeRenderer(typedefof<BigInteger>, new BigIntegerFormatRenderer());
        group.RegisterAttributeRenderer(typedefof<UInt64>, new BasicFormatRenderer());
        group.RegisterAttributeRenderer(typedefof<Int64>, new BasicFormatRenderer());
        group.RegisterAttributeRenderer(typedefof<int>, new BasicFormatRenderer());
        group.RegisterAttributeRenderer(typedefof<byte>, new BasicFormatRenderer());
        group.RegisterAttributeRenderer(typedefof<double>, new RealFormatRenderer());
        group.RegisterAttributeRenderer(typedefof<float>, new RealFormatRenderer());
        group.RegisterAttributeRenderer(typedefof<string>, new StringFormatRenderer());
        group.RegisterAttributeRenderer(typedefof<StrHelper>, new StringFormatRenderer());
        group.RegisterAttributeRenderer(typedefof<PrimitiveWithLocation<Double>>, new RealFormatRenderer2());
        group.RegisterAttributeRenderer(typedefof<PrimitiveWithLocation<string>>, new StringFormatRenderer2());
        group.RegisterAttributeRenderer(typedefof<PrimitiveWithLocation<BigInteger>>, new BasicFormatRendererBigInt());
        group.RegisterAttributeRenderer(typedefof<PrimitiveWithLocation<bool>>, new BasicFormatRenderer2<bool>());
        group.RegisterAttributeRenderer(typedefof<PrimitiveWithLocation<byte>>, new BasicFormatRenderer2<byte>());
        cache.[fileName] <- group
        group

let call fileName macroName (attrs:seq<string*#Object>)=
    let group = get_group fileName

    let template = group.GetInstanceOf(macroName);
    attrs |> Seq.iter(fun (attrName, obj) -> template.SetAttribute(attrName,obj))
    template.ToString 80

let call2 (fileName, macroName, [<ParamArray>] attrs:array<string*#Object>)=
    let group = get_group fileName

    let template = group.GetInstanceOf(macroName);
    attrs |> Seq.iter(fun (attrName, obj) -> template.SetAttribute(attrName,obj))
    template.ToString 80


