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

using FirebaseAdmin.Messaging;
using Transmitly.Channel.Configuration.Push;

namespace Transmitly.ChannelProvider.Firebase.FirebaseAdmin
{
	static class FirebaseWebpushConfigHandler
	{
		public static WebpushConfig? Create(
			IWebPushNotificationContent? web,
			IReadOnlyDictionary<string, string>? defaultHeaders)
		{
			var headers = FirebaseMessageMappingUtil.MergeDictionaries(defaultHeaders, web?.Headers);
			var data = web?.Data;
			var notification = web != null ? CreateNotification(web) : null;

			if (headers?.Count <= 0 && data?.Count <= 0 && notification == null)
				return null;

			return new WebpushConfig
			{
				Headers = headers,
				Data = data,
				Notification = notification
			};
		}

		private static WebpushNotification? CreateNotification(IWebPushNotificationContent web)
		{
			var actions = MapActions(web.Actions);
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
				web.Direction.HasValue ||
				actions?.Any() == true;

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
				Direction = MapDirection(web.Direction),
				Data = web.Data,
				Actions = actions
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

		private static IEnumerable<global::FirebaseAdmin.Messaging.Action>? MapActions(IReadOnlyCollection<WebPushNotificationAction>? actions)
		{
			if (actions?.Count <= 0)
				return null;

			var mapped = actions!
				.Where(x => !string.IsNullOrWhiteSpace(x.Action) && !string.IsNullOrWhiteSpace(x.Title))
				.Select(x => new global::FirebaseAdmin.Messaging.Action
				{
					ActionName = x.Action,
					Title = x.Title,
					Icon = x.Icon
				})
				.ToArray();

			return mapped.Length > 0 ? mapped : null;
		}
	}
}
