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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using Transmitly.Channel.Configuration.Push;
using Transmitly.Channel.Push;

namespace Transmitly.ChannelProvider.Firebase.FirebaseAdmin.Tests;

[TestClass]
public sealed class FirebaseAdminChannelProviderDispatcherTests
{
	private static readonly MethodInfo CreateMessageMethod =
		typeof(FirebaseAdminChannelProviderDispatcher)
			.GetMethod("CreateMessage", BindingFlags.Static | BindingFlags.NonPublic)!;

	[TestMethod]
	public void CreateMessage_ShouldMapDefaultAndPlatformSpecificPushConfiguration()
	{
		var communication = new PushNotificationStub
		{
			Title = "default-title",
			Body = "default-body",
			ImageUrl = "default-image",
			Data = new Dictionary<string, string>
			{
				["default-key"] = "default-value",
				["override"] = "default-override"
			},
			Headers = new Dictionary<string, string>
			{
				["x-default"] = "default-header",
				["x-override"] = "default-header-override"
			},
			Recipient =
			[
				new PlatformIdentityAddress("device-token", type: PlatformIdentityAddress.Types.DeviceToken())
			],
			Android = new AndroidPushNotificationContentStub
			{
				Title = "android-title",
				Body = "android-body",
				ImageUrl = "android-image",
				Data = new Dictionary<string, string>
				{
					["android-key"] = "android-value",
					["override"] = "android-override"
				},
				CollapseId = "collapse-id",
				Priority = AndroidNotificationPriority.High,
				TimeToLive = TimeSpan.FromMinutes(5),
				TargetApplicationId = "com.example.app",
				AllowDeliveryBeforeFirstUnlock = true
			},
			Apple = new ApplePushNotificationContentStub
			{
				Title = "apple-title",
				Body = "apple-body",
				ImageUrl = "apple-image",
				Data = new Dictionary<string, string>
				{
					["apple-key"] = "apple-value",
					["override"] = "apple-override"
				},
				Headers = new Dictionary<string, string>
				{
					["apns-specific"] = "apns-header",
					["x-override"] = "apns-override"
				},
				Subtitle = "apple-subtitle",
				SubtitleLocalizationKey = "subtitle-loc-key",
				SubtitleLocalizationArguments = ["sa"],
				ActionLocalizationKey = "action-loc-key",
				BodyLocalizationKey = "body-loc-key",
				BodyLocalizationArguments = ["ba"],
				TitleLocalizationKey = "title-loc-key",
				TitleLocalizationArguments = ["ta"],
				Badge = 3,
				Sound = "ding",
				IsBackgroundUpdate = true,
				IsContentMutable = true,
				Category = "category-id",
				ThreadId = "thread-id"
			},
			Web = new WebPushNotificationContentStub
			{
				Title = "web-title",
				Body = "web-body",
				ImageUrl = "web-image",
				Icon = "web-icon",
				Badge = "web-badge",
				Language = "en-US",
				Data = new Dictionary<string, string>
				{
					["web-key"] = "web-value",
					["override"] = "web-override"
				},
				Headers = new Dictionary<string, string>
				{
					["web-specific"] = "web-header",
					["x-override"] = "web-override"
				},
				Renotify = true,
				RequireInteraction = true,
				IsSilent = false,
				Tag = "web-tag",
				Timestamp = new DateTimeOffset(2025, 1, 1, 1, 2, 3, TimeSpan.Zero),
				VibratePattern = [100, 200],
				Direction = WebPushDisplayDirection.RightToLeft
			}
		};

		var message = InvokeCreateMessage(communication, communication.Recipient.First());

		Assert.IsNotNull(message.Notification);
		Assert.AreEqual("default-title", message.Notification.Title);
		Assert.AreEqual("default-body", message.Notification.Body);
		Assert.AreEqual("default-image", message.Notification.ImageUrl);
		Assert.IsNotNull(message.Data);
		Assert.AreEqual("default-value", message.Data["default-key"]);
		Assert.AreEqual("default-override", message.Data["override"]);
		Assert.IsFalse(message.Data.ContainsKey("android-key"));
		Assert.IsFalse(message.Data.ContainsKey("apple-key"));
		Assert.IsFalse(message.Data.ContainsKey("web-key"));
		Assert.AreEqual("device-token", message.Token);
		Assert.IsNull(message.Topic);

		Assert.IsNotNull(message.Android);
		Assert.AreEqual("collapse-id", message.Android.CollapseKey);
		Assert.AreEqual(Priority.High, message.Android.Priority);
		Assert.AreEqual(TimeSpan.FromMinutes(5), message.Android.TimeToLive);
		Assert.AreEqual("com.example.app", message.Android.RestrictedPackageName);
		Assert.AreEqual(true, message.Android.DirectBootOk);
		Assert.IsNotNull(message.Android.Notification);
		Assert.AreEqual("android-title", message.Android.Notification.Title);
		Assert.AreEqual("android-body", message.Android.Notification.Body);
		Assert.AreEqual("android-image", message.Android.Notification.ImageUrl);
		Assert.IsNotNull(message.Android.Data);
		Assert.AreEqual("android-value", message.Android.Data["android-key"]);
		Assert.AreEqual("android-override", message.Android.Data["override"]);
		Assert.IsFalse(message.Android.Data.ContainsKey("default-key"));
		Assert.IsFalse(message.Android.Data.ContainsKey("apple-key"));
		Assert.IsFalse(message.Android.Data.ContainsKey("web-key"));

		Assert.IsNotNull(message.Apns);
		Assert.IsNotNull(message.Apns.Headers);
		Assert.AreEqual("default-header", message.Apns.Headers["x-default"]);
		Assert.AreEqual("apns-header", message.Apns.Headers["apns-specific"]);
		Assert.AreEqual("apns-override", message.Apns.Headers["x-override"]);
		Assert.IsNotNull(message.Apns.CustomData);
		Assert.AreEqual("apple-value", message.Apns.CustomData["apple-key"]);
		Assert.AreEqual("apple-override", message.Apns.CustomData["override"]);
		Assert.IsFalse(message.Apns.CustomData.ContainsKey("default-key"));
		Assert.IsFalse(message.Apns.CustomData.ContainsKey("android-key"));
		Assert.IsFalse(message.Apns.CustomData.ContainsKey("web-key"));
		Assert.IsNotNull(message.Apns.FcmOptions);
		Assert.AreEqual("apple-image", message.Apns.FcmOptions.ImageUrl);
		Assert.IsNotNull(message.Apns.Aps);
		Assert.AreEqual(3, message.Apns.Aps.Badge);
		Assert.AreEqual("ding", message.Apns.Aps.Sound);
		Assert.AreEqual(true, message.Apns.Aps.ContentAvailable);
		Assert.AreEqual(true, message.Apns.Aps.MutableContent);
		Assert.AreEqual("category-id", message.Apns.Aps.Category);
		Assert.AreEqual("thread-id", message.Apns.Aps.ThreadId);
		Assert.IsNotNull(message.Apns.Aps.Alert);
		Assert.AreEqual("apple-title", message.Apns.Aps.Alert.Title);
		Assert.AreEqual("apple-body", message.Apns.Aps.Alert.Body);
		Assert.AreEqual("apple-subtitle", message.Apns.Aps.Alert.Subtitle);
		Assert.AreEqual("subtitle-loc-key", message.Apns.Aps.Alert.SubtitleLocKey);
		CollectionAssert.AreEqual(new[] { "sa" }, message.Apns.Aps.Alert.SubtitleLocArgs!.ToArray());
		Assert.AreEqual("action-loc-key", message.Apns.Aps.Alert.ActionLocKey);
		Assert.AreEqual("body-loc-key", message.Apns.Aps.Alert.LocKey);
		CollectionAssert.AreEqual(new[] { "ba" }, message.Apns.Aps.Alert.LocArgs!.ToArray());
		Assert.AreEqual("title-loc-key", message.Apns.Aps.Alert.TitleLocKey);
		CollectionAssert.AreEqual(new[] { "ta" }, message.Apns.Aps.Alert.TitleLocArgs!.ToArray());

		Assert.IsNotNull(message.Webpush);
		Assert.IsNotNull(message.Webpush.Headers);
		Assert.AreEqual("default-header", message.Webpush.Headers["x-default"]);
		Assert.AreEqual("web-header", message.Webpush.Headers["web-specific"]);
		Assert.AreEqual("web-override", message.Webpush.Headers["x-override"]);
		Assert.IsNotNull(message.Webpush.Data);
		Assert.AreEqual("web-value", message.Webpush.Data["web-key"]);
		Assert.AreEqual("web-override", message.Webpush.Data["override"]);
		Assert.IsFalse(message.Webpush.Data.ContainsKey("default-key"));
		Assert.IsFalse(message.Webpush.Data.ContainsKey("android-key"));
		Assert.IsFalse(message.Webpush.Data.ContainsKey("apple-key"));
		Assert.IsNotNull(message.Webpush.Notification);
		Assert.AreEqual("web-title", message.Webpush.Notification.Title);
		Assert.AreEqual("web-body", message.Webpush.Notification.Body);
		Assert.AreEqual("web-image", message.Webpush.Notification.Image);
		Assert.AreEqual("web-icon", message.Webpush.Notification.Icon);
		Assert.AreEqual("web-badge", message.Webpush.Notification.Badge);
		Assert.AreEqual("en-US", message.Webpush.Notification.Language);
		Assert.AreEqual(true, message.Webpush.Notification.Renotify);
		Assert.AreEqual(true, message.Webpush.Notification.RequireInteraction);
		Assert.AreEqual(false, message.Webpush.Notification.Silent);
		Assert.AreEqual("web-tag", message.Webpush.Notification.Tag);
		Assert.AreEqual(new DateTimeOffset(2025, 1, 1, 1, 2, 3, TimeSpan.Zero).ToUnixTimeMilliseconds(), message.Webpush.Notification.TimestampMillis);
		CollectionAssert.AreEqual(new[] { 100, 200 }, message.Webpush.Notification.Vibrate!.ToArray());
		Assert.AreEqual(Direction.RightToLeft, message.Webpush.Notification.Direction);
	}

