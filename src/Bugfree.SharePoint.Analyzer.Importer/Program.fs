open System
open System.Data.SqlClient
open FSharp.Data
open System.Xml.Linq

type E = XElement

module Db =
    [<Literal>]
    let compileTimeConnectionString = @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=" + __SOURCE_DIRECTORY__ + @"\WebApplications.mdf;Integrated Security=True"    

    type InsertWebApplication = SqlCommandProvider<"insert into [WebApplications] (Id, Title) values (@id, @title)", compileTimeConnectionString>
    type InsertSiteCollection = SqlCommandProvider<"insert into [SiteCollections] (Id, Url, WebApplicationId) values (@id, @url, @webApplicationId)", compileTimeConnectionString>
    type InsertWeb = SqlCommandProvider<"insert into [Webs] (Id, Title, Url, ParentWebId, SiteCollectionId) values (@id, @title, @url, @parentWebId, @siteCollectionId)", compileTimeConnectionString, AllParametersOptional = true>
    type InsertListItem = SqlCommandProvider<"insert into [ListItems] (Id, ItemId, Title, Url, CreatedAt, ModifiedAt, ContentTypeId, ListId) values (@id, @itemId, @title, @Url, @createdAt, @modifiedAt, @contentTypeId, @listId)", compileTimeConnectionString>
    type InsertList = SqlCommandProvider<"insert into [Lists] (Id, Title, WebId) values (@id, @title, @webId)", compileTimeConnectionString>
    type InsertDocumentLibrary = SqlCommandProvider<"insert into [DocumentLibraries] (Id, Title, WebId) values (@id, @title, @webId)", compileTimeConnectionString>
    type InsertDocumentLibraryItem = SqlCommandProvider<"insert into [DocumentLibraryItems] (Id, ItemId, Title, Url, CreatedAt, ModifiedAt, ContentTypeId, DocumentLibraryId, Name, Length, CheckOutStatus, CheckedOutBy, CheckedOutDate, MajorVersion, MinorVersion) values (@id, @itemId, @title, @Url, @createdAt, @modifiedAt, @contentTypeId, @documentLibraryId, @name, @length, @checkOutStatus, @checkedOutBy, @checkedOutDate, @majorVersion, @minorVersion)", compileTimeConnectionString>

    type ClearAll = 
        SqlCommandProvider<
            "delete from [WebApplications]; 
             delete from [SiteCollections]; 
             delete from [Webs]; 
             delete from [Lists]; 
             delete from [ListItems];
             delete from [DocumentLibraries];
             delete from [DocumentLibraryItems]", compileTimeConnectionString>

let xn s = XName.Get s

let parseListItems (items: E) (c: SqlConnection) (t: SqlTransaction) =
    for i in items.Elements(xn "ListItem") do
        let id = Guid(i.Element(xn "Id").Value)
        let itemId = i.Element(xn "ItemId").Value |> int
        let title = (i.Element(xn "Title").Value)
        let url = (i.Element(xn "Url").Value)
        let createdAt = DateTime.Parse(i.Element(xn "CreatedAt").Value)
        let modifiedAt = DateTime.Parse(i.Element(xn "ModifiedAt").Value)
        let contentTypeId = (i.Element(xn "ContentTypeId").Value)
        let listId = Guid(i.Parent.Parent.Element(xn "Id").Value)
        (new Db.InsertListItem(c, t)).Execute(id, itemId, title, url, createdAt, modifiedAt, contentTypeId, listId) |> ignore

