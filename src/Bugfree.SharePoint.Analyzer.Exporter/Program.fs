open System
open System.Xml.Linq
open System.Collections.Generic
open System.Text.RegularExpressions
open Microsoft.SharePoint
open Microsoft.SharePoint.Administration

type E = XElement
let xn s = XName.Get s
let content = SPWebService.ContentService

let visitListItems (list: SPList) level (listItemsElement: E) =
    // iterating items sometimes cases exceptions of the form "Feature '<guid>' 
    // for list template '<id>' is not installed in this farm". Error is likely
    // caused by a custom feature which was once installed in farm and used to 
    // created the list, but which has since been deactivated and removed.
    try
        for i in list.Items |> Seq.cast<SPListItem> do
            let title = 
                try 
                    // titles have been observered to sometimes contain special characters.
                    // It's unclear how these characters got introduced in the first place.
                    i.Title.Replace(Convert.ToChar((byte)0x1F), ' ').Replace(Convert.ToChar((byte)0x0B), ' ')
                with
                | :? ArgumentException -> ""
                | _ -> i.Title
           
            let listItemElement =
                E(xn "ListItem",
                    E(xn "Id", i.UniqueId),
                    E(xn "ItemId", i.ID),
                    E(xn "Title", title),
                    E(xn "Url", i.Url),
                    E(xn "CreatedAt", i.["Created"] :?> DateTime),
                    E(xn "ModifiedAt", i.["Modified"] :?> DateTime),
                    E(xn "ContentTypeId", i.["ContentTypeId"] |> string))
            listItemsElement.Add(listItemElement)
    with
    | :? System.ArgumentException as e -> 
        // error is likely caused by a custom feature which was once installed in farm and used to 
        // created the list, but which has since been deactivated and removed.
        if Regex.IsMatch(e.Message,"Feature '.+' for list template '.+' is not installed in this farm.") 
        then printfn "%s %s" e.Message e.StackTrace
        else reraise()

let visitDocumentLibraryItems (list: SPDocumentLibrary) level  (documentLibraryItemsElement: E) =
    try
        for i in list.Items |> Seq.cast<SPListItem> do
            let f = i.File
            let title = 
                try 
                    i.Title.Replace(Convert.ToChar((byte)0x1F), ' ').Replace(Convert.ToChar((byte)0x0B), ' ').Replace(Convert.ToChar((byte)0x07), ' ')
                with
                | :? ArgumentException -> ""
                | _ -> i.Title            

            // SPCheckOutStatus enumeration values documented at
            // https://msdn.microsoft.com/en-us/library/microsoft.sharepoint.spfile.spcheckoutstatus(v=office.12).aspx
            let checkedOutBy = 
                if f.CheckOutStatus <> SPFile.SPCheckOutStatus.None
                then 
                    try
                        f.CheckedOutBy.LoginName
                    with
                    | :? Microsoft.SharePoint.SPException as e ->
                        if e.Message.Contains("User cannot be found")
                        then "UserCannotBeFound"
                        else reraise()
                else ""           

            let checkedOutDate =
                if f.CheckOutStatus <> SPFile.SPCheckOutStatus.None 
                then f.CheckedOutDate 
                else DateTime.MinValue

            let documentLibraryItemElement =
                E(xn "DocumentLibraryItem",
                    E(xn "Id", i.UniqueId),
                    E(xn "ItemId", i.ID),
                    E(xn "Title", title),
                    E(xn "Url", i.Url),
                    E(xn "CreatedAt", i.["Created"] :?> DateTime),
                    E(xn "ModifiedAt", i.["Modified"] :?> DateTime),
                    E(xn "ContentTypeId", i.["ContentTypeId"] |> string),
                    E(xn "File",
                        E(xn "Name", f.Name),
                        E(xn "Length", f.Length),
                        E(xn "CheckOutStatus", f.CheckOutStatus),
                        E(xn "CheckedOutBy", checkedOutBy),
                        E(xn "CheckedOutDate", checkedOutDate),
                        E(xn "MajorVersion", f.MajorVersion),
                        E(xn "MinorVersion", f.MinorVersion)))
            documentLibraryItemsElement.Add(documentLibraryItemElement)
    with 
    | :? System.ArgumentException as e ->
        // error is likely caused by a custom feature which was once installed in farm and used to 
        // created the list, but which has since been deactivated and removed.
        if Regex.IsMatch(e.Message,"Feature '.+' for list template '.+' is not installed in this farm.") 
        then printfn "%s %s" e.Message e.StackTrace
        else reraise()

let visitLists (web: SPWeb) level (listsElement: E) =
    for list in web.Lists |> Seq.cast<SPList> do
        match list.BaseType with
        | SPBaseType.DocumentLibrary -> 
            printfn "%s%s (document library)" (new string(' ', level)) list.Title
            let listElement =
                E(xn "DocumentLibrary",
                    E(xn "Id", list.ID),
                    E(xn "Title", list.Title),
                    E(xn "DocumentLibraryItems"))
            listsElement.Add(listElement)
            visitDocumentLibraryItems (list :?> SPDocumentLibrary) (level + 2) (listElement.Element(xn "DocumentLibraryItems"))
        | _ -> 
            printfn "%s%s (list)" (new string(' ', level)) list.Title
            let listElement =
                E(xn "List",
                    E(xn "Id", list.ID),                
                    E(xn "Title", list.Title),
                    E(xn "ListItems"))
            listsElement.Add(listElement)
            visitListItems list (level + 2) (listElement.Element(xn "ListItems"))

let rec visitWeb (web: SPWeb) level (websElement: E) =
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
            visitWeb child (level + 2) (webElement.Element(xn "Webs"))
    with
    | :? System.IO.DirectoryNotFoundException as e -> 
        // accessing the Name property of a web sometimes causes a
        // System.IO.DirectoryNotFoundException to be thrown. Attempting to access
        // the web through the browser, the web appears to not exist. Exception is
        // likely caused by a data inconsistency in the content database.        
        printfn "%s %s" e.Message e.StackTrace

let visitSiteCollection (siteCollection: SPSite) level (siteCollectionsElement: E) =
    printfn "%s%s (site collection)" (new string(' ', level)) siteCollection.ServerRelativeUrl

    let siteCollectionElement =
        E(xn "SiteCollection",
            E(xn "Id", siteCollection.ID),
            E(xn "Url", siteCollection.ServerRelativeUrl),
            E(xn "Webs"))
    siteCollectionsElement.Add(siteCollectionElement)

    let rootWeb = siteCollection.RootWeb                        
    visitWeb rootWeb (level + 2) (siteCollectionElement.Element(xn "Webs"))    

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
    match argv |> Array.toList with
    | ["--web-application-name"; webApplicationName] ->
        let webApplications = 
            XDocument(
                XDeclaration("1.0", "utf-8", "yes"))            
        webApplications.Add(E(xn "WebApplications"))
        visitWebApplications (webApplications.Element(xn "WebApplications")) webApplicationName
        webApplications.Save (sprintf "%s.xml" webApplicationName)
    | ["--help"]
    | _ ->
        printfn "Bugfree.SharePoint.Analyzer.Exporter.exe --web-application-name <name>"
    0