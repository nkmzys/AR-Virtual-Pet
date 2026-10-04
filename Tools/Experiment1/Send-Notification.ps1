param(
    [Parameter(Mandatory = $true)] [string] $TargetAddress,
    [ValidateRange(1024, 65535)] [int] $Port = 5055,
    [ValidatePattern('^[A-Z0-9_]{1,64}$')] [string] $Command = 'SHOW_NOTIFICATION',
    [ValidateRange(100, 60000)] [int] $TimeoutMilliseconds = 5000
)
$ErrorActionPreference = 'Stop'
$client = [System.Net.Sockets.TcpClient]::new()
$writer = $null
$reader = $null
$connection = $null
try {
    $connection = $client.BeginConnect($TargetAddress, $Port, $null, $null)
    if (-not $connection.AsyncWaitHandle.WaitOne($TimeoutMilliseconds)) { throw 'TCP connection timed out.' }
    $client.EndConnect($connection)
    $client.ReceiveTimeout = $TimeoutMilliseconds
    $client.SendTimeout = $TimeoutMilliseconds
    $stream = $client.GetStream()
    $writer = [System.IO.StreamWriter]::new($stream, [System.Text.UTF8Encoding]::new($false), 1024, $true)
    $writer.NewLine = "`n"
    $writer.AutoFlush = $true
    $reader = [System.IO.StreamReader]::new($stream, [System.Text.Encoding]::ASCII, $false, 1024, $true)
    $writer.WriteLine($Command)
    $response = $reader.ReadLine()
    if ($response -ne "QUEUED $Command") { throw "Command was not queued: $response" }
    Write-Output $response
} finally {
    if ($null -ne $reader) { $reader.Dispose() }
    if ($null -ne $writer) { $writer.Dispose() }
    if ($null -ne $connection) { $connection.AsyncWaitHandle.Close() }
    $client.Dispose()
}
