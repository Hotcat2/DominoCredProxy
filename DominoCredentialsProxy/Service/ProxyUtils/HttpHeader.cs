using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DominoCredentialsProxy.Service.ProxyUtils
{
	public class HttpHeader
	{
		private readonly int _firstBytePos;
		private readonly MemoryStream _cache;

		/// <summary> [ValueFirstChar, ValueLength] </summary>
		private readonly List<KeyValuePair<int, int>> _values = new List<KeyValuePair<int, int>>();

		public HttpHeader(int firstBytePos, MemoryStream cache)
		{
			_firstBytePos = firstBytePos;
			_cache = cache;
		}

		private readonly List<byte> _name = new List<byte>();
		private string _nameCached;

		/// <summary> Adding char to header name. </summary>
		public void AddToName(byte ch)
		{
			_nameCached = null;
			_name.Add(ch);
		}

		public string Name
		{
			get
			{
				if (_nameCached != null) // Return if cached name exists.
					return _nameCached;
				_nameCached = Encoding.ASCII.GetString(_name.ToArray()).Trim();
				return _nameCached;
			}
		}

		public string Value
		{
			get
			{
				return _values.Aggregate("", (res, valuesPos) =>
				{
					var arr = new byte[valuesPos.Value];
					Array.Copy(_cache.ToArray(), valuesPos.Key, arr, 0, valuesPos.Value); // Copy this value range only.
					return res + Encoding.ASCII.GetString(arr);
				});
			}
		}
		
		/// <summary> Adding value range. </summary>
		public void AddValuePart(int startValue, int lengthValue) 
			=> _values.Add(new KeyValuePair<int, int>(startValue, lengthValue));
	}
}