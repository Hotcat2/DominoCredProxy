using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace DominoCredentialsProxy.Service.ProxyUtils
{
	public class HttpHeadersReader
	{
		private readonly NetworkStream _inputStream;
		private MemoryStream Cache { get; } = new MemoryStream();
		public int CurrentSymbol { get; private set; } = -1;

		public HttpHeadersReader(NetworkStream inputStream)
		{
			_inputStream = inputStream;
		}

		/// <summary>
		/// Need to 1 - change URI, 2 - change host, 3 - change authorization.
		/// </summary>
		public IEnumerable<HttpHeader> Headers()
		{
			int bytesRead;
			var buffer = new byte[512];
			//var header = new HttpHeader(0, _cache);
			HttpHeader header = null;

			//var symbolColon = Encoding.ASCII.GetBytes(":").First();
			var symbolsNewline = Encoding.ASCII.GetBytes("\r\n");
			var symbolsSpaceOrTab = Encoding.ASCII.GetBytes("\t" + string.Empty);

			//var isHeader = true;
			var startValue = -1;
			byte prevNewLineChar = 0;
			var headersComplete = false; // Headers bytes finished.

			while (_inputStream.DataAvailable)
			{
				if ((bytesRead = _inputStream.Read(buffer)) > 0)
				{
					Cache.Write(buffer, 0, bytesRead);

					for (var n = 0; n < bytesRead; n++)
					{
						CurrentSymbol++;
						var ch = buffer[n];

						if (headersComplete)
						{
							if (symbolsNewline.Contains(ch)) // Skip all new line chars until body.
								continue;
							break;
						}

						if (ch == prevNewLineChar) // NewLined appeared twice - HTTP header completed.
							headersComplete = true;

						if (symbolsNewline.Contains(ch)) // Is line ended.
						{
							if (startValue >= 0) // If newline char meets first time.
								header?.AddValuePart(startValue, CurrentSymbol - startValue);
							startValue = -1;
							if (prevNewLineChar == 0)
								prevNewLineChar = ch;
						}
						else if (prevNewLineChar > 0 && symbolsSpaceOrTab.Contains(ch)) // If prev char is newline and space or tab appeared.
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

					yield return header; // Returning last header.
				}
			}
		}

		/// <summary>
		/// Need to 1 - change URI, 2 - change host, 3 - change authorization.
		/// </summary>
		public async IAsyncEnumerable<HttpHeader> HeadersAsync(ProxyService proxyService, string remoteAddress, [EnumeratorCancellation] CancellationToken ctx)
		{
			var buffer = new byte[128]; // ^^^ Вернуть: [1024];
			HttpHeader header = null;

			//var symbolColon = Encoding.ASCII.GetBytes(":").First();
			var symbolsNewline = Encoding.ASCII.GetBytes("\r\n");
			var symbolsSpaceOrTab = Encoding.ASCII.GetBytes("\t" + ' ');

			//var isHeader = true;
			var startValue = -1;
			byte prevNewLineChar = 0;
			var headersCompleteChars = 0; // Headers newline bytes finished.
			var headersComplete = false;

			do
			{
				int bytesRead;

				proxyService.LogInfo($"HeadersAsync [{remoteAddress}] #1");
				var readStart = DateTime.Now;
				if ((bytesRead = await _inputStream.ReadAsync(buffer, ctx)) <= 0)
					continue;
				proxyService.LogInfo($"HeadersAsync [{remoteAddress}] #2 ${DateTime.Now - readStart}"); // ^^^ Не параллельно..
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

					if (ch == prevNewLineChar) // NewLined appeared twice - HTTP header completed.
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
			} while (!headersComplete && _inputStream.DataAvailable && !ctx.IsCancellationRequested);

			yield return header; // Returning last header.

			// ^^^ Собрать headerAuth и headerLotusUri.
			//yield return new HttpHeader();
		}
	}
}