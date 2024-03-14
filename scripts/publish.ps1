$additionalParams = $args -join ' '
$command = "func azure functionapp publish fapp-ungerfall-chatgpt-telegram-webhook --show-keys $additionalParams"

cd .\src\presentation.azure-function\
Invoke-Expression $command
cd ../..
