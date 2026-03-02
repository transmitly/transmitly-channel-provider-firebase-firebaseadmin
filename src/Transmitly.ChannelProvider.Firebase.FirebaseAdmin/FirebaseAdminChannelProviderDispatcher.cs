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
using Transmitly.Channel.Configuration.Push;
using Transmitly.Channel.Push;
using Transmitly.ChannelProvider.Firebase.Configuration;
using Transmitly.Util;

namespace Transmitly.ChannelProvider.Firebase.FirebaseAdmin
{
	public sealed class FirebaseAdminChannelProviderDispatcher : ChannelProviderDispatcher<IPushNotification>
	{
		private static readonly ConcurrentDictionary<string, Lazy<FirebaseApp>> _apps = new();
		private readonly FirebaseApp _app;

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
			var defaultHeaders = communication.Headers;

			return new Message
			{
				Data = communication.Data,
				Notification = CreateNotification(communication.Title, communication.Body, communication.ImageUrl),
				Android = CreateAndroidConfig(communication.Android),
				Apns = CreateApnsConfig(communication.Apple, defaultHeaders),
				Webpush = CreateWebpushConfig(communication.Web, defaultHeaders),
				Token = recipient.IfType(PlatformIdentityAddress.Types.DeviceToken(), recipient.Value),
				Topic = recipient.IfType(PlatformIdentityAddress.Types.Topic(), recipient.Value)
			};
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

		private static AndroidConfig? CreateAndroidConfig(
			IAndroidPushNotificationContent? android)
		{
			if (android == null)
				return null;

			var notification = CreateAndroidNotification(android);
			var data = android.Data;
			var priority = MapPriority(android.Priority);

			var hasConfigValues = notification != null ||
				data?.Count > 0 ||
				!string.IsNullOrWhiteSpace(android.CollapseId) ||
				priority.HasValue ||
				android.TimeToLive.HasValue ||
				!string.IsNullOrWhiteSpace(android.TargetApplicationId) ||
				android.AllowDeliveryBeforeFirstUnlock.HasValue;

			if (!hasConfigValues)
				return null;

			return new AndroidConfig
			{
				CollapseKey = android.CollapseId,
				Priority = priority,
				TimeToLive = android.TimeToLive,
				RestrictedPackageName = android.TargetApplicationId,
				DirectBootOk = android.AllowDeliveryBeforeFirstUnlock,
				Data = data,
				Notification = notification
			};
		}

		private static AndroidNotification? CreateAndroidNotification(IAndroidPushNotificationContent android)
		{
			if (string.IsNullOrWhiteSpace(android.Title) &&
				string.IsNullOrWhiteSpace(android.Body) &&
				string.IsNullOrWhiteSpace(android.ImageUrl))
			{
				return null;
			}

			return new AndroidNotification
			{
				Title = android.Title,
				Body = android.Body,
				ImageUrl = android.ImageUrl
			};
		}

		private static ApnsConfig? CreateApnsConfig(
			IApplePushNotificationContent? apple,
			IReadOnlyDictionary<string, string>? defaultHeaders)
		{
			var headers = MergeDictionaries(defaultHeaders, apple?.Headers);
			var customData = apple?.Data?.Count > 0
				? ToObjectDictionary(apple.Data)
				: null;

			ApsAlert? alert = null;
			Aps? aps = null;
			ApnsFcmOptions? fcmOptions = null;
			if (apple != null)
			{
				alert = CreateApsAlert(apple);
				aps = CreateAps(apple, alert);
				if (!string.IsNullOrWhiteSpace(apple.ImageUrl))
				{
					fcmOptions = new ApnsFcmOptions
					{
						ImageUrl = apple.ImageUrl
					};
				}
			}

			if (headers?.Count <= 0 && customData?.Count <= 0 && aps == null && fcmOptions == null)
				return null;

			return new ApnsConfig
			{
				Headers = headers,
				CustomData = customData,
				Aps = aps,
				FcmOptions = fcmOptions
			};
		}

