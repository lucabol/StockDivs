param(
    [string]$EnvironmentName,
    [string]$Location,
    [string]$Composefile = "compose.yaml"
)

$rgname="rg-$EnvironmentName"
$loc=$Location
$env="env-$EnvironmentName"
$logname="log-$EnvironmentName"
$regserver="reg$EnvironmentName"

az group create --name $rgname --location $loc

$regExists=$(az acr check-name --name $regserver --query "nameAvailable" -o tsv)
if($regExists -eq "true") {
    az acr create --resource-group $rgname --name $regserver --sku Basic
    az acr update -n $regserver --admin-enabled true
}
else {
    az acr update --resource-group $rgname --name $regserver --sku Basic
    az acr update -n $regserver --admin-enabled true
}

$regPwd=$(az acr credential show --name $regserver --query "passwords[0].value" -o tsv)
$regUser=$(az acr credential show --name $regserver --query "username" -o tsv)

$workspace = Get-AzOperationalInsightsWorkspace -ResourceGroupName $rgname -Name $logname -ErrorAction SilentlyContinue
if($workspace) {
    $wsId=$(az monitor log-analytics workspace update -g $rgname --workspace-name $logname --query customerId -o tsv)
} else {
    $wsId=$(az monitor log-analytics workspace create -g $rgname --workspace-name $logname --query customerId -o tsv)
}
$wsKey=$(az monitor log-analytics workspace get-shared-keys -g $rgname --workspace-name $logname --query primarySharedKey -o tsv)

az containerapp env create --name $env --resource-group $rgname --location $loc --logs-destination log-analytics --logs-workspace-id $wsId --logs-workspace-key $wsKey

az containerapp compose create --registry-server="$regserver.azurecr.io" --registry-username=$regUser --registry-password=$regPwd --resource-group $rgname --location $loc --environment $env -f compose.yaml

# Process the x-azure properties in the Docker Compose file
# Needs powershell-yaml module
# Install-Module -Name powershell-yaml -AllowPrerelease -Force

$compose = Get-Content $composefile | ConvertFrom-Yaml
$services = $compose.services

foreach($service in $services.GetEnumerator()) {
    $name = $service.Name
    $azure = $service.Value['x-azure']

    if($azure) {
        $replicas = $azure.replicas
        if($replicas) {
            $min = $replicas[0]
            $max = $replicas[1]
            Write-Host "name- Found replicas: $min $max"
            az containerapp update --name $name --resource-group $rgname --min-replicas $min --max-replicas $max
        }
        $ingresstype = $azure['ingress-type']
        if($ingresstype) {
            Write-Host "$name - Found ingress-type: $ingresstype"
            az containerapp ingress update --name $name --resource-group $rgname --transport $ingresstype
        }
    }
    else {
        Write-Host "$name - No Azure properties found."
    }
}

# Print out the endpoint of an application called Web
$endpoint=az containerapp show --name web --resource-group $rgname --query "properties.configuration.ingress.fqdn" -o tsv
write-host "Endpoint: http://$endpoint" -ForegroundColor Blue