	private static Message InvokeCreateMessage(IPushNotification communication, IPlatformIdentityAddress recipient)
	{
		return (Message)CreateMessageMethod.Invoke(null, [communication, recipient])!;
	}

	private sealed class PushNotificationStub : IPushNotification
	{
		public string? Title { get; init; }
		public string? Body { get; init; }
		public string? ImageUrl { get; init; }
		public IReadOnlyDictionary<string, string>? Data { get; init; }
		public IReadOnlyDictionary<string, string>? Headers { get; init; }
		public IAndroidPushNotificationContent? Android { get; init; }
		public IApplePushNotificationContent? Apple { get; init; }
		public IWebPushNotificationContent? Web { get; init; }
		public IReadOnlyCollection<IPlatformIdentityAddress> Recipient { get; init; } = [];
		public IExtendedProperties ExtendedProperties { get; } = new ExtendedProperties();
	}

	private sealed class AndroidPushNotificationContentStub : IAndroidPushNotificationContent
	{
		public string? Title { get; init; }
		public string? Body { get; init; }
		public string? ImageUrl { get; init; }
		public IReadOnlyDictionary<string, string>? Data { get; init; }
		public IReadOnlyDictionary<string, string>? Headers { get; init; }
		public string? CollapseId { get; init; }
		public AndroidNotificationPriority? Priority { get; init; }
		public TimeSpan? TimeToLive { get; init; }
		public string? TargetApplicationId { get; init; }
		public bool? AllowDeliveryBeforeFirstUnlock { get; init; }
	}

