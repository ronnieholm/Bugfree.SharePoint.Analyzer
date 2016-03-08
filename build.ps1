$sqlcmd = "C:\Program Files\Microsoft SQL Server\110\Tools\Binn\sqlcmd.exe"
$base_path = Split-Path -Path $PSCommandPath -Parent
$import_base_path = "$($base_path)\src\Bugfree.SharePoint.Analyzer.Importer"
$new_db_name = "WebApplications"
$mdf_file_path = "$($import_base_path)\$($new_db_name).mdf"
$log_file_path = "$($import_base_path)\$($new_db_name).ldf"
$detachdb_sql_path = "$($import_base_path)\detachdb.sql"
$createdb_sql_path = "$($import_base_path)\createdb.sql"

$detach_db_sql = @"
    USE master;
    GO
    EXEC sp_detach_db @dbname = '$new_db_name';
    GO
"@

$detach_db_sql | Out-File $detachdb_sql_path

if (Test-Path $mdf_file_path) {
    Remove-Item $mdf_file_path
}

if (Test-Path $log_file_path) {
    Remove-Item $log_file_path
}

$create_db_sql = @"
    USE master;
    GO
    CREATE DATABASE $new_db_name
    ON
    (NAME = '$($new_db_name)_dat',
         FILENAME = '$mdf_file_path',
         SIZE = 10MB,
         MAXSIZE = 50MB,
         FILEGROWTH = 5MB)
    LOG ON
    (NAME = '$($new_db_name)_log',
         FILENAME = '$log_file_path',
         SIZE = 5MB,
         MAXSIZE = 25MB,
         FILEGROWTH = 5MB)
    GO

    USE WebApplications;
"@

$schema = Get-Content "$($import_base_path)\schema.sql"
$create_db_sql = $create_db_sql + $schema
$create_db_sql | Out-File $createdb_sql_path
&$sqlcmd -S "(localdb)\v11.0" -i $createdb_sql_path
&$sqlcmd -S "(localdb)\v11.0" -i $detachdb_sql_path

$msbuild = "C:\Program Files (x86)\MSBuild\14.0\bin\amd64\msbuild.exe"
&$msbuild "$($base_path)\src\Bugfree.SharePoint.Analyzer.sln"