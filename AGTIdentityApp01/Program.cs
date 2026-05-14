using AgentGovernance.Trust;

var agentIdentity = AgentIdentity.Create(
    name: "DataProcessor",
    sponsor: "alice@company.com",
    capabilities: ["read:data", "write:reports"],
    organization: "Analytics"
);

Console.WriteLine(agentIdentity.Did);                                   // did:mesh:a1b2c3d4e5f6...
Console.WriteLine(Convert.ToBase64String(agentIdentity.PublicKey));     // Base64-encoded public key
Console.WriteLine(agentIdentity.Status);                                // "Active"

// Check the agent's trust score (0-1000, higher = safer)
var trustStore = new FileTrustStore("trust-scores.json");
var score = trustStore.GetScore(agentIdentity.Did);

Console.WriteLine(score);                                       // 500 (default starting score)

// Determine risk level based on score
var riskLevel = score switch{
    >= 700 => "Low", >= 400 => "Medium", _ => "High"
};
Console.WriteLine(riskLevel);                                   // "Medium"