	private sealed class ApplePushNotificationContentStub : IApplePushNotificationContent
	{
		public string? Title { get; init; }
		public string? Body { get; init; }
		public string? ImageUrl { get; init; }
		public IReadOnlyDictionary<string, string>? Data { get; init; }
		public IReadOnlyDictionary<string, string>? Headers { get; init; }
		public string? Subtitle { get; init; }
		public string? SubtitleLocalizationKey { get; init; }
		public IReadOnlyCollection<string> SubtitleLocalizationArguments { get; init; } = [];
		public string? ActionLocalizationKey { get; init; }
		public string? BodyLocalizationKey { get; init; }
		public IReadOnlyCollection<string>? BodyLocalizationArguments { get; init; }
		public string? TitleLocalizationKey { get; init; }
		public IReadOnlyCollection<string>? TitleLocalizationArguments { get; init; }
		public int? Badge { get; init; }
		public string? Sound { get; init; }
		public bool? IsBackgroundUpdate { get; init; }
		public bool? IsContentMutable { get; init; }
		public string? Category { get; init; }
		public string? ThreadId { get; init; }
	}

	private sealed class WebPushNotificationContentStub : IWebPushNotificationContent
	{
		public string? Title { get; init; }
		public string? Body { get; init; }
		public string? ImageUrl { get; init; }
		public IReadOnlyDictionary<string, string>? Data { get; init; }
		public IReadOnlyDictionary<string, string>? Headers { get; init; }
		public string? Icon { get; init; }
		public string? Badge { get; init; }
		public string? Language { get; init; }
		public bool? Renotify { get; init; }
		public bool? RequireInteraction { get; init; }
		public bool? IsSilent { get; init; }
		public string? Tag { get; init; }
		public DateTimeOffset? Timestamp { get; init; }
		public IReadOnlyCollection<int>? VibratePattern { get; init; }
		public WebPushDisplayDirection? Direction { get; init; }
	}
}
