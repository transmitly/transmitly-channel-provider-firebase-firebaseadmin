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
using Google.Apis.Auth.OAuth2;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;
using Transmitly.ChannelProvider.Firebase.Configuration;

namespace Transmitly.ChannelProvider.Firebase.FirebaseAdmin.Tests;

[TestClass]
public sealed class FirebaseAppCacheTests
{
	[TestMethod]
	public void SameOptions_ShareOneApp()
	{
		var options = Options();

		Assert.AreSame(FirebaseAdminChannelProviderDispatcher.GetApp(options), FirebaseAdminChannelProviderDispatcher.GetApp(options));
	}

	[TestMethod]
	public void RotatedCredential_GetsANewApp()
	{
		var options = Options();
		var before = FirebaseAdminChannelProviderDispatcher.GetApp(options);

		options.Credential = FirebaseCredential.FromAccessToken(Guid.NewGuid().ToString());

		Assert.AreNotSame(before, FirebaseAdminChannelProviderDispatcher.GetApp(options));
	}

	[TestMethod]
	public void ProvidersWithoutAnAppName_DoNotShareAnApp()
	{
		var tenantA = new FirebaseOptions { ProjectId = "project", Credential = FirebaseCredential.FromAccessToken(Guid.NewGuid().ToString()) };
		var tenantB = new FirebaseOptions { ProjectId = "project", Credential = FirebaseCredential.FromAccessToken(Guid.NewGuid().ToString()) };

		Assert.AreNotSame(FirebaseAdminChannelProviderDispatcher.GetApp(tenantA), FirebaseAdminChannelProviderDispatcher.GetApp(tenantB));
	}

	[TestMethod]
	public void StreamCredential_SurvivesMoreThanOneDispatch()
	{
		// Core builds a dispatcher per dispatch; the stream used to be read again by every one.
		var json = "{\"type\":\"authorized_user\",\"client_id\":\"id\",\"client_secret\":\"secret\",\"refresh_token\":\"refresh\"}";
		var options = Options();
		options.Credential = FirebaseCredential.FromStream(new MemoryStream(Encoding.UTF8.GetBytes(json)), FirebaseCredentialType.AuthorizedUser);

		_ = new FirebaseAdminChannelProviderDispatcher(options);
		_ = new FirebaseAdminChannelProviderDispatcher(options);
	}

	[TestMethod]
	public void DeleteApps_DeletesTheAppsForThatName()
	{
		var options = Options();
		var app = FirebaseAdminChannelProviderDispatcher.GetApp(options);
		var other = FirebaseAdminChannelProviderDispatcher.GetApp(Options());

		FirebaseAdminChannelProviderDispatcher.DeleteApps(options.AppName);

		Assert.IsNull(FirebaseApp.GetInstance(app.Name));
		Assert.IsNotNull(FirebaseApp.GetInstance(other.Name));
		Assert.AreNotSame(app, FirebaseAdminChannelProviderDispatcher.GetApp(options));
	}

	[TestMethod]
	public void AppCreatedByTheHost_IsUsedAsIs()
	{
		var options = Options();
		var hostApp = FirebaseApp.Create(new AppOptions { ProjectId = "host", Credential = GoogleCredential.FromAccessToken("host") }, options.AppName);

		Assert.AreSame(hostApp, FirebaseAdminChannelProviderDispatcher.GetApp(options));
	}

	private static FirebaseOptions Options() => new()
	{
		AppName = "test-" + Guid.NewGuid().ToString("N"),
		ProjectId = "project",
		Credential = FirebaseCredential.FromAccessToken(Guid.NewGuid().ToString())
	};
}
