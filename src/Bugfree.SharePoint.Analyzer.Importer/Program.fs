open System
open System.Data.SqlClient
open FSharp.Data
open System.Xml.Linq
open System.Data.SqlClient

type E = XElement

module Db =
    [<Literal>]
    let compileTimeConnectionString = @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=" + __SOURCE_DIRECTORY__ + @"\WebApplications.mdf;Integrated Security=True"    

    type InsertWebApplication = SqlCommandProvider<"insert into [WebApplications] (Id, Title) values (@id, @title)", compileTimeConnectionString>
    type InsertSiteCollection = SqlCommandProvider<"insert into [SiteCollections] (Id, Url, WebApplicationId) values (@id, @url, @webApplicationId)", compileTimeConnectionString>
    type InsertWeb = SqlCommandProvider<"insert into [Webs] (Id, Title, Url, ParentWebId, SiteCollectionId) values (@id, @title, @url, @parentWebId, @siteCollectionId)", compileTimeConnectionString, AllParametersOptional = true>
    type InsertListItem = SqlCommandProvider<"insert into [ListItems] (Id, ItemId, Title, Url, CreatedAt, ModifiedAt, ContentTypeId, ListId) values (@id, @itemId, @title, @Url, @createdAt, @modifiedAt, @contentTypeId, @listId)", compileTimeConnectionString>
    type InsertList = SqlCommandProvider<"insert into [Lists] (Id, Title, WebId) values (@id, @title, @webId)", compileTimeConnectionString>

    type ClearAll = 
        SqlCommandProvider<
            "delete from [WebApplications]; delete from [SiteCollections]; delete from [Webs]; delete from [Lists]; delete from [ListItems]", compileTimeConnectionString>

let xn s = XName.Get s

let parseListItems (items: E) (c: SqlConnection) (t: SqlTransaction) =
    for item in items.Elements(xn "ListItem") do
        let id = Guid(item.Element(xn "Id").Value)
        let itemId = item.Element(xn "ItemId").Value |> int
        let title = (item.Element(xn "Title").Value)
        let url = (item.Element(xn "Url").Value)
        let createdAt = DateTime.Parse(item.Element(xn "CreatedAt").Value)
        let modifiedAt = DateTime.Parse(item.Element(xn "ModifiedAt").Value)
        let contentTypeId = (item.Element(xn "ContentTypeId").Value)
        let listId = Guid(item.Parent.Parent.Element(xn "Id").Value)
        (new Db.InsertListItem(c, t)).Execute(id, itemId, title, url, createdAt, modifiedAt, contentTypeId, listId) |> ignore

let parseLists (lists: E) (c: SqlConnection) (t: SqlTransaction) =
    for list in lists.Elements(xn "List") do
        let id = Guid(list.Element(xn "Id").Value)
        let title = list.Element(xn "Title").Value
        let webId = Guid(lists.Parent.Element(xn "Id").Value)
        (new Db.InsertList(c, t)).Execute(id, title, webId) |> ignore
        parseListItems (list.Element(xn "ListItems")) c t

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
         
        parseLists (web.Element(xn "Lists")) c t
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
    let importFilePath = argv.[0]
    let runtimeConnectionString = argv.[1]
    use connection = new SqlConnection(runtimeConnectionString)

    connection.Open()
    use transaction = connection.BeginTransaction()
    let document = XDocument.Load(importFilePath)
    let webApplications = document.Root    

    try
        (new Db.ClearAll(connection, transaction)).Execute() |> ignore
        parseWebApplications webApplications connection transaction
        transaction.Commit()
    with
    | _ -> 
        transaction.Rollback()
        reraise()
    0