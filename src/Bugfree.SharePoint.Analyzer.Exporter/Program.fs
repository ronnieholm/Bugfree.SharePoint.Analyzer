open System
open System.Xml.Linq
open System.Collections.Generic
open Microsoft.SharePoint
open Microsoft.SharePoint.Administration

type E = XElement
let xn s = XName.Get s
let content = SPWebService.ContentService

let visitListItems (list: SPList) level (listItemsElement: E) =
    // itering items sometimes cases exceptions of the form "Feature '<guid>' 
    // for list template '<id>' is not installed in this farm". This is likely
    // caused by a custom feature which was once installed in farm and used to 
    // created the list, but which has since been deactivated and removed.
    try
        for item in list.Items |> Seq.cast<SPListItem> do
            let title = 
                try 
                    item.Title
                with
                | :? ArgumentException -> ""
                | _ -> item.Title
           
            let listItemElement =
                E(xn "ListItem",
                    E(xn "Id", item.UniqueId),
                    E(xn "ItemId", item.ID),
                    E(xn "Title", title.Replace(Convert.ToChar((byte)0x1F), ' ').Replace(Convert.ToChar((byte)0x0B), ' ')),
                    E(xn "Url", item.Url),
                    E(xn "CreatedAt", item.["Created"] :?> DateTime),
                    E(xn "ModifiedAt", item.["Modified"] :?> DateTime),
                    E(xn "ContentTypeId", item.["ContentTypeId"] |> string))
            listItemsElement.Add(listItemElement)
    with
    | :? System.ArgumentException as e -> printfn "%s" (sprintf "%s %s" e.Message e.StackTrace)

let visitLists (web: SPWeb) level (listsElement: E) =
    for list in web.Lists |> Seq.cast<SPList> do
        printfn "%s%s (list)" (new string(' ', level)) list.Title
        let listElement =
            E(xn "List",
                E(xn "Id", list.ID),                
                E(xn "Title", list.Title),
                E(xn "ListItems"))
        listsElement.Add(listElement)                 
        visitListItems list (level + 2) (listElement.Element(xn "ListItems"))

let rec visitWebRecursively (web: SPWeb) level (websElement: E) =
    // accessing the Name property of a web sometimes causes a
    // System.IO.DirectoryNotFoundException to be thrown. Attempting to access
    // the web through the browser, the web appears to not exist. Exception is
    // likely caused by a data inconsistency in the content database.
    try
        printfn "%s%s (web)" (new string(' ', level)) (if web.Name = "" then "No title" else web.Name)
        let webElement =
            E(xn "Web",
                E(xn "Id", web.ID),
                E(xn "Title", web.Name),
                E(xn "Url", web.ServerRelativeUrl),
                    E(xn "Lists"),
                    E(xn "Webs"))
   
        websElement.Add(webElement)
        visitLists web (level + 2) (webElement.Element(xn "Lists"))

        for child in web.Webs |> Seq.cast<SPWeb> do
            visitWebRecursively child (level + 2) (webElement.Element(xn "Webs"))
    with
    | :? System.IO.DirectoryNotFoundException as e -> printfn "%s" (sprintf "%s %s" e.Message e.StackTrace)


let visitSiteCollection (siteCollection: SPSite) level (siteCollectionsElement: E) =
    printfn "%s%s (site collection)" (new string(' ', level)) siteCollection.ServerRelativeUrl

    let siteCollectionElement =
        E(xn "SiteCollection",
            E(xn "Id", siteCollection.ID),
            E(xn "Url", siteCollection.ServerRelativeUrl),
            E(xn "Webs"))
    siteCollectionsElement.Add(siteCollectionElement)

    let rootWeb = siteCollection.RootWeb                        
    visitWebRecursively rootWeb (level + 2) (siteCollectionElement.Element(xn "Webs"))    

let visitWebApplications (webApplicationsElement: E) webApplicationName =
    for webApp in content.WebApplications do
        if webApp.Name = webApplicationName then
            printfn "%s (web app)" webApp.Name            
            let webApplicationElement =
                E(xn "WebApplication",
                    E(xn "Id", webApp.Id),
                    E(xn "Title", webApp.Name),
                    E(xn "SiteCollections")) 
            webApplicationsElement.Add(webApplicationElement)

            let siteCollections = webApp.Sites |> Seq.cast<SPSite>
            let mutable counter = 0
            for siteCollection in siteCollections do
                counter <- counter + 1
                printfn "  Processing %i of %i site collection" counter (siteCollections |> Seq.length)
                visitSiteCollection siteCollection 2 (webApplicationElement.Element(xn "SiteCollections"))                                        

[<EntryPoint>]
let main argv =
    let webApplicationName = argv.[0]
    let webApplications = 
        XDocument(
            XDeclaration("1.0", "utf-8", "yes"))            
    webApplications.Add(E(xn "WebApplications"))
    visitWebApplications (webApplications.Element(xn "WebApplications")) webApplicationName
    webApplications.Save (sprintf "%s.xml" webApplicationName)
    0