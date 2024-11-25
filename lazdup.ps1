$rgname="rg-stockdivs"
$loc="eastus2"
$env="env-stockdivs"
$logname="log-stockdivs"

az group create --name $rgname --location $loc
$wsId=$(az monitor log-analytics workspace update -g $rgname --workspace-name $logname --query customerId -o tsv)
$wsKey=$(az monitor log-analytics workspace get-shared-keys -g $rgname --workspace-name $logname --query primarySharedKey -o tsv)

az containerapp env create --name $env --resource-group $rgname --location $loc --logs-destination log-analytics --logs-workspace-id $wsId --logs-workspace-key $wsKey

az containerapp compose create --resource-group $rgname --location $loc --environment $env -f compose.yaml

$endpoint=az containerapp show --name web --resource-group $rgname --query "properties.configuration.ingress.fqdn" -o tsv
write-host "Endpoint: http://$endpoint" -ForegroundColor Green