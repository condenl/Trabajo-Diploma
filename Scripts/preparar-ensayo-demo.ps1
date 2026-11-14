# Creates a separate runnable copy and an SQL checkpoint. Never changes UI/App.config.
[CmdletBinding()]
param(
    [ValidateSet('Crear','Reiniciar')][string]$Modo = 'Crear',
    [string]$ServerInstance = '.',
    [string]$Sesion
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

function Conexion([string]$catalogo='master') {
    $b = New-Object Data.SqlClient.SqlConnectionStringBuilder
    $b['Data Source']=$ServerInstance; $b['Initial Catalog']=$catalogo
    $b['Integrated Security']=$true; $b['TrustServerCertificate']=$true
    return New-Object Data.SqlClient.SqlConnection($b.ConnectionString)
}
function Consultar([string]$sql, [hashtable]$valores=@{}) {
    $cn = Conexion
    try {
        $cn.Open(); $cmd=$cn.CreateCommand(); $cmd.CommandText=$sql; $cmd.CommandTimeout=180
        foreach($key in $valores.Keys){[void]$cmd.Parameters.AddWithValue($key,$valores[$key])}
        $dt=New-Object Data.DataTable; $ad=New-Object Data.SqlClient.SqlDataAdapter($cmd); [void]$ad.Fill($dt)
        return ,$dt
    } finally { $cn.Dispose() }
}
function ExigirInstanciaLocal {
    $t=Consultar "SELECT CAST(SERVERPROPERTY('MachineName') AS nvarchar(128)) AS Maquina, CAST(SERVERPROPERTY('IsLocalDB') AS int) AS LocalDB, CAST(SERVERPROPERTY('ProductMajorVersion') AS int) AS Version"
    $r=$t.Rows[0]
    $localDb=$r.LocalDB -eq 1 -and $ServerInstance -match '^\(localdb\)\\[^\\]+$'
    if(-not $localDb -and $r.Maquina -ne [Environment]::MachineName){throw 'El ensayo requiere una instancia SQL local.'}
    if([int]$r.Version -lt 16){throw 'El ensayo requiere SQL Server 16 o superior.'}
}

if($Modo -eq 'Reiniciar') {
    if(-not $Sesion -or -not (Test-Path -LiteralPath $Sesion -PathType Leaf)){throw 'Indique -Sesion con el archivo sesion-demo.json creado por este script.'}
    $estado=Get-Content -LiteralPath $Sesion -Raw -Encoding UTF8 | ConvertFrom-Json
    if($estado.Version -ne 1 -or $estado.Id -notmatch '^[a-f0-9]{32}$' -or $estado.Base -cne ('Perfumeria_Ensayo_'+$estado.Id)) {
        throw 'La sesion no identifica una base de ensayo valida; no se realizo la restauracion.'
    }
    $ServerInstance=$estado.Instancia; ExigirInstanciaLocal
    $db=[string]$estado.Base
    # SQL Server reads its own backup directory; the Windows caller need not have NTFS access there.
    if(Get-Process -Name UI -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq $estado.Aplicacion}){throw 'Cierre la aplicacion de este ensayo antes de reiniciarlo.'}
    $marca=Consultar "SELECT CAST(value AS nvarchar(64)) AS Id FROM [$db].sys.extended_properties WHERE class=0 AND name=N'PerfumeriaEnsayoId'"
    if($marca.Rows.Count -ne 1 -or $marca.Rows[0].Id -cne $estado.Id){throw 'La base no pertenece a esta sesion de ensayo; no se realizo la restauracion.'}
    $cabecera=Consultar 'RESTORE HEADERONLY FROM DISK=@ruta' @{ '@ruta'=[string]$estado.Respaldo }
    if($cabecera.Rows.Count -ne 1 -or $cabecera.Rows[0].DatabaseName -cne $db){throw 'El respaldo no corresponde a esta base de ensayo.'}
    [void](Consultar 'RESTORE VERIFYONLY FROM DISK=@ruta WITH CHECKSUM' @{ '@ruta'=[string]$estado.Respaldo })
    try {
        [void](Consultar "ALTER DATABASE [$db] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [$db] FROM DISK=@ruta WITH REPLACE, RECOVERY;" @{ '@ruta'=[string]$estado.Respaldo })
    } finally { [void](Consultar "ALTER DATABASE [$db] SET MULTI_USER;") }
    Write-Output "Ensayo reiniciado al punto guardado. Base: $db"
    Write-Output "APLICACION=$($estado.Aplicacion)"
    exit 0
}

