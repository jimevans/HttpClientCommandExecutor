# HttpClientCommandExecutor
A .NET Selenium command executor based on HttpClient

This project represents a demonstration of a command executor using the
`System.Net.Http.HttpClient` class instead of `HttpWebRequest`/`HttpWebResponse`.
This is to help mitigate the way that the latter abandon ports in .NET Core.
It is provided as an example only, and will not be released as an official
Selenium artifact, either via NuGet, or any other means, since it is likely
that the official .NET language bindings will migrate to using HttpClient
in the 4.x release life cycle.

Please note that there are two locations in the code that will not work with
any currently publicly released version of Selenium. The code used in those
locations require modifications to the Selenium .NET language binding source
code that exist in the HEAD revision, but are not publicly released at this
time. These locations are clearly marked with comments.

Special thanks to [@TFTomSun](https://github.com/TFTomSun) for the initial
code from which this is derived.
