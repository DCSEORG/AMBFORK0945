#!/bin/bash
# deploy-with-chat.sh - Full deployment including GenAI resources
# Deploys: App Service, Managed Identity, Azure SQL Database, Azure OpenAI, AI Search
#
# Prerequisites:
#   - Azure CLI installed and logged in (az login)
#   - Subscription context set (az account set --subscription <sub-id>)
#
# Usage:
#   chmod +x deploy-with-chat.sh
#   ./deploy-with-chat.sh

set -e

echo "========================================"
echo "Expense Management System Deployment"
echo "(with GenAI Chat capabilities)"
echo "========================================"
echo ""

# Configuration - modify these as needed
RESOURCE_GROUP="rg-expensemgmt-demo"
LOCATION="uksouth"
BASE_NAME="expensemgmt"

# Get current user info for SQL Admin
echo "Getting current user information..."
ADMIN_LOGIN=$(az ad signed-in-user show --query userPrincipalName -o tsv)
ADMIN_OBJECT_ID=$(az ad signed-in-user show --query id -o tsv)

echo "Deploying as: $ADMIN_LOGIN"
echo "Object ID: $ADMIN_OBJECT_ID"
echo ""

# Create resource group if it doesn't exist
echo "Step 1/9: Creating resource group..."
az group create --name $RESOURCE_GROUP --location $LOCATION --output none
echo "✓ Resource group created: $RESOURCE_GROUP"

# Deploy infrastructure with GenAI
echo ""
echo "Step 2/9: Deploying infrastructure (App Service, SQL, Managed Identity, GenAI)..."
DEPLOYMENT_OUTPUT=$(az deployment group create \
    --resource-group $RESOURCE_GROUP \
    --template-file infrastructure/main.bicep \
    --parameters baseName=$BASE_NAME \
    --parameters location=$LOCATION \
    --parameters adminObjectId=$ADMIN_OBJECT_ID \
    --parameters adminLogin=$ADMIN_LOGIN \
    --parameters deployGenAI=true \
    --query properties.outputs -o json)

echo "✓ Infrastructure deployed"

# Extract outputs
APP_SERVICE_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.appServiceName.value')
APP_SERVICE_URL=$(echo $DEPLOYMENT_OUTPUT | jq -r '.appServiceUrl.value')
MANAGED_IDENTITY_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.managedIdentityName.value')
MANAGED_IDENTITY_CLIENT_ID=$(echo $DEPLOYMENT_OUTPUT | jq -r '.managedIdentityClientId.value')
SQL_SERVER_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.sqlServerName.value')
SQL_SERVER_FQDN=$(echo $DEPLOYMENT_OUTPUT | jq -r '.sqlServerFqdn.value')
DATABASE_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.databaseName.value')
OPENAI_ENDPOINT=$(echo $DEPLOYMENT_OUTPUT | jq -r '.openAIEndpoint.value')
OPENAI_MODEL_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.openAIModelName.value')
SEARCH_ENDPOINT=$(echo $DEPLOYMENT_OUTPUT | jq -r '.searchEndpoint.value')

echo ""
echo "Deployment outputs:"
echo "  App Service: $APP_SERVICE_NAME"
echo "  App URL: $APP_SERVICE_URL"
echo "  Managed Identity: $MANAGED_IDENTITY_NAME"
echo "  SQL Server: $SQL_SERVER_FQDN"
echo "  Database: $DATABASE_NAME"
echo "  OpenAI Endpoint: $OPENAI_ENDPOINT"
echo "  OpenAI Model: $OPENAI_MODEL_NAME"
echo "  Search Endpoint: $SEARCH_ENDPOINT"

# Configure App Service connection string
echo ""
echo "Step 3/9: Configuring App Service settings..."
CONNECTION_STRING="Server=tcp:${SQL_SERVER_FQDN},1433;Database=${DATABASE_NAME};Authentication=Active Directory Managed Identity;User Id=${MANAGED_IDENTITY_CLIENT_ID};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

