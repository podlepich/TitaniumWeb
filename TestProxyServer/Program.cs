using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;

namespace TestProxyServer
{
    class Program
    {
        static void Main(string[] args)
        {
            ProxyServer proxyServer = new ProxyServer();
            proxyServer.CertificateManager.RootCertificate = new X509Certificate2(Path.Combine(Environment.CurrentDirectory, "rootCert.pfx"), string.Empty, X509KeyStorageFlags.Exportable);
            proxyServer.CertificateManager.TrustRootCertificate(true);            

            proxyServer.BeforeRequest += OnRequest;
            proxyServer.BeforeResponse += OnResponse;
            proxyServer.ServerCertificateValidationCallback += OnCertificateValidation;
            proxyServer.ClientCertificateSelectionCallback += OnCertificateSelection;

            var explicitEndPoint = new ExplicitProxyEndPoint(IPAddress.Any.MapToIPv4(), 8000, true)
            {
                //Exclude Https addresses you don't want to proxy
                //Usefull for clients that use certificate pinning
                //for example dropbox.com
                // ExcludedHttpsHostNameRegex = new List<string>() { "google.com", "dropbox.com" }

                //Use self-issued generic certificate on all https requests
                //Optimizes performance by not creating a certificate for each https-enabled domain
                //Usefull when certificate trust is not requiered by proxy clients
                // GenericCertificate = new X509Certificate2(Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "genericcert.pfx"), "password")                
            };

            //explicitEndPoint.BeforeTunnelConnectRequest += OnBeforeTunnelConnectRequest;
            //explicitEndPoint.BeforeTunnelConnectResponse += OnBeforeTunnelConnectResponse;
        
            proxyServer.AddEndPoint(explicitEndPoint);
            proxyServer.Start();
            proxyServer.SetAsSystemHttpProxy(explicitEndPoint);
            proxyServer.SetAsSystemHttpsProxy(explicitEndPoint);

            foreach (var endPoint in proxyServer.ProxyEndPoints)
                Console.WriteLine("Listening on '{0}' endpoint at Ip {1} and port: {2} ",endPoint.GetType().Name, endPoint.IpAddress, endPoint.Port);

            Console.Read();

            //Unsubscribe & Quit
            explicitEndPoint.BeforeTunnelConnectRequest -= OnBeforeTunnelConnectRequest;
            explicitEndPoint.BeforeTunnelConnectResponse -= OnBeforeTunnelConnectResponse;
            proxyServer.BeforeRequest -= OnRequest;
            proxyServer.BeforeResponse -= OnResponse;
            proxyServer.ServerCertificateValidationCallback -= OnCertificateValidation;
            proxyServer.ClientCertificateSelectionCallback -= OnCertificateSelection;
            proxyServer.Stop();
        }

        static private async Task OnBeforeTunnelConnectRequest(object sender, TunnelConnectSessionEventArgs e)
        {
            string hostname = e.HttpClient.Request.RequestUri.Host;

            Console.WriteLine(DateTime.Now + "Tunnel to request : " + e.WebSession.Request.Url);

            if (e.WebSession.Request.RequestUri.AbsoluteUri.Contains("hayneedle.com"))
            {
                lock (sender)
                {
                    if (e.HttpClient.Response != null)
                    {
                        Console.WriteLine("success");
                    }
                }
            }
        }

        static private async Task OnBeforeTunnelConnectResponse(object sender, TunnelConnectSessionEventArgs e)
        {
            string hostname = e.HttpClient.Request.RequestUri.Host;

            Console.WriteLine(DateTime.Now + "Tunnel to response : " + e.WebSession.Request.Url);
        }

        static private async Task OnRequest(object sender, SessionEventArgs e)
        {
            Console.WriteLine(DateTime.Now + "Request : " + e.WebSession.Request.Url);

            if (e.WebSession.Request.RequestUri.AbsoluteUri.Contains("hayneedle.com"))
            {              
                lock (e.ClientEndPoint)
                {
                    //var b = e.GetResponseBody();

                    using (var client = new WebClient())
                    {
                        var responseString = client.DownloadString("https://www.hayneedle.com/product/thepioneerwomantimelessbeautyglasscakestand.cfm");
                    }
                }
            }

            ////read request headers
            var requestHeaders = e.WebSession.Request.Headers;

            /*var method = e.WebSession.Request.Method.ToUpper();
            if ((method == "POST" || method == "PUT" || method == "PATCH"))
            {
                //Get/Set request body bytes
                byte[] bodyBytes = await e.GetRequestBody();
                e.SetRequestBody(bodyBytes);

                //Get/Set request body as string
                string bodyString = await e.GetRequestBodyAsString();
                e.SetRequestBodyString(bodyString);

            }*/

            Debug.WriteLine(e.IsTransparent);

            //To cancel a request with a custom HTML content
            //Filter URL
            /*if (e.WebSession.Request.RequestUri.AbsoluteUri.Contains("google.com"))
            {
                 e.Ok("<!DOCTYPE html>" +
                      "<html><body><h1>" +
                      "Website Blocked" +
                      "</h1>" +
                      "<p>Blocked by titanium web proxy.</p>" +
                      "</body>" +
                      "</html>");
            }
            //Redirect example
            if (e.WebSession.Request.RequestUri.AbsoluteUri.Contains("wikipedia.org"))
            {
                 e.Redirect("https://www.paypal.com");
            }*/
        }

        static private async Task OnResponse(object sender, SessionEventArgs e)
        {
            Console.WriteLine(DateTime.Now + "Response : " + e.WebSession.Request.Url);

            //read response headers
            var responseHeaders = e.WebSession.Response.Headers;
            //e.WebSession.Response.ResponseHeaders.Remove("Content-Security-Policy");

            //if (!e.ProxySession.Request.Host.Equals("medeczane.sgk.gov.tr")) return;
            if (e.WebSession.Request.Method == "GET" || e.WebSession.Request.Method == "POST")
            {
                /*if (e.WebSession.Response.StatusCode == 200)
                {
                    if (e.WebSession.Response.ContentType != null && e.WebSession.Response.ContentType.Trim().ToLower().Contains("text/html"))
                    {
                        byte[] bodyBytes = await e.GetResponseBody();
                        e.SetResponseBody(bodyBytes);

                        string body = await e.GetResponseBodyAsString();
                        body = body.Replace("<title>", "<title>xxxxxxxxxxxxxxxxxxxxxxxxxx");

                        e.SetResponseBodyString(body);
                    }
                }*/
            }
        }
        /// Allows overriding default certificate validation logic
        static private Task OnCertificateValidation(object sender, CertificateValidationEventArgs e)
        {
            //set IsValid to true/false based on Certificate Errors
            if (e.SslPolicyErrors == System.Net.Security.SslPolicyErrors.None)
                e.IsValid = true;

            return Task.FromResult(0);
        }

        /// Allows overriding default client certificate selection logic during mutual authentication
        static private Task OnCertificateSelection(object sender, CertificateSelectionEventArgs e)
        {
            //set e.clientCertificate to override
            return Task.FromResult(0);
        }
    }
}
