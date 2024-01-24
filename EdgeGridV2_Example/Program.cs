﻿using Akamai.EdgeGrid.AuthV2;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        // Create a new instance of EdgeGridV2Signer
        EdgeGridV2Signer signer = new EdgeGridV2Signer();

        // Create local variables for ClientToken, AccessToken, and Secret
        var edgeRc = "edgerc.txt"; // This should be the path to your .edgerc file
        // The API endpoint you want to call
        var endPoint = "/papi/v1/contracts";
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
    }
}
