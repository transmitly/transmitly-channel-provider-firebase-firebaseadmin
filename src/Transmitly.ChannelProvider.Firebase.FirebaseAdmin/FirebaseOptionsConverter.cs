// ﻿﻿Copyright (c) Code Impressions, LLC. All Rights Reserved.
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

		private static GoogleCredential? CreateCredential(FirebaseCredential? firebaseCredential)
		{
			if (firebaseCredential == null)
				return null;
			
			GoogleCredential? credential = null;
			
			if (firebaseCredential.IsDefault)
				credential = GoogleCredential.GetApplicationDefault();
			else if (firebaseCredential.IsAccessToken)
				credential = GoogleCredential.FromAccessToken(firebaseCredential.AccessToken);
			else if (firebaseCredential.IsJson)
				credential = GoogleCredential.FromJson(firebaseCredential.Json);
			else if (firebaseCredential.IsFilePath)
				credential = GoogleCredential.FromFile(firebaseCredential.FilePath);
			else if (firebaseCredential.IsStream)
				credential = GoogleCredential.FromStream(firebaseCredential.Stream);
			else
				throw new NotSupportedException("No suitable credential method was found to generate a google credential.");

			return credential;
		}
	}
}