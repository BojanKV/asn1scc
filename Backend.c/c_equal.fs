﻿(*
* Copyright (c) 2008-2012 Semantix and (c) 2012-2015 Neuropublic
*
* This file is part of the ASN1SCC tool.
*
* Licensed under the terms of GNU General Public Licence as published by
* the Free Software Foundation.
*
*  For more informations see License.txt file
*)

module c_equal

open System.Numerics
open CommonTypes
open FsUtils
open Ast
open System.IO
open VisitTree
open uPER
open CloneTree
open c_utils



let rec EmitTypeBody (t:Asn1Type) (path:list<string>)  (m:Asn1Module) (r:AstRoot) (cp1:string option, cp2:string option)   =
    let p1 = match cp1 with Some p1 -> p1 | None -> GetTypeAccessPathPriv "pVal1" path  r
    let p2 = match cp2 with Some p2 -> p2 | None -> GetTypeAccessPathPriv "pVal2" path  r
    match t.Kind with
    | Integer           -> c_src.isEqual_Integer p1 p2
    | Enumerated(_)     -> c_src.isEqual_Enumerated p1 p2
    | Boolean           -> c_src.isEqual_Boolean p1 p2
    | Real              -> c_src.isEqual_Real p1 p2
    | NullType          -> c_src.isEqual_NullType ()
    | IA5String         -> c_src.isEqual_IA5String p1 p2
    | NumericString     -> c_src.isEqual_NumericString p1 p2
    | OctetString       ->
        match (GetTypeUperRange t.Kind t.Constraints r) with
        | Concrete(a,b) when  a=b   -> c_src.isEqual_OctetString p1 p2 true a
        | Concrete(a,b)             -> c_src.isEqual_OctetString p1 p2 false a
        | NegInf(_)                 -> raise (BugErrorException("Negative size"))
        | PosInf(_)                 -> raise (BugErrorException("All sizeable types must be constraint, otherwise max size is infinite"))
        | Full                      -> raise (BugErrorException("All sizeable types must be constraint, otherwise max size is infinite"))
        | Empty                     -> raise (BugErrorException("I do not known how this is handled"))      
    | BitString         ->
        match (GetTypeUperRange t.Kind t.Constraints r) with
        | Concrete(a,b) when  a=b   -> c_src.isEqual_BitString p1 p2 true a
        | Concrete(a,b)             -> c_src.isEqual_BitString p1 p2 false a
        | NegInf(_)                 -> raise (BugErrorException("Negative size"))
        | PosInf(_)                 -> raise (BugErrorException("All sizeable types must be constraint, otherwise max size is infinite"))
        | Full                      -> raise (BugErrorException("All sizeable types must be constraint, otherwise max size is infinite"))
        | Empty                     -> raise (BugErrorException("I do not known how this is handled"))      
    | Choice(children)  ->
        let arrChildre = children |> Seq.map(fun c -> c_src.isEqual_Choice_Child (c.CName_Present C) (EmitTypeBody c.Type (path@[c.Name.Value]) m r (None, None)))
        c_src.isEqual_Choice p1 p2 arrChildre
    | ReferenceType(md,ts, _)  ->
        let getPrtAux (cp:string option) (pVal:string) =
            match cp with
            | None      -> GetTypeAccessPathPrivPtr pVal path  r
            | Some x   ->
                match (Ast.GetActualTypeByName md ts r).Kind with
                | IA5String 
                | NumericString -> x
                | _             -> sprintf "&%s" x
            
        let ptr1 = getPrtAux cp1 "pVal1"
        let ptr2 = getPrtAux cp2 "pVal2"
        let tsName = Ast.GetTasCName ts.Value r.TypePrefix
        c_src.isEqual_ReferenceType ptr1 ptr2 tsName
    | SequenceOf(child)     -> 
        let getIndexNameByPath (path:string list) =
            "i" + ((Seq.length (path |> Seq.filter(fun s -> s ="#"))) + 1).ToString()
        let index = getIndexNameByPath path
        let sInnerType = EmitTypeBody child (path@["#"]) m r (None, None)
        match (GetTypeUperRange t.Kind t.Constraints r) with
        | Concrete(a,b) when  a=b   -> c_src.isEqual_SequenceOf p1 p2 index true a sInnerType
        | Concrete(a,b)             -> c_src.isEqual_SequenceOf p1 p2 index false a sInnerType
        | NegInf(_)                 -> raise (BugErrorException("Negative size"))
        | PosInf(_)                 -> raise (BugErrorException("All sizeable types must be constraint, otherwise max size is infinite"))
        | Full                      -> raise (BugErrorException("All sizeable types must be constraint, otherwise max size is infinite"))
        | Empty                     -> raise (BugErrorException("I do not known how this is handled"))      
    | Sequence(children)    -> 
        let asn1Children = children |> List.filter(fun x -> not x.AcnInsertedField)
        let printChild (c:ChildInfo) sNestedContent = 
            let chKey = (path@[c.Name.Value])
            let sChildBody = EmitTypeBody c.Type chKey m r (None, None)
            let content = 
                match c.Optionality with
                | Some(Default(dv)) -> 
                    let sDefaultValue = c_variables.PrintAsn1Value dv c.Type false ("",0) m r
                    let sChildTypeDeclaration = c_h.PrintTypeDeclaration c.Type chKey r
                    let sInnerType1Def = EmitTypeBody c.Type chKey m r (Some "defValue", None)
                    let sInnerTypeDef2 = EmitTypeBody c.Type chKey m r (None, Some "defValue")
                    c_src.isEqual_Sequence_child_default p1 p2 sDefaultValue (c.CName ProgrammingLanguage.C) sChildTypeDeclaration sChildBody sInnerType1Def sInnerTypeDef2
                | _                -> c_src.isEqual_Sequence_child p1 p2 c.Optionality.IsSome (c.CName ProgrammingLanguage.C) sChildBody
            c_src.JoinItems content sNestedContent

        let rec printChildren  = function
            |[]     -> null
            |x::xs  -> printChild x  (printChildren xs)
    
        printChildren asn1Children

    