let parseDocumentLibraryItems (items: E) (c: SqlConnection) (t: SqlTransaction) =
    for d in items.Elements(xn "DocumentLibraryItem") do
        let id = Guid(d.Element(xn "Id").Value)
        let itemId = d.Element(xn "ItemId").Value |> int
        let title = (d.Element(xn "Title").Value)
        let url = (d.Element(xn "Url").Value)
        let createdAt = DateTime.Parse(d.Element(xn "CreatedAt").Value)
        let modifiedAt = DateTime.Parse(d.Element(xn "ModifiedAt").Value)
        let contentTypeId = (d.Element(xn "ContentTypeId").Value)
        let listId = Guid(d.Parent.Parent.Element(xn "Id").Value)

        // file subtree
        let f = d.Element(xn "File")
        let name = f.Element(xn "Name").Value
        let length = Int32.Parse(f.Element(xn "Length").Value)
        let checkOutStatus = f.Element(xn "CheckOutStatus").Value
        let checkedOutBy = f.Element(xn "CheckedOutBy").Value
        let checkedOutDate = f.Element(xn "CheckedOutDate").Value
        let majorVersion = Int32.Parse(f.Element(xn "MajorVersion").Value)
        let minorVersion = Int32.Parse(f.Element(xn "MinorVersion").Value)        

        // input date invalid when convertering to DateTime. Thus, we change
        // it some a valid default date
        // TODO: Change exporter instead
        let checkedOutDate' =
            if checkedOutDate = "0001-01-01T00:00:00"
            then DateTime(1900, 1, 1)
            else DateTime.Parse(checkedOutDate)

        (new Db.InsertDocumentLibraryItem(c, t)).Execute(id, itemId, title, url, createdAt, modifiedAt, contentTypeId, listId, name, length, checkOutStatus, checkedOutBy, checkedOutDate', majorVersion, minorVersion) |> ignore

let parseLists (lists: seq<E>) (c: SqlConnection) (t: SqlTransaction) =
    for l in lists do
        let id = Guid(l.Element(xn "Id").Value)
        let title = l.Element(xn "Title").Value
        let webId = Guid(l.Parent.Parent.Element(xn "Id").Value)
        (new Db.InsertList(c, t)).Execute(id, title, webId) |> ignore
        parseListItems (l.Element(xn "ListItems")) c t

let parseDocumentLibraries (documentLibraries: seq<E>) (c: SqlConnection) (t: SqlTransaction) =
    for d in documentLibraries do
        let id = Guid(d.Element(xn "Id").Value)
        let title = d.Element(xn "Title").Value
        let webId = Guid(d.Parent.Parent.Element(xn "Id").Value)
        (new Db.InsertDocumentLibrary(c, t)).Execute(id, title, webId) |> ignore
        parseDocumentLibraryItems (d.Element(xn "DocumentLibraryItems")) c t        
      
let rec parseWebs (webs: E) (c: SqlConnection) (t: SqlTransaction) =
    let getParentWebId (web: E) =        
        let p = web.Parent
        if p.Name = xn "Web" then Some(Guid(p.Element(xn "Id").Value)) 
        elif p.Name = xn "Webs" && p.Parent.Name = xn "Web" then Some(Guid(p.Parent.Element(xn "Id").Value))
        else None

    let rec getSiteCollectionId (webs: E) =
        let p = webs.Parent
        if p.Name = xn "SiteCollection"
        then p.Element(xn "Id").Value
        else getSiteCollectionId(p) 

    let siteCollectionId =  Guid(getSiteCollectionId(webs))

    for web in webs.Elements(xn "Web") do
        let id = Guid(web.Element(xn "Id").Value)
        let title = web.Element(xn "Title").Value
        let url = web.Element(xn "Url").Value
        (new Db.InsertWeb(c, t)).Execute(Some(id), Some(title), Some(url), getParentWebId(web), Some(siteCollectionId)) |> ignore

        let lists = web.Element(xn "Lists").Descendants(xn "List") |> Seq.toList
        let documentLibraries = web.Element(xn "Lists").Descendants(xn "DocumentLibrary") |> Seq.toList
        parseLists lists c t
        parseDocumentLibraries documentLibraries c t
        parseWebs (web.Element(xn "Webs")) c t

let parseSiteCollections (scs: E) (c: SqlConnection) (t: SqlTransaction) =
    for sc in scs.Elements(xn "SiteCollection") do
        let id = Guid(sc.Element(xn "Id").Value)
        let url = sc.Element(xn "Url").Value
        let webApplicationId = Guid(scs.Parent.Element(xn "Id").Value)
        (new Db.InsertSiteCollection(c, t)).Execute(id, url, webApplicationId) |> ignore
        parseWebs (sc.Element(xn "Webs")) c t

let parseWebApplication (wa: E) (c: SqlConnection) (t: SqlTransaction) =
    let id = Guid(wa.Element(xn "Id").Value)
    let title = wa.Element(xn "Title").Value
    (new Db.InsertWebApplication(c, t)).Execute(id, title) |> ignore    
    parseSiteCollections (wa.Element(xn "SiteCollections")) c t

let parseWebApplications (webApplications: E) (c: SqlConnection) (t: SqlTransaction) =
    for webApplication in webApplications.Elements(xn "WebApplication") do
        parseWebApplication webApplication c t

[<EntryPoint>]
let main argv = 
    match argv |> Array.toList with
    | ["--importFilePath"; importFilePath; "--connection-string"; runtimeConnectionString] ->
        use connection = new SqlConnection(runtimeConnectionString)

        connection.Open()
        use transaction = connection.BeginTransaction()
        let document = XDocument.Load(importFilePath)
        let webApplications = document.Root    

        try
            //(new Db.ClearAll(connection, transaction)).Execute() |> ignore
            parseWebApplications webApplications connection transaction
            transaction.Commit()
        with
        | _ -> 
            transaction.Rollback()
            reraise()
    | ["--help"]
    | _ ->
        printfn "Bugfree.SharePoint.Analyzer.Importer.exe --importFilePath <file-path> --connection-string <connection-string>"

    0