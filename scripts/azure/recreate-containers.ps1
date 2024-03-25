$account = "cosmos-ungerfall-chatgpt"
$db = "telegram-bot"
$timedTasksExecutionsContainer = "timedTaskExecutions"
$rg = "rg-chatgpt"

$loginStatus = az login
# if the status is null or empty, the login has failed
if ([string]::IsNullOrEmpty($loginStatus)) {
    echo "Failed to login. Exiting the script."
    exit
}

$timedTaskExecutionsContainerExists = az cosmosdb sql container exists --account-name $account `
                                 --database-name $db `
                                 --name $timedTasksExecutionsContainer `
                                 --resource-group $rg
if ($timedTaskExecutionsContainerExists -eq "true") {
  echo "Deleting the $timedTasksExecutionsContainer container..."
  az cosmosdb sql container delete --account-name $account `
                                 --database-name $db `
                                 --name $timedTasksExecutionsContainer `
                                 --resource-group $rg `
                                 --yes
}

echo "Creating the empty $timedTasksExecutionsContainer container..."
az cosmosdb sql container create --account-name $account `
                                 --database-name $db `
                                 --name $timedTasksExecutionsContainer `
                                 --partition-key-path "/chatId" `
                                 --resource-group $rg `
                                 --query "id" `
                                 --ttl -1 # TTL is enabled, but items won't be automatically deleted unless they have their own TTL set.

