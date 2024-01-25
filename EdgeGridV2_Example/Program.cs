using Akamai.EdgeGrid.AuthV2;
using System.Text;

class Program
{
    static async Task Main(string[] args)
    {
        // Create a new instance of EdgeGridV2Signer
        EdgeGridV2Signer signer = new EdgeGridV2Signer();

        // Create local variables for ClientToken, AccessToken, and Secret
        var edgeRc = "edgerc.txt"; // This should be the path to your .edgerc file
        // The API endpoints you want to call
        var endPoint = "/papi/v1/contracts";
        var endPoint2 = "/papi/v1/search/find-by-value";
        // Create a new instance of ClientCredential
        ClientCredential credential = new ClientCredential(edgeRc);

        // Create a new HttpRequestMessage
        HttpRequestMessage request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri("https://" + credential.Host + endPoint)
        };

        // Execute the request
        try
        {
            Stream responseStream = await signer.Execute(request, credential);

            // Read the response
            using (StreamReader reader = new StreamReader(responseStream))
            {
                string response = await reader.ReadToEndAsync();
                Console.WriteLine(response);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }

        // Payload for POST request
        var payload = "{\"propertyName\":\"www.example.com\"}";
        
        HttpRequestMessage postRequest = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri("https://" + credential.Host + endPoint2),
            
        };

        // Execute the request
        try
        {
            Stream responseStream2 = await signer.Execute(postRequest, credential, new MemoryStream(Encoding.UTF8.GetBytes(payload)));

            // Read the response
            using (StreamReader reader = new StreamReader(responseStream2))
            {
                string response = await reader.ReadToEndAsync();
                Console.WriteLine(response);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }

    }
}
