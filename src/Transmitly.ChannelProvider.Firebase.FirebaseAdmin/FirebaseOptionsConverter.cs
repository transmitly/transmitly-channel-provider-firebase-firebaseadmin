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
using System.Security.Cryptography;
using System.Text;
using Transmitly.ChannelProvider.Firebase.Configuration;
using Transmitly.Util;

namespace Transmitly.ChannelProvider.Firebase.FirebaseAdmin
{
	static class FirebaseOptionsConverter
	{
		public static AppOptions FromFirebaseOptions(FirebaseOptions options)
		{
			Guard.AgainstNull(options);

			return new AppOptions
			{
				Credential = CreateCredential(options.Credential),
				ProjectId = options.ProjectId,
				ServiceAccountId = options.ServiceAccountId
			};
		}

		/// <summary>
		/// Identifies the credential and project an app is built from, without keeping the secret itself.
		/// </summary>
		public static string Fingerprint(FirebaseOptions options)
		{
			var credential = options.Credential;
			// Same order as CreateCredential. A key file is read each time so a replaced file is picked up;
			// a stream can't be read twice, so the dispatcher keys on the stream itself instead.
			var source =
				credential == null ? "none" :
				credential.IsDefault ? "default" :
				credential.IsAccessToken ? "token:" + credential.AccessToken :
				credential.IsJson ? $"json:{credential.Type}:{credential.Json}" :
				credential.IsFilePath ? $"file:{credential.Type}:{File.ReadAllText(credential.FilePath!)}" :
				"stream";

			var data = Encoding.UTF8.GetBytes(source + "\n" + options.ProjectId + "\n" + options.ServiceAccountId);
#if NET
			return Convert.ToBase64String(SHA256.HashData(data));
#else
			using var sha = SHA256.Create();
			return Convert.ToBase64String(sha.ComputeHash(data));
#endif
		}

		private static GoogleCredential? CreateCredential(FirebaseCredential? firebaseCredential)
		{
			if (firebaseCredential == null)
				return null;
			
			GoogleCredential? credential = null;
			
			if (firebaseCredential.IsDefault)
				credential = GoogleCredential.GetApplicationDefault();
			else if (firebaseCredential.IsAccessToken)
				credential = GoogleCredential.FromAccessToken(firebaseCredential.AccessToken);
			// CredentialFactory only accepts the declared credential type, per Google's guidance for credential files.
			else if (firebaseCredential.IsJson)
				credential = CredentialFactory.FromJson(firebaseCredential.Json!, TypeName(firebaseCredential.Type));
			else if (firebaseCredential.IsFilePath)
				credential = CredentialFactory.FromFile(firebaseCredential.FilePath!, TypeName(firebaseCredential.Type));
			else if (firebaseCredential.IsStream)
			{
				// The typed FromStream isn't public, so load the stream's JSON; the caller's stream stays open.
				using var reader = new StreamReader(firebaseCredential.Stream!, Encoding.UTF8, true, 1024, leaveOpen: true);
				credential = CredentialFactory.FromJson(reader.ReadToEnd(), TypeName(firebaseCredential.Type));
			}
			else
				throw new NotSupportedException("No suitable credential method was found to generate a google credential.");

			return credential;
		}

		private static string TypeName(FirebaseCredentialType type) => type switch
		{
			FirebaseCredentialType.ServiceAccount => JsonCredentialParameters.ServiceAccountCredentialType,
			FirebaseCredentialType.AuthorizedUser => JsonCredentialParameters.AuthorizedUserCredentialType,
			FirebaseCredentialType.ImpersonatedServiceAccount => JsonCredentialParameters.ImpersonatedServiceAccountCredentialType,
			FirebaseCredentialType.ExternalAccount => JsonCredentialParameters.ExternalAccountCredentialType,
			FirebaseCredentialType.ExternalAccountAuthorizedUser => JsonCredentialParameters.ExternalAccountAuthorizedUserCredentialType,
			_ => throw new NotSupportedException($"Unknown Firebase credential type '{type}'.")
		};
	}
}