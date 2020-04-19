using System.Collections.Generic;
using System.Linq;
using JWT.Builder;

namespace LotusHttpCredProxy.Service.Utils
{
	public static class JwtHelper
	{
		internal static string GetClaim(string claimName, string secret, string token)
		{
			var payload = new JwtBuilder()
				.WithSecret(secret)
				.MustVerifySignature()
				.Decode<IDictionary<string, object>>(token);

			return payload.FirstOrDefault(p => p.Key.Equals(claimName)).Value.ToString();
		}
	}
}