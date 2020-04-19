using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace DominoCredentialsProxy.Service.ProxyUtils
{
	public class HttpHeadersReaderFromStream
	{
		private readonly ILoggerFactory _loggerFactory;
		private readonly ILogger _logger;
		private readonly string _remoteAddress;
		private readonly Socket _client;
		private readonly NetworkStream _stream;
		
		private MemoryStream Cache { get; } = new MemoryStream();
		public int CurrentSymbol { get; private set; } = -1;


		public HttpHeadersReaderFromStream(ILoggerFactory loggerFactory, string remoteAddress, Socket client, NetworkStream stream)
		{
			_logger = loggerFactory.CreateLogger(typeof(HttpHeadersReaderFromStream));
			_loggerFactory = loggerFactory;
			_remoteAddress = remoteAddress;
			_client = client;
			_stream = stream;
		}

		internal void LogInfo(string msg, params object[] args)
			=> _logger.LogInformation($"{DateTime.Now:HH:mm:ss.fff} {msg}", args);

		public async IAsyncEnumerable<HttpHeader> HeadersAsync([EnumeratorCancellation] CancellationToken ctx)
		{
			_client.NoDelay = true;
			_client.Blocking = false;

			HttpHeader header = null;

			//var symbolColon = Encoding.ASCII.GetBytes(":").First();
			var symbolsNewline = Encoding.ASCII.GetBytes("\r\n");
			var symbolsSpaceOrTab = Encoding.ASCII.GetBytes("\t" + string.Empty);

			//var isHeader = true;
			var startValue = -1;
			byte prevNewLineChar = 0;
			var headersCompleteChars = 0; // Headers newline bytes finished.
			var headersComplete = false;

			var buffer = new byte[256]; // ^^^ 1024
			var sw = Stopwatch.StartNew();

			do
			{
				int bytesRead;
				sw.Restart();
				if ((bytesRead = await _stream.ReadAsync(buffer, ctx)) <= 0)
					break;
				LogInfo($"Read time [{_remoteAddress}]: {sw.Elapsed}");
				Cache.Write(buffer, 0, bytesRead);

				for (var n = 0; n < bytesRead; n++)
				{
					CurrentSymbol++;
					var ch = buffer[n];

					if (headersCompleteChars > 0) // If header completed.
					{
						if (headersCompleteChars > 1 || !symbolsNewline.Contains(ch)) // If all headers chars finished at all.
						{
							headersComplete = true;
							break;
						}

						headersCompleteChars++;
					}
					else if (ch == prevNewLineChar) // NewLined appeared twice - HTTP header completed.
						headersCompleteChars = 1;

					if (symbolsNewline.Contains(ch)) // Is line ended.
					{
						if (startValue >= 0) // If newline char meets first time.
							header?.AddValuePart(startValue, CurrentSymbol - startValue);
						startValue = -1;
						if (prevNewLineChar == 0)
							prevNewLineChar = ch;
					}
					else if (prevNewLineChar > 0 && symbolsSpaceOrTab.Contains(ch)
					) // If prev char is newline and space or tab appeared.
					{
						startValue = CurrentSymbol;
						prevNewLineChar = 0;
					}
					else if (startValue < 0) // If new header appeared.
					{
						if (header != null)
							yield return header;
						header = new HttpHeader(CurrentSymbol, Cache);

						prevNewLineChar = 0;
						startValue = CurrentSymbol;
					}
				}
			} while (!headersComplete && !ctx.IsCancellationRequested && _stream.DataAvailable);

			yield return header;

			/*
			do
			{
				sw.Restart();
				while (_stream.DataAvailable) // Waiting until data will be available, wait 3ms if not.
				{
					await Task.Delay(1, ctx);
					if (sw.ElapsedMilliseconds > _client.ReceiveTimeout)
						break;
				}

				int bytesRead;
				if ((bytesRead = await _client.ReceiveAsync(mem, SocketFlags.Partial, ctx)) <= 0)
					continue;

				var buffer = mem.ToArray();
				await Cache.WriteAsync(buffer, 0, bytesRead, ctx);

				for (var n = 0; n < bytesRead; n++)
				{
					CurrentSymbol++;
					var ch = buffer[n];

					if (headersCompleteChars > 0) // If header completed.
					{
						if (headersCompleteChars > 1 || !symbolsNewline.Contains(ch)) // If all headers chars finished at all.
						{
							headersComplete = true;
							break;
						}

						headersCompleteChars++;
					}
					else if (ch == prevNewLineChar) // NewLined appeared twice - HTTP header completed.
						headersCompleteChars = 1;

					if (symbolsNewline.Contains(ch)) // Is line ended.
					{
						if (startValue >= 0) // If newline char meets first time.
							header?.AddValuePart(startValue, CurrentSymbol - startValue);
						startValue = -1;
						if (prevNewLineChar == 0)
							prevNewLineChar = ch;
					}
					else if (prevNewLineChar > 0 && symbolsSpaceOrTab.Contains(ch)
					) // If prev char is newline and space or tab appeared.
					{
						startValue = CurrentSymbol;
						prevNewLineChar = 0;
					}
					else if (startValue < 0) // If new header appeared.
					{
						if (header != null)
							yield return header;
						header = new HttpHeader(CurrentSymbol, Cache);

						prevNewLineChar = 0;
						startValue = CurrentSymbol;
					}
				}
			} while (!headersComplete && !ctx.IsCancellationRequested);

			yield return header;
			*/
		}
	}
}