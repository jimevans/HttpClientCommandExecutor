using System;
using System.Diagnostics;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Remote;

namespace CommandExecutorTester
{
    class Program
    {
        static void Main(string[] args)
        {
            // Modify this value to toggle between using the HttpClient based
            // command executor and the standard one.
            bool useHttpClient = true;

            // Modify this line to the location of your local chromedriver.exe
            string servicePath = @"C:\Path\To\Executables";
            ChromeDriverService service = ChromeDriverService.CreateDefaultService(servicePath);
            DriverServiceCommandExecutor executor;
            if (useHttpClient)
            {
                HttpCommandExecutor httpExecutor = CreateHttpCommandExecutor(service.ServiceUrl, useHttpClient);

                // Note: This requires a to-be-released version of Selenium. If you
                // need to use this with a prior version, you'll need to create the
                // command executor using one of the existing constructor overloads
                // and update the internalExecutor field using reflection. Additionally,
                // this step is not necessary for remote execution, as there is already
                // a RemoteWebDriver constructor overload that takes an ICommandExecutor
                // argument.
                // executor = new DriverServiceCommandExecutor(service, httpExecutor);
                executor = new DriverServiceCommandExecutor(service, TimeSpan.FromSeconds(60));
                HttpClientCommandExecutor.ReflectionHelper.SetFieldValue<HttpCommandExecutor>(executor, "internalExecutor", httpExecutor);
            }
            else
            {
                executor = new DriverServiceCommandExecutor(service, TimeSpan.FromSeconds(60));
            }

            bool continueExecution = true;
            int executionCounter = 0;
            ChromeOptions options = new ChromeOptions();
            IWebDriver driver = new RemoteWebDriver(executor, options.ToCapabilities());
            string url = "http://webdriver-herald.herokuapp.com/selenium/current";
            driver.Url = url;
            IWebElement element = driver.FindElement(By.CssSelector("h1"));
            Console.WriteLine("Disconnected sockets before execution: {0}", GetDisconnectedSocketCount());
            string consoleStatusMessage = "Performing execution number: ";
            Console.Write(consoleStatusMessage);
            while (continueExecution)
            {
                executionCounter++;
                Console.CursorLeft = consoleStatusMessage.Length;
                Console.Write(executionCounter);
                try
                {
                    bool enabled = element.Enabled;
                    continueExecution = executionCounter < 1000;
                }
                catch (Exception)
                {
                }
            }

            driver.Quit();
            Console.WriteLine();
            Console.WriteLine("Disconnected sockets after execution: {0}", GetDisconnectedSocketCount());
            Console.WriteLine("Browser closing. Press <Enter> to quit.");
            Console.ReadLine();
        }

        static HttpCommandExecutor CreateHttpCommandExecutor(Uri remoteServerUri, bool useHttpClientExecutor)
        {
            HttpCommandExecutor httpExecutor;
            if (useHttpClientExecutor)
            {
                httpExecutor = new HttpClientCommandExecutor.HttpClientCommandExecutor(remoteServerUri, TimeSpan.FromSeconds(60));
            }
            else
            {
                httpExecutor = new HttpCommandExecutor(remoteServerUri, TimeSpan.FromSeconds(60));
            }

            return httpExecutor;
        }

        static int GetDisconnectedSocketCount()
        {
            int waitingSockets = 0;
            Process netstatProcess = new Process();
            netstatProcess.StartInfo.FileName = "netstat";
            netstatProcess.StartInfo.Arguments = "-nao";
            netstatProcess.StartInfo.RedirectStandardOutput = true;
            netstatProcess.Start();
            string netstatOutput = netstatProcess.StandardOutput.ReadToEnd();
            netstatProcess.WaitForExit();

            string[] lines = netstatOutput.Split('\n');
            foreach (string line in lines)
            {
                if (line.Contains("TIME_WAIT"))
                {
                    waitingSockets++;
                }
            }

            return waitingSockets;
        }
    }
}
