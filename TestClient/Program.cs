using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace TestClient
{
	class Program
	{
		static void Main(string[] args)
		{
			new Program().TestClients();
		}

		private void LogInfo(string msg)
			=> Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [T:{Thread.CurrentThread.ManagedThreadId}] {msg}");

		private void TestClients()
		{
			var tasks = new List<Task>();

			for (var n = 0; n < 5; n++)
				tasks.Add(TestOneClient(n));

			Task.WhenAll(tasks).Wait();

			Console.WriteLine("Press Enter.");
			Console.ReadKey();
		}

		private async Task TestOneClient(int clientNum)
		{
			var sw = Stopwatch.StartNew();
			LogInfo($"Started [{clientNum}]");

			var client = new WebClient();
			//var host = $"http://localhost:8087/tc{clientNum}";
			var host = $"http://192.168.210.200:8087/tc{clientNum}";
			var res = await client.DownloadStringTaskAsync(host);

			LogInfo($"Finished [{clientNum}]: {sw.Elapsed}");

			/*
			var request = (HttpWebRequest)WebRequest.Create("localhost:8087/tc");
			request.Method = "GET";

			using var response = request. GetResponse();
			//response.
			*/
		}
	}
}
