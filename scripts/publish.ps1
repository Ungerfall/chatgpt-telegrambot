$additionalParams = $args -join ' '
$dotnet = "8.0"
$command = "func azure functionapp publish fapp-ungerfall-chatgpt-telegram-webhook --show-keys --dotnet-version $dotnet $additionalParams"

cd .\src\presentation.azure-function\
Invoke-Expression $command
cd ../..