if($Sesion){throw '-Sesion se utiliza solamente con -Modo Reiniciar.'}
ExigirInstanciaLocal
$id=[guid]::NewGuid().ToString('N'); $db='Perfumeria_Ensayo_'+$id
$stage=Join-Path $env:LOCALAPPDATA ('Perfumeria\Ensayos\'+$id)
$bin=Join-Path $root 'UI\bin\Release'
if(-not (Test-Path (Join-Path $bin 'UI.exe')) -or -not (Test-Path (Join-Path $root 'Installer\bin\SeedDemoActors.exe'))){throw 'Compile primero con Installer/build-installer.ps1.'}
$existe=Consultar 'SELECT DB_ID(@base) AS Id' @{ '@base'=$db }
if($existe.Rows[0].Id -isnot [DBNull]){throw 'La base generada ya existe; no se sobrescribira.'}
New-Item -ItemType Directory -Path $stage | Out-Null
Copy-Item (Join-Path $bin '*.dll') $stage
foreach($name in @('UI.exe','UI.exe.config')){Copy-Item -LiteralPath (Join-Path $bin $name) -Destination $stage}
Copy-Item -LiteralPath (Join-Path $root 'Installer\bin\SeedDemoActors.exe') -Destination $stage
if(Test-Path (Join-Path $root 'Documentacion\Ejemplos')){Copy-Item -LiteralPath (Join-Path $root 'Documentacion\Ejemplos') -Destination (Join-Path $stage 'Ejemplos') -Recurse}
& powershell -NoProfile -File (Join-Path $root 'Installer\InicializarBaseDatos.ps1') `
    -ServerInstance $ServerInstance -DatabaseName $db -BackupPath (Join-Path $root 'SQL\Trabajo Practico Final 3.bak') `
    -MigrationDirectory (Join-Path $root 'SQL') -ConfigPath (Join-Path $stage 'UI.exe.config') -LogPath (Join-Path $stage 'preparacion.log') -SeedDemoActors
if($LASTEXITCODE -ne 0){throw "No se completo el ensayo. Consulte $stage\preparacion.log. La configuracion de desarrollo se conserva."}
$b=New-Object Data.SqlClient.SqlConnectionStringBuilder
$b['Data Source']=$ServerInstance; $b['Initial Catalog']=$db; $b['Integrated Security']=$true
$cn=New-Object Data.SqlClient.SqlConnection($b.ConnectionString)
try {
    $cn.Open();$cmd=$cn.CreateCommand();$cmd.CommandText="EXEC sys.sp_addextendedproperty @name=N'PerfumeriaEnsayoId', @value=@id"
    [void]$cmd.Parameters.AddWithValue('@id',$id);[void]$cmd.ExecuteNonQuery()
} finally {$cn.Dispose()}
$ubicacion=Consultar "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(4000)) AS Ruta"
$carpeta=[string]$ubicacion.Rows[0].Ruta
if(-not $carpeta){
    $archivos=Consultar 'SELECT physical_name AS Ruta FROM sys.master_files WHERE database_id=DB_ID(@base) AND type=0' @{ '@base'=$db }
    if($archivos.Rows.Count -eq 1){$carpeta=Split-Path -Parent ([string]$archivos.Rows[0].Ruta)}
}
if(-not $carpeta){throw 'SQL Server no informa una carpeta para el punto de restauracion.'}
$backup=Join-Path $carpeta ($db+'-inicial.bak')
[void](Consultar "BACKUP DATABASE [$db] TO DISK=@ruta WITH COPY_ONLY, CHECKSUM" @{ '@ruta'=$backup })
[void](Consultar 'RESTORE VERIFYONLY FROM DISK=@ruta WITH CHECKSUM' @{ '@ruta'=$backup })
$estado=[ordered]@{Version=1;Id=$id;Instancia=$ServerInstance;Base=$db;Creada=(Get-Date).ToString('o');Respaldo=$backup;Aplicacion=(Join-Path $stage 'UI.exe')}
$archivo=Join-Path $stage 'sesion-demo.json'; $estado | ConvertTo-Json | Set-Content -LiteralPath $archivo -Encoding UTF8
Write-Output "Ensayo creado con actores y escenarios precargados. Base: $db"
Write-Output "APLICACION=$($estado.Aplicacion)"
Write-Output "SESION=$archivo"
Write-Output 'Para repetir: cierre esta copia y ejecute el script con -Modo Reiniciar -Sesion RUTA_SESION.'
