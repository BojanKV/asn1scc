﻿module FrontEntMain

open Antlr.Runtime
open Antlr.Runtime.Tree
open System.IO
open Antlr.Asn1
open Antlr.Acn
open System.Collections.Generic
open System.Linq
open CommonTypes

//let CreateAstRoot (list:(ITree*string*array<IToken>) seq) (encodings:array<Asn1Encoding>) generateEqualFunctions typePrefix checkWithOss astXmlFileName icdUperHtmlFileName icdAcnHtmlFileName (mappingFunctionsModule:string) integerSizeInBytes =  

let antlrParse (lexer: ICharStream -> #ITokenSource ) parser treeParser (fileName: string, files : string seq) = 

    let stream = 
        match Seq.length files > 1 with
        | true  ->
            let memStream = new MemoryStream()
            files |> Seq.iter(fun f -> 
                let fileData  = File.ReadAllBytes(f)
                memStream.Write(fileData, 0, fileData.Length)
                memStream.WriteByte(byte 32)    //append a space in case there is no character after ASN.1 END
            )
            memStream.Position <- (int64 0)
            new CommonTokenStream(lexer(new ANTLRInputStream(memStream)));
        | false ->
            let file  = files |> Seq.toList |> List.head
            CommonTokenStream(lexer(new ANTLRFileStream(file)));
    let tokens = stream.GetTokens().Cast<IToken>() |> Seq.toArray
    let tree = treeParser(parser(stream));
    {ParameterizedAsn1Ast.AntlrParserResult.fileName = fileName; ParameterizedAsn1Ast.AntlrParserResult.rootItem=tree; ParameterizedAsn1Ast.AntlrParserResult.tokens=tokens}

let constructAst (args:CommandLineSettings) =
    let asn1ParseTrees = args.asn1Files |> Seq.groupBy(fun f -> f) |> Seq.map (antlrParse (fun f -> new asn1Lexer(f)) (fun ts -> new asn1Parser(ts))  (fun p -> p.moduleDefinitions().Tree :?> ITree)  ) |> Seq.toList
    let acnParseTrees = args.acnFiles |> Seq.groupBy(fun f -> f) |> Seq.map (antlrParse (fun f -> new acnLexer(f)) (fun ts -> new acnParser(ts))  (fun p -> p.moduleDefinitions().Tree :?> ITree)  ) |> Seq.toList

    (*
        * constructs a parameterized (templatized) ASN.1 AST by antlr trees.
        * A parameterized ASN.1 AST is the one that contains generic types. E.g.
        * 	
        * FrequenciesTemplate{INTEGER:someLength, SomeType } ::= SEQUENCE (SIZE(someLength)) OF SomeType
        * 
		* MyTestInt ::= FrequenciesTemplate{5, INTEGER(1..20)}
		* MyTestReal ::= FrequenciesTemplate{5, REAL}
        * 
        * 
    *)

    let parameterized_ast = CreateAsn1AstFromAntlrTree.CreateAstRoot asn1ParseTrees args

    (*
        *  Removes parameterized types by resolving them. In the example above
        *  FrequenciesTemplate will no longer exist
        *  and MyTestInt and MyTestReal will defined as follows
        *  MyTestInt ::= SEQUENCE (SIZE(5)) OF INTEGER(1..20)
        *  MyTestReal ::= SEQUENCE (SIZE(5)) OF REAL
        *)
    let asn1Ast0 = MapParamAstToNonParamAst.DoWork parameterized_ast


    (*
        * Performs semantic validations which cannot be handled during ANTLR parsing.
        * For example the following definition
        * MySeq ::= {a INTEGER, a REAL}
        * is OK for ANTLR but of course not OK for ASN.1
        *)
    //todo : check for commented code with uPER.
    CheckAsn1.CheckFiles asn1Ast0 0

    (*
        Ensure unique enum names
    *)
    let uniqueEnumNamesAst = EnsureUniqueEnumNames.DoWork asn1Ast0 

    (*
        - Updates ASN.1 AST with ASN.1 information
        - Creates the expanded tree (i.e reference types are now resolved)
    *)
    let acnAst = AcnCreateFromAntlr.mergeAsn1WithAcnAst uniqueEnumNamesAst acnParseTrees

    (*
        check acn references
    *)
    let acnDeps = CheckLongReferences.checkAst acnAst

    (acnAst, acnDeps)