		private static ApsAlert? CreateApsAlert(IApplePushNotificationContent apple)
		{
			var hasAlertValues = !string.IsNullOrWhiteSpace(apple.Title) ||
				!string.IsNullOrWhiteSpace(apple.Body) ||
				!string.IsNullOrWhiteSpace(apple.Subtitle) ||
				!string.IsNullOrWhiteSpace(apple.ActionLocalizationKey) ||
				!string.IsNullOrWhiteSpace(apple.BodyLocalizationKey) ||
				(apple.BodyLocalizationArguments?.Count > 0) ||
				!string.IsNullOrWhiteSpace(apple.TitleLocalizationKey) ||
				(apple.TitleLocalizationArguments?.Count > 0) ||
				!string.IsNullOrWhiteSpace(apple.SubtitleLocalizationKey) ||
				(apple.SubtitleLocalizationArguments?.Count > 0);

			if (!hasAlertValues)
				return null;

			return new ApsAlert
			{
				Title = apple.Title,
				Body = apple.Body,
				Subtitle = apple.Subtitle,
				ActionLocKey = apple.ActionLocalizationKey,
				LocKey = apple.BodyLocalizationKey,
				LocArgs = apple.BodyLocalizationArguments,
				TitleLocKey = apple.TitleLocalizationKey,
				TitleLocArgs = apple.TitleLocalizationArguments,
				SubtitleLocKey = apple.SubtitleLocalizationKey,
				SubtitleLocArgs = apple.SubtitleLocalizationArguments
			};
		}

		private static Aps? CreateAps(IApplePushNotificationContent apple, ApsAlert? alert)
		{
			var hasApsValues = alert != null ||
				apple.Badge.HasValue ||
				!string.IsNullOrWhiteSpace(apple.Sound) ||
				apple.IsBackgroundUpdate.HasValue ||
				apple.IsContentMutable.HasValue ||
				!string.IsNullOrWhiteSpace(apple.Category) ||
				!string.IsNullOrWhiteSpace(apple.ThreadId);

			if (!hasApsValues)
				return null;

			return new Aps
			{
				Alert = alert,
				Badge = apple.Badge,
				Sound = apple.Sound,
				ContentAvailable = apple.IsBackgroundUpdate ?? false,
				MutableContent = apple.IsContentMutable ?? false,
				Category = apple.Category,
				ThreadId = apple.ThreadId
			};
		}

		private static WebpushConfig? CreateWebpushConfig(
			IWebPushNotificationContent? web,
			IReadOnlyDictionary<string, string>? defaultHeaders)
		{
			var headers = MergeDictionaries(defaultHeaders, web?.Headers);
			var data = web?.Data;
			var notification = web != null ? CreateWebpushNotification(web) : null;

			if (headers?.Count <= 0 && data?.Count <= 0 && notification == null)
				return null;

			return new WebpushConfig
			{
				Headers = headers,
				Data = data,
				Notification = notification
			};
		}

		private static WebpushNotification? CreateWebpushNotification(IWebPushNotificationContent web)
		{
			var hasNotificationValues = !string.IsNullOrWhiteSpace(web.Title) ||
				!string.IsNullOrWhiteSpace(web.Body) ||
				!string.IsNullOrWhiteSpace(web.Icon) ||
				!string.IsNullOrWhiteSpace(web.Badge) ||
				!string.IsNullOrWhiteSpace(web.ImageUrl) ||
				!string.IsNullOrWhiteSpace(web.Language) ||
				web.Renotify.HasValue ||
				web.RequireInteraction.HasValue ||
				web.IsSilent.HasValue ||
				!string.IsNullOrWhiteSpace(web.Tag) ||
				web.Timestamp.HasValue ||
				(web.VibratePattern?.Count > 0) ||
				web.Direction.HasValue;

			if (!hasNotificationValues)
				return null;

			return new WebpushNotification
			{
				Title = web.Title,
				Body = web.Body,
				Icon = web.Icon,
				Badge = web.Badge,
				Image = web.ImageUrl,
				Language = web.Language,
				Renotify = web.Renotify,
				RequireInteraction = web.RequireInteraction,
				Silent = web.IsSilent,
				Tag = web.Tag,
				TimestampMillis = web.Timestamp?.ToUnixTimeMilliseconds(),
				Vibrate = web.VibratePattern?.ToArray(),
				Direction = MapDirection(web.Direction)
			};
		}

		private static Priority? MapPriority(AndroidNotificationPriority? priority)
		{
			return priority switch
			{
				AndroidNotificationPriority.High => Priority.High,
				AndroidNotificationPriority.Normal => Priority.Normal,
				_ => null
			};
		}

		private static Direction? MapDirection(WebPushDisplayDirection? direction)
		{
			return direction switch
			{
				WebPushDisplayDirection.Auto => Direction.Auto,
				WebPushDisplayDirection.LeftToRight => Direction.LeftToRight,
				WebPushDisplayDirection.RightToLeft => Direction.RightToLeft,
				_ => null
			};
		}

		private static Dictionary<string, object>? ToObjectDictionary(IReadOnlyDictionary<string, string>? data)
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

		private static Dictionary<string, string>? MergeDictionaries(params IReadOnlyDictionary<string, string>?[] sources)
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
	}
}
