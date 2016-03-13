# Bugfree.SharePoint.Analyzer

SharePoint's API isn't optimized for dynamic queries on
structural metadata across web applications, site collections,
and webs. In this context, structural metadata refers to any
piece of information accessible through the SharePoint API and
which may help answer questions like the following:

  - Which content types are associated with which document libraries?
  - Which features are enabled where?
  - When was an item in a list last modified?
  - Which security groups are in effect where?
  - Which pages host which web parts?
  - ...

While each answer provides valuable insight into how SharePoint
is actually used -- particularly useful in migration scenarios or
validating governance -- answering each question may take hours
of query runtime in a large farm.

Instead of running multiple queries against the SharePoint API
directly, this project contains a skeleton exporter/importer to
project SharePoint's hierarchical metadata model onto a
relational read model stored in SQL database (via an intermediate
XML read model). Using standard database tools and techniques,
most queries can be answered in a matter of seconds.

Writing the export/import logic ourselves has the added benefit
of providing feedback on the source platform and help better
understand why the results come out the way they do.

In communicating answers to questions, tabular results may
suffice. Alternatively, PowerBI visualization on the basis of
parameterized SQL queries may come in handy.

## How to compile

In order to compile the exporter, first grab a copy of
Microsoft.SharePoint.dll from a SharePoint 2007 server and place
it in the libs folder. License restrictions preclude the
redistribution of the server-side library.

Because of the importer's use of the
[FSharp.Data.SqlClient](http://fsprojects.github.io/FSharp.Data.SqlClient)
type provider, a database with a schema matching the code is
required to compile the importer. Running the build script
creates the database and compiles the exporter and importer:

    % .\build.ps1

The LocalDB database generated by the script is solely intended
for compilation purposes. To import larger datasets into it,
adjust the script's values accordingly.

## How to use

Limited by the web services of SharePoint 2007, the exporter
connects to SharePoint through the server-side API. This implies
that the exporter be run on one of the farm's SharePoint
servers. A server likely not equipped with the latest in
operating systems and .NET frameworks. The server may not even
have Internet access.

Thus, the exporter is compiled against .NET Framework 3.5 and
runs on .NET runtime 2.0, which is what SharePoint itself
requires. To invoke it, provide the web application name as show
below. Remember to surround the argument with quotes if it
contains spaces:

    % .\Bugfree.SharePoint.Analyzer.Exporter.exe <web-app-name>
 
The exporter outputs an XML read model in
&lt;web-app-name&gt;.xml. This file must be transferred to
another computer running more up-to-date software for import into
the SQL database. Multiple XML files may be imported into the
same database as long as their web application names are unique.

XML file in hand, the importer is run as below, applying the same
rule of surrounding arguments containing spaces with quotes:

    % .\Bugfree.SharePoint.Analyzer.Importer.exe <web-app-name>.xml <connection-string>

The importer has been tested against LocalDB and SQL Azure. With
LocalDB, the connection string follows this pattern:

    Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=<absolute-path-to-mdf>;Integrated Security=True

## How to query

To order the list of site collections within a web application by
last list item modification date descending, the following query
is required. It provides a skeleton query for how to traverse the
hierarchical structure of SharePoint when represented
relationally:

```sql
select sc.Url, max(LastModifiedAt) LastModifiedAt from
    (select w.SiteCollectionId, LatestLists.LastModifiedAt from 
        (select l.Title, l.WebId, ListId, LastModifiedAt from
            (select li.ListId, max(li.ModifiedAt) LastModifiedAt
    	     from WebApplications wa
             inner join SiteCollections sc on sc.WebApplicationId = wa.Id
             inner join Webs w on w.SiteCollectionId = sc.Id
             inner join Lists l on l.WebId = w.Id
             inner join ListItems li on li.ListId = l.Id
             where wa.Title = '<web-app-name>'
             group by li.ListId) as LatestItems
             inner join Lists l on l.Id = LatestItems.ListId) as LatestLists
             inner join Webs w on w.Id = LatestLists.WebId) as LatestWebs
    inner join SiteCollections sc on sc.Id = LatestWebs.SiteCollectionId
    group by sc.Url
    order by LastModifiedAt desc

```

## How it works

The exporter constructs an XML representation of the projected
metadata. The XML is kept entirely in memory during construction
and may require GBs of memory, depending on the size of the web
application. The exporter is therefore compiled as a 64 bit
executable.

Exporting involves traversing SharePoint's hierarchical structure
in a depth-first manner, starting from the web application and
moving toward list items. Similarly, the importer contains a
recursive descent parser which does a depth-first traversal of
the XML model, emitting SQL statements as it progresses.

## Notes

The exporter and importer started out with a type-based domain
model of WebApplication, SiteCollection, Web, List, and
ListItem. Those types were then annotated with DataMember and
DataContract attributes to aid the DataContractSerializer with
XML serialization and deserialization. For some reason, .NET
wasn't able to correctly serialize and deserialize the XML back
into objects across .NET versions. Instead, serialization and
deserialization is now handcrafted.

If the SharePoint 2007 farm would provide an empty database with
write access or an Internet connection to SQL Azure, we wouldn't
need separate exporting and importing tools. But creating an
extra database in a legacy environment or enable Internet access
from the server isn't always straightforward. Unfortunately
LocalDB isn't supported with the .NET 3.5 framework. Hence the
introduction of the intermediate XML model.

The libs folder enables compiling the solution on a machine
without SharePoint 2007 installed. Running the exporter on a
SharePoint server, .NET will load SharePoint server assembly from
the Global Assembly Cache.

## Supported platforms

SharePoint 2007 for export, LocalDB or SQL Server, SQL Azure for
import, Visual Studio 2015 for building.