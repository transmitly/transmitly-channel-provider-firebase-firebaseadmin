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
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Transmitly.Channel.Push;
using Transmitly.ChannelProvider.Firebase.Configuration;
using Transmitly.Util;

namespace Transmitly.ChannelProvider.Firebase.FirebaseAdmin
{
	public sealed partial class FirebaseAdminChannelProviderDispatcher(FirebaseOptions options) : ChannelProviderDispatcher<IPushNotification>
	{
		private static readonly HashSet<string> _deviceTokenKeys = new(StringComparer.OrdinalIgnoreCase)
		{
			PlatformIdentityAddress.Types.DeviceToken(),
			"devicetoken",
			"device_token",
			"push-token",
			"pushtoken",
			"push_token",
			"token"
		};

		private static readonly HashSet<string> _topicKeys = new(StringComparer.OrdinalIgnoreCase)
		{
			PlatformIdentityAddress.Types.Topic(),
			"topic",
			"push-topic",
			"pushtopic",
			"push_topic",
			"/topics"
		};

		// Same as core's push channel: an APNs hex token, or 20+ URL-safe characters with FCM's optional "<instance id>:" prefix.
		private const string DeviceTokenPattern = @"^(?:[A-Fa-f0-9]{64}|(?:[A-Za-z0-9_-]+:)?[A-Za-z0-9_-]{20,})$";
#if NET7_0_OR_GREATER
		[GeneratedRegex(DeviceTokenPattern)]
		private static partial Regex DeviceTokenRegex();
		private static readonly Regex _deviceTokenPattern = DeviceTokenRegex();
#else
		private static readonly Regex _deviceTokenPattern = new(DeviceTokenPattern, RegexOptions.Compiled);
#endif
		private static readonly ConcurrentDictionary<(string AppName, string Fingerprint, Stream? Stream), Lazy<FirebaseApp>> _apps = new();
		private static int _appCount;
		private readonly FirebaseApp _app = GetApp(Guard.AgainstNull(options));

		/// <summary>
		/// Deletes the Firebase apps this provider created, such as the ones a rotated credential left behind.
		/// Apps still in use are created again on their next dispatch. Don't call it while a dispatch is in flight.
		/// </summary>
		/// <param name="appName">Only delete apps created for this <see cref="FirebaseOptions.AppName"/>; <see langword="null"/> deletes them all.</param>
		public static void DeleteApps(string? appName = null)
		{
			foreach (var key in _apps.Keys)
			{
				if ((appName == null || key.AppName == appName) && _apps.TryRemove(key, out var app) && app.IsValueCreated)
					app.Value.Delete();
			}
		}

		internal static FirebaseApp GetApp(FirebaseOptions options)
		{
			// An app the host created under this name is used as is; the host owns its credential.
			var hostApp = FirebaseApp.GetInstance(options.AppName);
			if (hostApp != null)
				return hostApp;

			// Keyed by credential as well as name, so a rotated credential gets a new app and providers with different credentials never share one.
			var key = (options.AppName, FirebaseOptionsConverter.Fingerprint(options), options.Credential?.Stream);
			var app = _apps.GetOrAdd(key, k => new Lazy<FirebaseApp>(() =>
				FirebaseApp.Create(FirebaseOptionsConverter.FromFirebaseOptions(options), $"{k.AppName}#{Interlocked.Increment(ref _appCount)}")));
			try
			{
				return app.Value;
			}
			catch
			{
				// Don't cache the failure; the next dispatch tries again.
				((ICollection<KeyValuePair<(string, string, Stream?), Lazy<FirebaseApp>>>)_apps).Remove(new(key, app));
				throw;
			}
		}

		public override async Task<IReadOnlyCollection<IDispatchResult?>> DispatchAsync(IPushNotification communication, IDispatchCommunicationContext communicationContext, CancellationToken cancellationToken)
		{
			List<Message> messages = new(communication.Recipient.Count);
			foreach (var recipient in communication.Recipient)
			{
				messages.Add(CreateMessage(communication, recipient));
			}

			var response = await FirebaseMessaging.GetMessaging(_app).SendEachAsync(messages, cancellationToken);

			var results = response.Responses.Select(m => new FirebaseDispatchResult(m)).ToList();
			Dispatched(communicationContext, communication, results.Where(x => x.Status.IsSuccess()).ToList());
			Error(communicationContext, communication, results.Where(x => !x.Status.IsSuccess()).ToList());
			return results;
		}

