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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;
using Transmitly.ChannelProvider.Firebase.Configuration;

namespace Transmitly.ChannelProvider.Firebase.FirebaseAdmin.Tests;

[TestClass]
public sealed class FirebaseCredentialTests
{
	private const string UserJson = "{\"type\":\"authorized_user\",\"client_id\":\"id\",\"client_secret\":\"secret\",\"refresh_token\":\"refresh\"}";

	[TestMethod]
	[DataRow("json")]
	[DataRow("file")]
	[DataRow("stream")]
	public void CredentialData_MustBeTheDeclaredType(string source)
	{
		Assert.IsNotNull(Convert(source, FirebaseCredentialType.AuthorizedUser).Credential);

		// The default is a service account, so the same user credential is refused.
		Assert.ThrowsExactly<InvalidOperationException>(() => Convert(source, FirebaseCredentialType.ServiceAccount));
	}

	private static global::FirebaseAdmin.AppOptions Convert(string source, FirebaseCredentialType type)
	{
		var credential = source switch
		{
			"json" => FirebaseCredential.FromJson(UserJson, type),
			"file" => FirebaseCredential.FromFile(WriteTempFile(UserJson), type),
			_ => FirebaseCredential.FromStream(new MemoryStream(Encoding.UTF8.GetBytes(UserJson)), type)
		};
		return FirebaseOptionsConverter.FromFirebaseOptions(new FirebaseOptions { Credential = credential });
	}

	private static string WriteTempFile(string content)
	{
		var path = Path.GetTempFileName();
		File.WriteAllText(path, content);
		return path;
	}
}
