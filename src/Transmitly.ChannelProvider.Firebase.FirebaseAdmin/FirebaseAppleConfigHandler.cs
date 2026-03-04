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
	static class FirebaseAppleConfigHandler
	{
		public static ApnsConfig? Create(
			IApplePushNotificationContent? apple,
			IReadOnlyDictionary<string, string>? defaultHeaders)
		{
			var headers = FirebaseMessageMappingUtil.MergeDictionaries(defaultHeaders, apple?.Headers);
			var customData = apple?.Data?.Count > 0
				? FirebaseMessageMappingUtil.ToObjectDictionary(apple.Data)
				: null;

			ApsAlert? alert = null;
			Aps? aps = null;
			ApnsFcmOptions? fcmOptions = null;
			string? liveActivityToken = null;
			if (apple != null)
			{
				alert = CreateAlert(apple);
				aps = CreateAps(apple, alert);
				liveActivityToken = apple.LiveActivityToken;
				if (!string.IsNullOrWhiteSpace(apple.ImageUrl))
				{
					fcmOptions = new ApnsFcmOptions
					{
						ImageUrl = apple.ImageUrl
					};
				}
			}

			if (headers?.Count <= 0 &&
				customData?.Count <= 0 &&
				aps == null &&
				fcmOptions == null &&
				string.IsNullOrWhiteSpace(liveActivityToken))
				return null;

			return new ApnsConfig
			{
				Headers = headers,
				CustomData = customData,
				Aps = aps,
				FcmOptions = fcmOptions,
				LiveActivityToken = liveActivityToken
			};
		}

		private static ApsAlert? CreateAlert(IApplePushNotificationContent apple)
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
				(apple.SubtitleLocalizationArguments?.Count > 0) ||
				!string.IsNullOrWhiteSpace(apple.LaunchImage);

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
				SubtitleLocArgs = apple.SubtitleLocalizationArguments,
				LaunchImage = apple.LaunchImage
			};
		}

		private static Aps? CreateAps(IApplePushNotificationContent apple, ApsAlert? alert)
		{
			var criticalSound = MapCriticalSound(apple.CriticalSound);
			var apsCustomData = MapApsCustomData(
				apple.InterruptionLevel,
				apple.RelevanceScore,
				apple.TargetContentId);
			var hasApsValues = alert != null ||
				apple.Badge.HasValue ||
				!string.IsNullOrWhiteSpace(apple.Sound) ||
				criticalSound != null ||
				apple.IsBackgroundUpdate.HasValue ||
				apple.IsContentMutable.HasValue ||
				!string.IsNullOrWhiteSpace(apple.Category) ||
				!string.IsNullOrWhiteSpace(apple.ThreadId) ||
				apsCustomData?.Count > 0;

			if (!hasApsValues)
				return null;

			return new Aps
			{
				Alert = alert,
				Badge = apple.Badge,
				Sound = criticalSound == null ? apple.Sound : null,
				CriticalSound = criticalSound,
				ContentAvailable = apple.IsBackgroundUpdate ?? false,
				MutableContent = apple.IsContentMutable ?? false,
				Category = apple.Category,
				ThreadId = apple.ThreadId,
				CustomData = apsCustomData
			};
		}

		private static CriticalSound? MapCriticalSound(AppleCriticalSound? criticalSound)
		{
			if (criticalSound == null)
				return null;

			var hasValues = criticalSound.IsCritical.HasValue ||
				!string.IsNullOrWhiteSpace(criticalSound.Name) ||
				criticalSound.Volume.HasValue;
			if (!hasValues)
				return null;

			return new CriticalSound
			{
				Critical = criticalSound.IsCritical ?? true,
				Name = criticalSound.Name,
				Volume = criticalSound.Volume
			};
		}

		private static Dictionary<string, object>? MapApsCustomData(
			AppleNotificationInterruptionLevel? interruptionLevel,
			double? relevanceScore,
			string? targetContentId)
		{
			Dictionary<string, object>? customData = null;
			var interruptionLevelValue = interruptionLevel switch
			{
				AppleNotificationInterruptionLevel.Passive => "passive",
				AppleNotificationInterruptionLevel.Active => "active",
				AppleNotificationInterruptionLevel.TimeSensitive => "time-sensitive",
				AppleNotificationInterruptionLevel.Critical => "critical",
				_ => null
			};

			if (!string.IsNullOrWhiteSpace(interruptionLevelValue))
			{
				customData ??= new Dictionary<string, object>(StringComparer.Ordinal);
				customData["interruption-level"] = interruptionLevelValue;
			}
			if (relevanceScore.HasValue)
			{
				customData ??= new Dictionary<string, object>(StringComparer.Ordinal);
				customData["relevance-score"] = relevanceScore.Value;
			}
			if (!string.IsNullOrWhiteSpace(targetContentId))
			{
				customData ??= new Dictionary<string, object>(StringComparer.Ordinal);
				customData["target-content-id"] = targetContentId;
			}

			return customData;
		}
	}
}
