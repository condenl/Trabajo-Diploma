# Exercises only a NEW, separately marked rehearsal database and retains it for inspection.
param([string]$ServerInstance='.')
$ErrorActionPreference='Stop'
$scriptEnsayo=Join-Path $PSScriptRoot 'preparar-ensayo-demo.ps1'
$salida=& powershell -NoProfile -File $scriptEnsayo -ServerInstance $ServerInstance
if($LASTEXITCODE -ne 0){throw 'No se pudo preparar la copia de prueba.'}
$archivo=@($salida|Where-Object {$_ -like 'SESION=*'})[-1].Substring(7)
$s=Get-Content -LiteralPath $archivo -Raw -Encoding UTF8|ConvertFrom-Json
if($s.Base -cne ('Perfumeria_Ensayo_'+$s.Id) -or $s.Id -notmatch '^[a-f0-9]{32}$'){throw 'Destino de prueba invalido.'}
$b=New-Object Data.SqlClient.SqlConnectionStringBuilder
$b['Data Source']=$s.Instancia;$b['Initial Catalog']=$s.Base;$b['Integrated Security']=$true
function Valor([string]$sql){
    $cn=New-Object Data.SqlClient.SqlConnection($b.ConnectionString)
    try{$cn.Open();$cmd=$cn.CreateCommand();$cmd.CommandText=$sql;return $cmd.ExecuteScalar()}finally{$cn.Dispose()}
}
$precio=Valor "SELECT PrecioVenta FROM Producto WHERE Codigo='DEMO-FLORAL'"
[void](Valor "UPDATE Producto SET PrecioVenta=PrecioVenta+123 WHERE Codigo='DEMO-FLORAL'")
if((Valor "SELECT PrecioVenta FROM Producto WHERE Codigo='DEMO-FLORAL'") -eq $precio){throw 'No se produjo el cambio de prueba.'}
for($i=0;$i -lt 2;$i++){
    & powershell -NoProfile -File $scriptEnsayo -Modo Reiniciar -Sesion $archivo
    if($LASTEXITCODE -ne 0 -or (Valor "SELECT PrecioVenta FROM Producto WHERE Codigo='DEMO-FLORAL'") -ne $precio){throw 'El punto inicial no se recupero correctamente.'}
}
$falso=Join-Path $env:TEMP ('ensayo-destino-invalido-'+[guid]::NewGuid().ToString('N')+'.json')
$s.Base='Trabajo Practico Final 3';$s|ConvertTo-Json|Set-Content -LiteralPath $falso -Encoding UTF8
$p=Start-Process powershell -WindowStyle Hidden -ArgumentList @('-NoProfile','-File',('"'+$scriptEnsayo+'"'),'-Modo','Reiniciar','-Sesion',('"'+$falso+'"')) -Wait -PassThru -RedirectStandardOutput ($falso+'.out') -RedirectStandardError ($falso+'.err')
if($p.ExitCode -eq 0 -or -not (Get-Content ($falso+'.err') -Raw).Contains('base de ensayo valida')){throw 'No se rechazo el destino ajeno antes de restaurar.'}
Write-Output 'PASS: copia independiente, cambio de prueba, restauracion inicial, segundo reinicio y rechazo de destino ajeno.'
Write-Output "SESION=$archivo"
