# Azure Services Architecture

This diagram shows the Azure services created by the deployment scripts and how they connect.

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                              Azure Resource Group                                │
│                            (rg-expensemgmt-demo)                                │
│                                                                                  │
│  ┌─────────────────────────────────────────────────────────────────────────┐   │
│  │                          User Assigned                                   │   │
│  │                       Managed Identity                                   │   │
│  │                    (mid-expensemgmt-xxxx)                               │   │
│  │                                                                          │   │
│  │   Used by App Service to authenticate to SQL Database and Azure OpenAI  │   │
│  └───────────────────────────────┬─────────────────────────────────────────┘   │
│                                  │                                              │
│          ┌───────────────────────┼───────────────────────┐                     │
│          │                       │                       │                     │
│          ▼                       ▼                       ▼                     │
│  ┌───────────────────┐  ┌───────────────────┐  ┌───────────────────┐          │
│  │                   │  │                   │  │                   │          │
│  │   App Service     │  │   Azure SQL       │  │  Azure OpenAI     │          │
│  │   (Linux S1)      │  │   Database        │  │  (Sweden Central) │          │
│  │                   │  │                   │  │                   │          │
│  │  ┌─────────────┐  │  │  ┌─────────────┐  │  │  ┌─────────────┐  │          │
│  │  │ ASP.NET 8.0 │  │  │  │  Northwind  │  │  │  │   GPT-4o    │  │          │
│  │  │ Razor Pages │  │  │  │  Database   │  │  │  │   Model     │  │          │
│  │  │ + APIs      │  │  │  │             │  │  │  │             │  │          │
│  │  └─────────────┘  │  │  └─────────────┘  │  │  └─────────────┘  │          │
│  │                   │  │                   │  │                   │          │
│  │  URL: /Index      │  │  Entra ID Auth    │  │  Function Calling │          │
│  │       /Chat       │  │  Only             │  │  for DB Queries   │          │
│  │       /swagger    │  │                   │  │                   │          │
│  └────────┬──────────┘  └────────▲──────────┘  └────────▲──────────┘          │
│           │                      │                      │                      │
│           │    SQL Connection    │   Chat Completions   │                      │
│           │    (Stored Procs)    │   (Managed Identity) │                      │
│           └──────────────────────┴──────────────────────┘                      │
│                                                                                  │
│  ┌─────────────────────────────────────────────────────────────────────────┐   │
│  │                         AI Search (Optional)                             │   │
│  │                      (search-expensemgmt-xxxx)                           │   │
│  │                                                                          │   │
│  │          Used for RAG pattern to search contextual documents             │   │
│  └─────────────────────────────────────────────────────────────────────────┘   │
│                                                                                  │
└─────────────────────────────────────────────────────────────────────────────────┘

                                    ▲
                                    │
                                    │ HTTPS
                                    │
                              ┌─────┴─────┐
                              │   Users   │
                              │ (Browser) │
                              └───────────┘
```

## Data Flow

1. **User Access**: Users access the web application via HTTPS at the App Service URL
2. **Web Application**: ASP.NET 8.0 Razor Pages serve the UI and expose REST APIs
3. **Database Access**: App uses Managed Identity to connect to Azure SQL (no passwords)
4. **AI Chat**: Chat requests go through Azure OpenAI with function calling to query the database
5. **All Auth**: All service-to-service communication uses the User Assigned Managed Identity

## Deployment Options

| Script | What it deploys |
|--------|-----------------|
| `deploy.sh` | App Service, SQL Database, Managed Identity (Chat uses dummy responses) |
| `deploy-with-chat.sh` | All above + Azure OpenAI + AI Search (Full AI chat capabilities) |

## Security

- **No SQL passwords**: Azure AD-Only authentication enforced
- **No API keys stored**: Managed Identity for Azure OpenAI
- **HTTPS only**: All traffic encrypted
- **Entra ID**: Single identity for all services
