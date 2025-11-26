using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Configuration;

public static class AwsSecretsLoader
{
    public static async Task<IDictionary<string, string>> LoadSecretsAsync(string secretName, string region)
    {
        var client = new AmazonSecretsManagerClient(Amazon.RegionEndpoint.GetBySystemName(region));

        var request = new GetSecretValueRequest
        {
            SecretId = secretName
        };

        var response = await client.GetSecretValueAsync(request);

        if (string.IsNullOrEmpty(response.SecretString))
            return new Dictionary<string, string>();

        var dictionary = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(response.SecretString);
        return dictionary ?? new Dictionary<string, string>();
    }
}
