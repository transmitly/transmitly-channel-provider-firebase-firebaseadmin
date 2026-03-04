// Copyright (c) Code Impressions, LLC. All Rights Reserved.
//  
//  Licensed under the Apache License, Version 2.0 (the "License")
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//  
//      http://www.apache.org/licenses/LICENSE-2.0
//  
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.

namespace Transmitly.ChannelProvider.Firebase.FirebaseAdmin
{
	static class FirebaseMessageMappingUtil
	{
		public static Dictionary<string, string>? MergeDictionaries(params IReadOnlyDictionary<string, string>?[] sources)
		{
			Dictionary<string, string>? result = null;
			foreach (var source in sources)
			{
				if (source == null || source.Count == 0)
					continue;

				result ??= new Dictionary<string, string>(StringComparer.Ordinal);
				foreach (var item in source)
				{
					result[item.Key] = item.Value;
				}
			}

			return result;
		}

		public static Dictionary<string, object>? ToObjectDictionary(IReadOnlyDictionary<string, string>? data)
		{
			if (data == null || data.Count == 0)
				return null;

			Dictionary<string, object> result = new(data.Count, StringComparer.Ordinal);
			foreach (var item in data)
			{
				result[item.Key] = item.Value;
			}
			return result;
		}

	}
}