az webapp config connection-string set \
    --name $APP_SERVICE_NAME \
    --resource-group $RESOURCE_GROUP \
    --connection-string-type SQLAzure \
    --settings DefaultConnection="$CONNECTION_STRING" \
    --output none

# Configure GenAI settings
az webapp config appsettings set \
    --name $APP_SERVICE_NAME \
    --resource-group $RESOURCE_GROUP \
    --settings \
        "OpenAI__Endpoint=$OPENAI_ENDPOINT" \
        "OpenAI__DeploymentName=$OPENAI_MODEL_NAME" \
        "Search__Endpoint=$SEARCH_ENDPOINT" \
        "ManagedIdentityClientId=$MANAGED_IDENTITY_CLIENT_ID" \
        "AZURE_CLIENT_ID=$MANAGED_IDENTITY_CLIENT_ID" \
    --output none

echo "✓ App Service settings configured"

# Wait for SQL Server to be ready
echo ""
echo "Step 4/9: Waiting 30 seconds for SQL Server to be fully ready..."
sleep 30
echo "✓ Wait complete"

# Add current user's IP to SQL firewall
echo ""
echo "Step 5/9: Adding your IP to SQL Server firewall..."
MY_IP=$(curl -s https://api.ipify.org)
az sql server firewall-rule create \
    --resource-group $RESOURCE_GROUP \
    --server $SQL_SERVER_NAME \
    --name "AllowMyIP" \
    --start-ip-address $MY_IP \
    --end-ip-address $MY_IP \
    --output none
echo "✓ Firewall rule added for IP: $MY_IP"

# Install Python dependencies
echo ""
echo "Step 6/9: Installing Python dependencies..."
pip3 install --quiet pyodbc azure-identity
echo "✓ Python dependencies installed"

# Update Python scripts with actual values
echo ""
echo "Step 7/9: Running database scripts..."

# Update run-sql.py with actual values
sed -i.bak "s/example.database.windows.net/$SQL_SERVER_FQDN/g" run-sql.py && rm -f run-sql.py.bak
sed -i.bak "s/database_name/$DATABASE_NAME/g" run-sql.py && rm -f run-sql.py.bak

# Run schema import
echo "  Importing database schema..."
python3 run-sql.py

# Update and run db role script
sed -i.bak "s/example.database.windows.net/$SQL_SERVER_FQDN/g" run-sql-dbrole.py && rm -f run-sql-dbrole.py.bak
sed -i.bak "s/database_name/$DATABASE_NAME/g" run-sql-dbrole.py && rm -f run-sql-dbrole.py.bak
sed -i.bak "s/MANAGED-IDENTITY-NAME/$MANAGED_IDENTITY_NAME/g" script.sql && rm -f script.sql.bak

echo "  Configuring managed identity database access..."
python3 run-sql-dbrole.py

# Update and run stored procedures script
sed -i.bak "s/example.database.windows.net/$SQL_SERVER_FQDN/g" run-sql-stored-procs.py && rm -f run-sql-stored-procs.py.bak
sed -i.bak "s/database_name/$DATABASE_NAME/g" run-sql-stored-procs.py && rm -f run-sql-stored-procs.py.bak

echo "  Creating stored procedures..."
python3 run-sql-stored-procs.py

echo "✓ Database configured"

# Deploy application code
echo ""
echo "Step 8/9: Deploying application code..."
az webapp deploy \
    --resource-group $RESOURCE_GROUP \
    --name $APP_SERVICE_NAME \
    --src-path ./app.zip \
    --type zip
echo "✓ Application deployed"

# Deploy Chat UI (if separate)
echo ""
echo "Step 9/9: Chat UI is integrated with the main app"
echo "✓ Chat UI ready"

echo ""
echo "========================================"
echo "Deployment Complete!"
echo "========================================"
echo ""
echo "Access your application at:"
echo "  Main App: $APP_SERVICE_URL/Index"
echo "  Chat UI: $APP_SERVICE_URL/Chat"
echo "  API Docs: $APP_SERVICE_URL/swagger"
echo ""
echo "The Chat UI is now connected to Azure OpenAI and can"
echo "interact with your expense data using natural language!"
echo ""
