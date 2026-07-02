using Azure.Identity;

namespace AgentGovernance.Examples.AuditBlobTelemetry;

/// <summary>
/// Application configuration loaded from environment variables.
/// </summary>
internal sealed record AppConfiguration(
    string OpenAIEndpoint,
    string DeploymentName,
    string ApplicationInsightsConnectionString,
    string StorageAccountUri,
    string AuditContainerName,
    string? TenantId)
{
    /// <summary>
    /// Load configuration from environment variables.
    /// </summary>
    public static AppConfiguration LoadFromEnvironment()
    {
        var endpoint = GetRequiredEnvironmentVariable("MS_FOUNDRY_ENDPOINT");
        var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT") ?? "gpt-5.4-mini";
        var applicationInsightsConnectionString = GetRequiredEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
        var storageAccountName = GetRequiredEnvironmentVariable("AZURE_STORAGE_ACCOUNT_NAME");
        var storageAccountUri = $"https://{storageAccountName}.blob.core.windows.net/";
        var auditContainerName = Environment.GetEnvironmentVariable("AUDIT_STORAGE_CONTAINER") ?? "agt-audit";
        var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");

        return new AppConfiguration(
            endpoint,
            deploymentName,
            applicationInsightsConnectionString,
            storageAccountUri,
            auditContainerName,
            tenantId);
    }

    /// <summary>
    /// Create Azure credential based on configuration.
    /// </summary>
    public DefaultAzureCredential CreateCredential()
    {
        if (TenantId is not null)
        {
            return new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                TenantId = TenantId,
                ExcludeManagedIdentityCredential = true,   // TODO: This should be enabled on production env
            });
        }

        return new DefaultAzureCredential();
    }

    private static string GetRequiredEnvironmentVariable(string name) =>
        Environment.GetEnvironmentVariable(name)
            ?? throw new InvalidOperationException($"{name} environment variable is not set.");
}
