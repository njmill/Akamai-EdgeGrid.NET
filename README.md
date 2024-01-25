# EdgeGridSigner (for .NET/c#)

This library assists in the interaction with Akamai's {Open} API found at http://developer.akamai.com. 
The API Spec can be found at: https://developer.akamai.com/api

## Project organization
* /EdgeGridAuth - core auth signere project; also contains new EdgeGridAuthV2 class for updated version
* /EdgeGridAuthTest - MSTest unit tests
* /OpenAPI - generic openapi.exe tool to demonstrate using the signer
* /EdgeGridV2_Example - example application for the new EdgeGridAuthV2 class
* /Akamai.EdgeGrid.Auth.sln - root VisualStudio solution

## Install
* Open the Akamai.EdgeGrid.Auth.sln in Visual Studio; Rebuild All
* OR ```MSBuild.exe Akamai.EdgeGrid.Auth.sln /t:rebuild```
* Copy the Akamai.EdgeGrid.Auth.dll to your application or solution. 

## Getting Started
* Create an instance of `EdgeGridV1Signer` or `EdgeGridV2Signer` and call either Sign (if you are managing the http communication yourself
* or call Execute() to utilize built in safety checks

## EdgeGridV2 Example:
```
using Akamai.EdgeGrid.AuthV2;

// Create an instance of EdgeGridV2Signer
EdgeGridV2Signer signer = new EdgeGridV2Signer();

// Create a ClientCredential object from a file
ClientCredential credential = ClientCredential("path_to_your_edgerc.txt_file");

// API Endoint
var endPoint = "/papi/v1/contracts";

// Create a HttpRequestMessage object
HttpRequestMessage request = new HttpRequestMessage
{
    Method = HttpMethod.Get,
    RequestUri = new Uri("https://" + credential.Host + endPoint)
};

// Call the Execute method
Stream responseStreamTask = await signer.Execute(request, credential);

// Read the response
using (StreamReader reader = new StreamReader(responseStream2))
{
    string response = await reader.ReadToEndAsync();
    Console.WriteLine(response);
}


```

## EdgeGridV1 Example:
```
using Akamai.EdgeGrid.Auth;

var signer = new EdgeGridV1Signer();
var credential = new ClientCredential(clientToken, accessToken, secret);

//TODO: create httpRequest via WebRequest.Create(uri);
signer.Sign(httpRequest, credential);
```

alternatively, you can use the execute() method to manage the connection and perform verification checks
```
using Akamai.EdgeGrid.Auth;

var signer = new EdgeGridV1Signer();
var credential = new ClientCredential(clientToken, accessToken, secret); 

//TODO: create httpRequest via WebRequest.Create(uri);
signer.Execute(httpRequest, credential);
```


## Sample application (openapi.exe)
* A sample application has been created that can take command line parameters.

```openapi.exe
-a akab-access1234
-c akab-client1234 
-s secret1234
https://akab-url123.luna.akamaiapis.net/diagnostic-tools/v1/locations
```