		private static Message CreateMessage(
			IPushNotification communication,
			IPlatformIdentityAddress recipient)
		{
			var token = ResolveToken(recipient);
			var topic = ResolveTopic(recipient);
			var defaultHeaders = communication.Headers;

			return new Message
			{
				Data = communication.Data,
				Notification = CreateNotification(communication.Title, communication.Body, communication.ImageUrl),
				Android = FirebaseAndroidConfigHandler.Create(communication.Android),
				Apns = FirebaseAppleConfigHandler.Create(communication.Apple, defaultHeaders),
				Webpush = FirebaseWebpushConfigHandler.Create(communication.Web, defaultHeaders),
				//todo: Token is deprecated in favor of Fid, but apps send FCM registration tokens, not Firebase
				// Installation IDs, so this stays until FID recipients are supported. Revisit later.
#pragma warning disable CS0618
				Token = token,
#pragma warning restore CS0618
				Topic = topic
			};
		}

		private static string? ResolveToken(IPlatformIdentityAddress recipient)
		{
			if (recipient.IsType(PlatformIdentityAddress.Types.DeviceToken()))
			{
				return recipient.Value;
			}

			if (recipient.IsType(PlatformIdentityAddress.Types.Topic()) || IsTopic(recipient))
			{
				return null;
			}

			return IsDeviceToken(recipient) ? recipient.Value : null;
		}

		private static string? ResolveTopic(IPlatformIdentityAddress recipient)
		{
			if (recipient.IsType(PlatformIdentityAddress.Types.Topic()))
			{
				return NormalizeTopic(recipient.Value);
			}

			if (recipient.IsType(PlatformIdentityAddress.Types.DeviceToken()))
			{
				return null;
			}

			return IsTopic(recipient) ? NormalizeTopic(recipient.Value) : null;
		}

		private static bool IsDeviceToken(IPlatformIdentityAddress recipient)
		{
			if (MatchesConvention(recipient, _deviceTokenKeys))
			{
				return true;
			}

			return _deviceTokenPattern.IsMatch(recipient.Value);
		}

		private static bool IsTopic(IPlatformIdentityAddress recipient)
		{
			if (MatchesConvention(recipient, _topicKeys))
			{
				return true;
			}

			return recipient.Value.StartsWith("/topics/", StringComparison.OrdinalIgnoreCase) ||
				recipient.Value.StartsWith("topic:", StringComparison.OrdinalIgnoreCase);
		}

		private static bool MatchesConvention(IPlatformIdentityAddress recipient, HashSet<string> keys)
		{
			if (recipient.Type is string type && keys.Contains(type))
			{
				return true;
			}

			if (recipient.Purposes is not null && recipient.Purposes.Any(p => p is not null && keys.Contains(p)))
			{
				return true;
			}

			if (recipient.AddressParts.Keys.Any(keys.Contains))
			{
				return true;
			}

			return recipient.Attributes.Keys.Any(keys.Contains);
		}

		private static string NormalizeTopic(string topic)
		{
			if (topic.StartsWith("/topics/", StringComparison.OrdinalIgnoreCase))
			{
				return topic["/topics/".Length..];
			}

			if (topic.StartsWith("topic:", StringComparison.OrdinalIgnoreCase))
			{
				return topic["topic:".Length..];
			}

			return topic;
		}

		private static Notification? CreateNotification(string? title, string? body, string? imageUrl)
		{
			if (string.IsNullOrWhiteSpace(title) &&
				string.IsNullOrWhiteSpace(body) &&
				string.IsNullOrWhiteSpace(imageUrl))
			{
				return null;
			}

			return new Notification
			{
				Title = title,
				Body = body,
				ImageUrl = imageUrl
			};
		}
	}
}
