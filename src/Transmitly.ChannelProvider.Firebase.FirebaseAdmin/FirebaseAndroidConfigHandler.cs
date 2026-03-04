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
	static class FirebaseAndroidConfigHandler
	{
		public static AndroidConfig? Create(IAndroidPushNotificationContent? android)
		{
			if (android == null)
				return null;

			var notification = CreateNotification(android);
			var data = FirebaseMessageMappingUtil.MergeDictionaries(android.Headers, android.Data);
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

		private static AndroidNotification? CreateNotification(IAndroidPushNotificationContent android)
		{
			var hasNotificationValues = !string.IsNullOrWhiteSpace(android.Title) ||
				!string.IsNullOrWhiteSpace(android.Body) ||
				!string.IsNullOrWhiteSpace(android.ImageUrl) ||
				!string.IsNullOrWhiteSpace(android.Icon) ||
				!string.IsNullOrWhiteSpace(android.AccentColor) ||
				!string.IsNullOrWhiteSpace(android.Sound) ||
				!string.IsNullOrWhiteSpace(android.Tag) ||
				!string.IsNullOrWhiteSpace(android.ClickAction) ||
				!string.IsNullOrWhiteSpace(android.TitleLocalizationKey) ||
				(android.TitleLocalizationArguments?.Count > 0) ||
				!string.IsNullOrWhiteSpace(android.BodyLocalizationKey) ||
				(android.BodyLocalizationArguments?.Count > 0) ||
				!string.IsNullOrWhiteSpace(android.NotificationChannelId) ||
				!string.IsNullOrWhiteSpace(android.Ticker) ||
				android.IsSticky.HasValue ||
				android.EventTimestamp.HasValue ||
				android.IsLocalOnly.HasValue ||
				android.DisplayPriority.HasValue ||
				(android.VibrateTimings?.Count > 0) ||
				android.UseDefaultVibrateTimings.HasValue ||
				android.UseDefaultSound.HasValue ||
				android.LightSettings != null ||
				android.UseDefaultLightSettings.HasValue ||
				android.Visibility.HasValue ||
				android.NotificationCount.HasValue;

			if (!hasNotificationValues)
			{
				return null;
			}

			var notification = new AndroidNotification
			{
				Title = android.Title,
				Body = android.Body,
				ImageUrl = android.ImageUrl,
				Icon = android.Icon,
				Color = android.AccentColor,
				Sound = android.Sound,
				Tag = android.Tag,
				ClickAction = android.ClickAction,
				TitleLocKey = android.TitleLocalizationKey,
				TitleLocArgs = android.TitleLocalizationArguments,
				BodyLocKey = android.BodyLocalizationKey,
				BodyLocArgs = android.BodyLocalizationArguments,
				ChannelId = android.NotificationChannelId,
				Ticker = android.Ticker,
				Priority = MapDisplayPriority(android.DisplayPriority),
				VibrateTimingsMillis = android.VibrateTimings?.Select(x => (long)x.TotalMilliseconds).ToArray(),
				LightSettings = MapLightSettings(android.LightSettings),
				Visibility = MapVisibility(android.Visibility),
				NotificationCount = android.NotificationCount
			};

			if (android.IsSticky.HasValue)
				notification.Sticky = android.IsSticky.Value;
			if (android.EventTimestamp.HasValue)
				notification.EventTimestamp = android.EventTimestamp.Value.UtcDateTime;
			if (android.IsLocalOnly.HasValue)
				notification.LocalOnly = android.IsLocalOnly.Value;
			if (android.UseDefaultVibrateTimings.HasValue)
				notification.DefaultVibrateTimings = android.UseDefaultVibrateTimings.Value;
			if (android.UseDefaultSound.HasValue)
				notification.DefaultSound = android.UseDefaultSound.Value;
			if (android.UseDefaultLightSettings.HasValue)
				notification.DefaultLightSettings = android.UseDefaultLightSettings.Value;

			return notification;
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

		private static NotificationPriority? MapDisplayPriority(AndroidNotificationDisplayPriority? priority)
		{
			return priority switch
			{
				AndroidNotificationDisplayPriority.Minimum => NotificationPriority.MIN,
				AndroidNotificationDisplayPriority.Low => NotificationPriority.LOW,
				AndroidNotificationDisplayPriority.Default => NotificationPriority.DEFAULT,
				AndroidNotificationDisplayPriority.High => NotificationPriority.HIGH,
				AndroidNotificationDisplayPriority.Maximum => NotificationPriority.MAX,
				_ => null
			};
		}

		private static NotificationVisibility? MapVisibility(AndroidNotificationVisibility? visibility)
		{
			return visibility switch
			{
				AndroidNotificationVisibility.Private => NotificationVisibility.PRIVATE,
				AndroidNotificationVisibility.Public => NotificationVisibility.PUBLIC,
				AndroidNotificationVisibility.Secret => NotificationVisibility.SECRET,
				_ => null
			};
		}

		private static LightSettings? MapLightSettings(AndroidNotificationLightSettings? settings)
		{
			if (settings == null)
				return null;

			var hasValues = !string.IsNullOrWhiteSpace(settings.Color) ||
				settings.OnDuration.HasValue ||
				settings.OffDuration.HasValue;
			if (!hasValues)
				return null;

			var lightSettings = new LightSettings();
			if (!string.IsNullOrWhiteSpace(settings.Color))
				lightSettings.Color = settings.Color;
			if (settings.OnDuration.HasValue)
				lightSettings.LightOnDurationMillis = (long)settings.OnDuration.Value.TotalMilliseconds;
			if (settings.OffDuration.HasValue)
				lightSettings.LightOffDurationMillis = (long)settings.OffDuration.Value.TotalMilliseconds;
			return lightSettings;
		}
	}
}
