# Transmitly.ChannelProvider.Firebase.FirebaseAdmin

A [Transmitly](https://github.com/transmitly/transmitly) Firebase channel provider dispatcher implementation using Google's [Firebase Admin .NET SDK](https://github.com/firebase/firebase-admin-dotnet).

## Recommended Package

Most users should use the convenience package:

- [transmitly-channel-provider-firebase](https://github.com/transmitly/transmitly-channel-provider-firebase)

It provides `AddFirebaseSupport(...)` and registers this dispatcher for you.

## Using This Package Directly (Advanced)

Use this package directly if you want explicit control over channel-provider registration and dispatcher wiring.

The convenience package registers this provider with the equivalent of:

```csharp
using Transmitly;
using Transmitly.ChannelProvider.Firebase.Configuration;
using Transmitly.ChannelProvider.Firebase.FirebaseAdmin;

var builder = new CommunicationsClientBuilder();

builder.ChannelProvider
	.Build(Id.ChannelProvider.Firebase(), new FirebaseOptions
	{
		Credential = FirebaseCredential.FromFile("firebase-service-account.json"),
		ProjectId = "your-firebase-project-id", // optional when available via credential/environment
		AppName = "default" // optional
	})
	.AddDispatcher<FirebaseAdminChannelProviderDispatcher, IPushNotification>(Id.Channel.PushNotification())
	.Register();
```

See the [Transmitly](https://github.com/transmitly/transmitly) project for pipeline/channel concepts and end-to-end setup.

---
_Copyright (c) Code Impressions, LLC. This open-source project is sponsored and maintained by Code Impressions and is licensed under the [Apache License, Version 2.0](http://apache.org/licenses/LICENSE-2.0.html)._
