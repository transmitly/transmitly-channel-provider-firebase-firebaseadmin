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

using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using Transmitly.Channel.Push;
using Transmitly.ChannelProvider.Firebase.Configuration;
using Transmitly.Util;

namespace Transmitly.ChannelProvider.Firebase.FirebaseAdmin
{
	public sealed class FirebaseAdminChannelProviderDispatcher : ChannelProviderDispatcher<IPushNotification>
	{
		private static readonly ConcurrentDictionary<string, Lazy<FirebaseApp>> _apps = new();
		private readonly FirebaseApp _app;
		private static readonly HashSet<string> _excludedDataKeys = new(StringComparer.OrdinalIgnoreCase)
		{
			"trx",
			"pid",
			"att",
			"lnk"
		};
		private const int MaxFlattenDepth = 8;

		public FirebaseAdminChannelProviderDispatcher(FirebaseOptions options)
		{
			Guard.AgainstNull(options);

			var convertedOptions = FirebaseOptionsConverter.FromFirebaseOptions(options);

			_app = _apps.GetOrAdd(options.AppName,
				new Lazy<FirebaseApp>(() =>
				{
					// There might be another instnace from the hosted app with the same app name
					var existing = FirebaseApp.GetInstance(options.AppName);
					return existing ?? FirebaseApp.Create(convertedOptions, options.AppName);

				}, isThreadSafe: true)).Value;
		}

		public override async Task<IReadOnlyCollection<IDispatchResult?>> DispatchAsync(IPushNotification communication, IDispatchCommunicationContext communicationContext, CancellationToken cancellationToken)
		{
			List<Message> messages = new(communication.Recipient.Count);
			foreach (var recipient in communication.Recipient)
			{
				messages.Add(new Message
				{
					Data = TryConvertToDictionary(communicationContext.ContentModel?.Model),
					Notification = new Notification
					{
						Title = communication.Title,
						Body = communication.Body,
						ImageUrl = communication.ImageUrl
					},
					Token = recipient.IfType(PlatformIdentityAddress.Types.DeviceToken(), recipient.Value),
					Topic = recipient.IfType(PlatformIdentityAddress.Types.Topic(), recipient.Value)
				});
			}

			var response = await FirebaseMessaging.GetMessaging(_app).SendEachAsync(messages, cancellationToken);

			var results = response.Responses.Select(m => new FirebaseDispatchResult(m)).ToList();
			Dispatched(communicationContext, communication, results.Where(x => x.Status.IsSuccess()).ToList());
			Error(communicationContext, communication, results.Where(x => !x.Status.IsSuccess()).ToList());
			return results;
		}
		
		private static Dictionary<string, string>? TryConvertToDictionary(object? content)
		{
			try
			{
				if (content == null)
					return null;

				if (TryExtractDictionaryEntries(content, out var dictionaryEntries))
					return NormalizeToStringDictionary(dictionaryEntries);

				var descriptorValues = content
					.GetType()
					.GetProperties()
					.Where(prop => prop.GetIndexParameters().Length == 0)
					.Select(prop => new KeyValuePair<string, object?>(prop.Name, prop.GetValue(content, null)));

				return NormalizeToStringDictionary(descriptorValues);
			}
			catch
			{
				return null;
			}
		}

		private static bool TryExtractDictionaryEntries(object content, out IEnumerable<KeyValuePair<string, object?>> values)
		{
			values = [];
			if (content is IReadOnlyDictionary<string, object?> readOnlyObjectDictionary)
			{
				values = readOnlyObjectDictionary;
				return true;
			}

			if (content is IDictionary<string, object?> objectDictionary)
			{
				values = objectDictionary;
				return true;
			}

			if (content is IDictionary<string, string> stringDictionary)
			{
				values = stringDictionary.Select(x => new KeyValuePair<string, object?>(x.Key, x.Value));
				return true;
			}

			if (content is IDictionary nonGenericDictionary)
			{
				values = nonGenericDictionary
					.Cast<DictionaryEntry>()
					.Where(entry => entry.Key != null)
					.Select(entry => new KeyValuePair<string, object?>(entry.Key!.ToString()!, entry.Value));
				return true;
			}

			return false;
		}

		private static Dictionary<string, string>? NormalizeToStringDictionary(IEnumerable<KeyValuePair<string, object?>> values)
		{
			var data = new Dictionary<string, string>(StringComparer.Ordinal);
			foreach (var value in values.Where(x => !string.IsNullOrWhiteSpace(x.Key)))
			{
				AppendValue(data, value.Key, value.Value, depth: 0, isRoot: true);
			}

			return data.Count > 0 ? data : null;
		}

		private static void AppendValue(Dictionary<string, string> data, string key, object? value, int depth, bool isRoot = false)
		{
			if (string.IsNullOrWhiteSpace(key) || value == null)
				return;

			if (isRoot && _excludedDataKeys.Contains(key))
				return;

			if (TryConvertScalarToString(value, out var scalar))
			{
				data[key] = scalar!;
				return;
			}

			if (depth >= MaxFlattenDepth)
				return;

			if (TryExtractDictionaryEntries(value, out var dictionaryEntries))
			{
				foreach (var child in dictionaryEntries.Where(x => !string.IsNullOrWhiteSpace(x.Key)))
				{
					AppendValue(data, $"{key}.{child.Key}", child.Value, depth + 1);
				}
				return;
			}

			var properties = value
				.GetType()
				.GetProperties()
				.Where(prop => prop.GetIndexParameters().Length == 0)
				.ToArray();

			if (properties.Length == 0)
				return;

			foreach (var property in properties)
			{
				AppendValue(data, $"{key}.{property.Name}", property.GetValue(value, null), depth + 1);
			}
		}

		private static bool TryConvertScalarToString(object? value, out string? result)
		{
			result = null;
			if (value == null)
				return false;

			if (value is string str)
			{
				result = str;
				return true;
			}

			if (value is IFormattable formattable)
			{
				result = formattable.ToString(format: null, CultureInfo.InvariantCulture);
				return true;
			}

			return false;
		}
	}
}