let OnType_collerLocalVariables (t:Asn1Type) (path:list<string>) (parent:option<Asn1Type>) (ass:Assignment) (m:Asn1Module) (r:AstRoot) (state:list<LOCAL_VARIABLE>) = 
    match t.Kind with
    | SequenceOf(child) -> 
        let nCurLevel =  (Seq.length (path |> Seq.filter(fun s -> s ="#"))) + 1
        let nAllocatedLevel = state |> Seq.filter(fun lv -> match lv with SEQUENCE_OF_INDEX(_)->true |_ -> false )|>Seq.length
        if nCurLevel>nAllocatedLevel then
            (SEQUENCE_OF_INDEX (nCurLevel))::state
        else
            state
    | _             -> state

let CollectLocalVars (t:Asn1Type) (tas:TypeAssignment) (m:Asn1Module) (r:AstRoot) =
    let lvs = VisitType t [m.Name.Value; tas.Name.Value] None (TypeAssignment tas)  m r {DefaultVisitors with visitType=OnType_collerLocalVariables} []
    let emitLocalVariable  = function
        | SEQUENCE_OF_INDEX(i) -> c_src.Emit_local_variable_SQF_Index (BigInteger i) 
        | _                 -> ""
    lvs |> Seq.map emitLocalVariable

let PrintTypeAss (tas:TypeAssignment) (m:Asn1Module) (r:AstRoot) = 
    let sName = tas.GetCName r.TypePrefix
    let t = RemoveWithComponents tas.Type r
    let sStar = (TypeStar tas.Type r)
    let localVars = CollectLocalVars t tas m r
    let sContent = EmitTypeBody t [m.Name.Value; tas.Name.Value] m r (None, None)
    c_src.PrintEqual sName sStar localVars sContent 


