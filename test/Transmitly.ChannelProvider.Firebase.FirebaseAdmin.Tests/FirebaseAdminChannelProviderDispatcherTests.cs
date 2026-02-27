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
using System.Reflection;

namespace Transmitly.ChannelProvider.Firebase.FirebaseAdmin.Tests;

[TestClass]
public sealed class FirebaseAdminChannelProviderDispatcherTests
{
	private static readonly MethodInfo TryConvertToDictionaryMethod =
		typeof(FirebaseAdminChannelProviderDispatcher)
			.GetMethod("TryConvertToDictionary", BindingFlags.Static | BindingFlags.NonPublic)!;

	[TestMethod]
	public void TryConvertToDictionary_ShouldUseDictionaryEntries_AndFilterExcludedKeys()
	{
		var input = new Dictionary<string, object?>
		{
			["name"] = "alice",
			["amount"] = 12.5m,
			["trx"] = "ignore",
			["pid"] = "ignore",
			["nullable"] = null
		};

		var result = InvokeTryConvertToDictionary(input);

		Assert.IsNotNull(result);
		Assert.AreEqual("alice", result["name"]);
		Assert.AreEqual("12.5", result["amount"]);
		Assert.IsFalse(result.ContainsKey("trx"));
		Assert.IsFalse(result.ContainsKey("pid"));
		Assert.IsFalse(result.ContainsKey("nullable"));
	}

	[TestMethod]
	public void TryConvertToDictionary_ShouldUseReflectionForPoco()
	{
		var input = new SampleModel
		{
			Title = "hello",
			Count = 3
		};

		var result = InvokeTryConvertToDictionary(input);

		Assert.IsNotNull(result);
		Assert.AreEqual("hello", result["Title"]);
		Assert.AreEqual("3", result["Count"]);
	}

	[TestMethod]
	public void TryConvertToDictionary_ShouldReturnNull_WhenNoUsableValues()
	{
		var result = InvokeTryConvertToDictionary(new Dictionary<string, object?>
		{
			["trx"] = new object(),
			["lnk"] = "ignore",
			["att"] = "ignore",
			["pid"] = "ignore"
		});

		Assert.IsNull(result);
	}

	[TestMethod]
	public void TryConvertToDictionary_ShouldExcludeReservedKeys_WhenValuesAreComplexObjects()
	{
		var input = new Dictionary<string, object?>
		{
			["trx"] = new Dictionary<string, object?> { ["OtpCode"] = "123456" },
			["pid"] = new { Id = "abc123" },
			["att"] = new[] { "file1" },
			["lnk"] = new[] { "cid:hero" },
			["custom"] = "kept"
		};

		var result = InvokeTryConvertToDictionary(input);

		Assert.IsNotNull(result);
		Assert.AreEqual("kept", result["custom"]);
		Assert.IsFalse(result.ContainsKey("trx"));
		Assert.IsFalse(result.ContainsKey("pid"));
		Assert.IsFalse(result.ContainsKey("att"));
		Assert.IsFalse(result.ContainsKey("lnk"));
	}

	[TestMethod]
	public void TryConvertToDictionary_ShouldConvertGuidValuesToString()
	{
		var expectedGuid = new Guid("8c121f6b-778b-4b57-a487-2b2522f82306");

		var result = InvokeTryConvertToDictionary(new Dictionary<string, object?>
		{
			["requestId"] = expectedGuid
		});

		Assert.IsNotNull(result);
		Assert.AreEqual(expectedGuid.ToString(), result["requestId"]);
	}

	[TestMethod]
	public void TryConvertToDictionary_ShouldFlattenNestedGuid_FromAnonymousModelShape()
	{
		var expectedGuid = new Guid("babf2719-357d-4594-a2cc-328e6b83c38a");

		var input = new
		{
			Type = "Recipients",
			Data = new
			{
				Guid = expectedGuid
			}
		};

		var result = InvokeTryConvertToDictionary(input);

		Assert.IsNotNull(result);
		Assert.AreEqual("Recipients", result["Type"]);
		Assert.AreEqual(expectedGuid.ToString(), result["Data.Guid"]);
	}

	[TestMethod]
	public void TryConvertToDictionary_ShouldFlattenNestedGuid_FromDictionaryModelShape()
	{
		var expectedGuid = new Guid("babf2719-357d-4594-a2cc-328e6b83c38a");
		var input = new Dictionary<string, object?>
		{
			["Type"] = "Recipients",
			["Data"] = new Dictionary<string, object?>
			{
				["Guid"] = expectedGuid
			}
		};

		var result = InvokeTryConvertToDictionary(input);

		Assert.IsNotNull(result);
		Assert.AreEqual("Recipients", result["Type"]);
		Assert.AreEqual(expectedGuid.ToString(), result["Data.Guid"]);
	}

	[TestMethod]
	public void TryConvertToDictionary_ShouldFlattenNestedGuid_FromTransactionModelCreate()
	{
		var expectedGuid = new Guid("babf2719-357d-4594-a2cc-328e6b83c38a");
		var transactionModel = TransactionModel.Create(new
		{
			Type = "Recipients",
			Data = new
			{
				Guid = expectedGuid
			}
		});

		var result = InvokeTryConvertToDictionary(transactionModel.Model);

		Assert.IsNotNull(result);
		Assert.AreEqual("Recipients", result["Type"]);
		Assert.AreEqual(expectedGuid.ToString(), result["Data.Guid"]);
	}

	private static Dictionary<string, string>? InvokeTryConvertToDictionary(object? model)
	{
		return (Dictionary<string, string>?)TryConvertToDictionaryMethod.Invoke(null, [model]);
	}

	private sealed class SampleModel
	{
		public string? Title { get; set; }
		public int Count { get; set; }
	}
}
