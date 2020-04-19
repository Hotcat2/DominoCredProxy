using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LotusHttpCredProxy.Service.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace LotusHttpCredProxy.Service
{
	public class LotusCredProxyService : HttpLogger<LotusCredProxyService>
	{
		public async Task Start(IPAddress addressToBind, int port, CancellationToken ctx)
		{
			var tcpListener = new TcpListener(addressToBind, port);
			tcpListener.Start();
			ctx.Register(() => tcpListener.Stop());

			var clientTasks = new List<Task>();
			long addedClients = 0;
			
			while (!ctx.IsCancellationRequested)
			{
				var client = await tcpListener.AcceptTcpClientAsync();
				var clientTask = ProcessTcpClient(client, ctx);

				clientTasks.Add(clientTask);
				addedClients++;

				if (addedClients > 100) // Clear completed client tasks after each 100 acceptances.
				{
					addedClients = 0;
					clientTasks.RemoveAll(t => !t.IsCompleted);
				}
			}
		}

		private async Task ProcessTcpClient(TcpClient client, CancellationToken ctx)
		{
			var remoteIp = client.Client.RemoteEndPoint as IPEndPoint;

			try
			{
				Log(LogLevel.Trace, $"Connected: [{remoteIp}]");

				client.ReceiveTimeout = 30000;
				client.SendTimeout = 30000;

				var stream = client.GetStream();
				var streamHelper = new HttpStreamHelper(stream);
				var headers = await streamHelper.GetRequestHeaders(ctx);

				if (headers.Length < 1)
				{
					await SimpleHttpResponse(stream, HttpStatusCode.BadRequest, "There is no HTTP request headers.", ctx);
					return;
				}

				var mainHeader = headers.First();
				headers = headers.Skip(1).ToArray();

				// ^^^ Тут паралельно-асинхронно открываем соединение с домино.

				var lotusAuth = await GetLotusAuthorization(headers, stream, ctx);
				
				if (string.IsNullOrEmpty(lotusAuth))
					return;

				
			}
			catch (Exception ex)
			{
				Log(LogLevel.Error, " Can't process HTTP connection [" + remoteIp + "]: {0}", ex);
			}
			finally
			{
				client.Close();
			}
		}

		/// <summary> Authorizing on Lotus Domino based on JWT claims. </summary>
		private async Task<string> GetLotusAuthorization(IEnumerable<HttpHeader> headers, Stream stream, CancellationToken ctx)
		{
			// ^^^ Надо это как-то кэшировать.

			var individualId = await GetIndividualId(headers, stream, ctx);
			if (string.IsNullOrEmpty(individualId))
				return null;

			return null; // ^^^ Дальше слазить в Vault и получить лотусовые имя и пароль.
		}

		/// <summary> Get IndividualID from JWT claim. </summary>
		private async Task<string> GetIndividualId(IEnumerable<HttpHeader> headers, Stream stream, CancellationToken ctx)
		{
			var authHeader = headers.FirstOrDefault(h => h.Name.Equals(HeaderNames.Authorization, StringComparison.InvariantCultureIgnoreCase));

			if (authHeader == null)
			{
				await SimpleHttpResponse(stream, HttpStatusCode.BadRequest, $"Header {HeaderNames.Authorization} is absent.", ctx);
				return null;
			}

			var value = authHeader.Value;
			const string bearerPrefix = "basic"; // ^^^ "bearer";
			if (value.Substring(0, bearerPrefix.Length + 1).Equals(bearerPrefix + ' ', StringComparison.InvariantCultureIgnoreCase))
				return JwtHelper.GetClaim("IndividualID", "^^^", value.Substring(bearerPrefix.Length + 1));

			await SimpleHttpResponse(stream, HttpStatusCode.BadRequest, $"Header {HeaderNames.Authorization} is not a " + bearerPrefix, ctx);
			return null;
		}

		private static async Task SimpleHttpResponse(Stream stream, HttpStatusCode statusCode, string body, CancellationToken ctx)
		{
			var newLine = "\r\n";
			var bodyBytes = Encoding.UTF8.GetBytes(body ?? "");

			var res = $"HTTP/1.1 {(int)statusCode} {statusCode}{newLine}"
				+ $"{HeaderNames.ContentType}: text/plain; charset=utf-8{newLine}"
				+ $"{HeaderNames.ContentLength}: {bodyBytes.Length}{newLine}"
				+ newLine;

			await stream.WriteAsync(Encoding.ASCII.GetBytes(res), ctx);
			await stream.WriteAsync(bodyBytes, ctx);
		}
	}
}