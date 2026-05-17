// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AgentGovernance.Audit;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using System.Text;
using System.Text.Json;

namespace AgentGovernance.Examples.AuditBlobTelemetry;

internal sealed class BlobAuditSink : IDisposable
{
    private readonly AppendBlobClient _appendBlobClient;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    public BlobAuditSink(string storageAccountUri, AzureCliCredential credential, string containerName, string blobName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageAccountUri);
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobName);

        var serviceClient = new BlobServiceClient(new Uri(storageAccountUri), credential);
        var containerClient = serviceClient.GetBlobContainerClient(containerName);
        containerClient.CreateIfNotExists();
        _appendBlobClient = containerClient.GetAppendBlobClient(blobName);
    }

    public void Append(GovernanceEvent governanceEvent)
    {
        ArgumentNullException.ThrowIfNull(governanceEvent);

        var record = new GovernanceAuditRecord(
            governanceEvent.EventId,
            governanceEvent.Timestamp,
            governanceEvent.Type.ToString(),
            governanceEvent.AgentId,
            governanceEvent.SessionId,
            governanceEvent.PolicyName,
            new Dictionary<string, object>(governanceEvent.Data));

        var payload = JsonSerializer.Serialize(record) + Environment.NewLine;
        var bytes = Encoding.UTF8.GetBytes(payload);

        _gate.Wait();
        try
        {
            EnsureBlobExists();
            using var stream = new MemoryStream(bytes, writable: false);
            _appendBlobClient.AppendBlock(stream);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void EnsureBlobExists()
    {
        if (_initialized)
        {
            return;
        }

        _appendBlobClient.CreateIfNotExists();
        _initialized = true;
    }

    public void Dispose()
    {
        _gate.Dispose();
    }

    private sealed record GovernanceAuditRecord(
        string EventId,
        DateTimeOffset Timestamp,
        string Type,
        string AgentId,
        string SessionId,
        string? PolicyName,
        IReadOnlyDictionary<string, object> Data);
}
